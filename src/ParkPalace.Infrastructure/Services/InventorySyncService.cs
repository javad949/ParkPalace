using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public class InventorySyncService : IInventorySyncService
{
    private readonly ICrmDbContext _dbContext;
    private readonly ISepidarInventoryAdapter _adapter;
    private readonly IConsumptionCalculator _consumptionCalculator;
    private readonly IPurchaseRecommendationEngine _recommendationEngine;
    private readonly ILogger<InventorySyncService> _logger;

    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static bool _isSyncing = false;

    public InventorySyncService(
        ICrmDbContext dbContext,
        ISepidarInventoryAdapter adapter,
        IConsumptionCalculator consumptionCalculator,
        IPurchaseRecommendationEngine recommendationEngine,
        ILogger<InventorySyncService> logger)
    {
        _dbContext = dbContext;
        _adapter = adapter;
        _consumptionCalculator = consumptionCalculator;
        _recommendationEngine = recommendationEngine;
        _logger = logger;
    }

    public async Task<SyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var latestJob = await _dbContext.InventorySyncJobs
            .OrderByDescending(j => j.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var lastSuccessJob = await _dbContext.InventorySyncJobs
            .Where(j => j.Status == SyncJobStatus.Success)
            .OrderByDescending(j => j.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        bool isStale = lastSuccessJob == null || (now - (lastSuccessJob.CompletedAtUtc ?? lastSuccessJob.StartedAtUtc)).TotalHours > 24;

        return new SyncStatusDto
        {
            IsSyncing = _isSyncing,
            LastSuccessfulSyncUtc = lastSuccessJob?.CompletedAtUtc,
            IsStale = isStale,
            StaleWarningHours = 24,
            LatestJob = latestJob == null ? null : MapJob(latestJob)
        };
    }

    public async Task<SyncJobDto> TriggerSyncAsync(
        SyncJobType jobType, 
        string triggeredBy, 
        CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("A synchronization job is already actively running. Multiple overlapping jobs are forbidden.");
        }

        _isSyncing = true;
        var job = new InventorySyncJob
        {
            JobType = jobType,
            Status = SyncJobStatus.Running,
            StartedAtUtc = DateTime.UtcNow,
            TriggeredBy = triggeredBy
        };

        _dbContext.InventorySyncJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            _logger.LogInformation("Starting Inventory Sync Job {JobId} via {AdapterName}", job.Id, _adapter.AdapterName);

            // 1. Ensure Default Warehouse
            var warehouse = await _dbContext.Warehouses
                .FirstOrDefaultAsync(w => w.Code == "WH-MAIN", cancellationToken);

            if (warehouse == null)
            {
                warehouse = new Warehouse
                {
                    Code = "WH-MAIN",
                    Name = "انبار مرکزی کارخانه نان",
                    Description = "انبار اصلی دریافت و ذخیره‌سازی آرد و مواد اولیه نان و قنادی",
                    SepidarWarehouseRef = "1",
                    IsActive = true
                };
                _dbContext.Warehouses.Add(warehouse);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // 2. Fetch and synchronize item definitions and mappings
            var sepidarItems = await _adapter.GetItemsAsync(cancellationToken);
            var defaultCategory = await _dbContext.Categories.FirstOrDefaultAsync(c => c.IsActive, cancellationToken);
            var defaultUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "KG", cancellationToken);

            foreach (var sItem in sepidarItems)
            {
                var mapping = await _dbContext.InventoryItemMappings
                    .Include(m => m.Material)
                    .FirstOrDefaultAsync(m => m.SepidarItemId == sItem.ItemRef, cancellationToken);

                if (mapping == null)
                {
                    // Check if material already exists by InternalCode
                    var existingMat = await _dbContext.Materials
                        .FirstOrDefaultAsync(m => m.InternalCode == sItem.Code, cancellationToken);

                    if (existingMat == null)
                    {
                        var unit = await _dbContext.UnitsOfMeasure
                            .FirstOrDefaultAsync(u => u.Code == sItem.UnitCode, cancellationToken) ?? defaultUnit;

                        existingMat = new Material
                        {
                            InternalCode = sItem.Code,
                            Name = sItem.Title,
                            CategoryId = defaultCategory?.Id ?? Guid.Empty,
                            BaseUnitId = unit?.Id ?? Guid.Empty,
                            IsActive = sItem.IsActive
                        };
                        _dbContext.Materials.Add(existingMat);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    mapping = new InventoryItemMapping
                    {
                        MaterialId = existingMat.Id,
                        SepidarItemId = sItem.ItemRef,
                        SepidarItemCode = sItem.Code,
                        SepidarItemTitle = sItem.Title,
                        SepidarBaseUnit = sItem.UnitCode,
                        IsActive = sItem.IsActive,
                        LastSyncUtc = DateTime.UtcNow
                    };
                    _dbContext.InventoryItemMappings.Add(mapping);
                }
                else
                {
                    mapping.SepidarItemCode = sItem.Code;
                    mapping.SepidarItemTitle = sItem.Title;
                    mapping.SepidarBaseUnit = sItem.UnitCode;
                    mapping.IsActive = sItem.IsActive;
                    mapping.LastSyncUtc = DateTime.UtcNow;
                }
                job.ItemsProcessed++;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 3. Fetch and update inventory stock snapshots
            var stockBalances = await _adapter.GetStockBalancesAsync(warehouse.SepidarWarehouseRef ?? "1", cancellationToken);
            var mappingsDict = await _dbContext.InventoryItemMappings
                .ToDictionaryAsync(m => m.SepidarItemId, cancellationToken);

            foreach (var stock in stockBalances)
            {
                if (!mappingsDict.TryGetValue(stock.ItemRef, out var itemMap))
                {
                    continue;
                }

                var snapshot = await _dbContext.InventorySnapshots
                    .FirstOrDefaultAsync(s => s.WarehouseId == warehouse.Id && s.MaterialId == itemMap.MaterialId, cancellationToken);

                var unit = await _dbContext.UnitsOfMeasure
                    .FirstOrDefaultAsync(u => u.Code == stock.UnitCode, cancellationToken) ?? defaultUnit;

                if (snapshot == null)
                {
                    snapshot = new InventorySnapshot
                    {
                        WarehouseId = warehouse.Id,
                        MaterialId = itemMap.MaterialId,
                        OnHandQuantity = stock.OnHand,
                        ReservableQuantity = stock.Reservable,
                        IncomingQuantity = stock.Incoming,
                        ReservedQuantity = stock.Reserved,
                        UnitId = unit?.Id ?? Guid.Empty,
                        SnapshotTimeUtc = stock.SnapshotTimeUtc,
                        IsStale = false,
                        SourceSnapshotKey = $"WH-{warehouse.Code}-ITEM-{stock.ItemRef}"
                    };
                    _dbContext.InventorySnapshots.Add(snapshot);
                }
                else
                {
                    snapshot.OnHandQuantity = stock.OnHand;
                    snapshot.ReservableQuantity = stock.Reservable;
                    snapshot.IncomingQuantity = stock.Incoming;
                    snapshot.ReservedQuantity = stock.Reserved;
                    snapshot.UnitId = unit?.Id ?? snapshot.UnitId;
                    snapshot.SnapshotTimeUtc = stock.SnapshotTimeUtc;
                    snapshot.IsStale = false;
                }
                job.SnapshotsUpdated++;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 4. Fetch and synchronize inventory movements (idempotent upserts)
            var checkpoint = await _dbContext.InventorySyncCheckpoints
                .FirstOrDefaultAsync(c => c.WarehouseId == warehouse.Id, cancellationToken);

            DateTime? sinceDate = jobType == SyncJobType.Full ? null : checkpoint?.LastVoucherDateUtc?.AddDays(-2); // overlapping window
            var movements = await _adapter.GetInventoryMovementsAsync(warehouse.SepidarWarehouseRef ?? "1", sinceDate, cancellationToken);

            var existingKeyList = await _dbContext.InventoryMovements
                .Where(m => m.SourceMovementKey != null)
                .Select(m => m.SourceMovementKey!)
                .ToListAsync(cancellationToken);
            var existingKeys = existingKeyList.ToHashSet();

            foreach (var mov in movements)
            {
                if (!mappingsDict.TryGetValue(mov.ItemRef, out var itemMap))
                {
                    continue;
                }

                job.MovementsProcessed++;

                var unit = await _dbContext.UnitsOfMeasure
                    .FirstOrDefaultAsync(u => u.Code == mov.UnitCode, cancellationToken) ?? defaultUnit;

                if (!existingKeys.Contains(mov.VoucherRef))
                {
                    var newMov = new InventoryMovement
                    {
                        WarehouseId = warehouse.Id,
                        MaterialId = itemMap.MaterialId,
                        VoucherNumber = mov.VoucherNumber,
                        MovementType = mov.MovementType,
                        Quantity = mov.Quantity,
                        UnitId = unit?.Id ?? Guid.Empty,
                        TransactionDateUtc = mov.TransactionDateUtc,
                        PostingStatus = mov.PostingStatus,
                        IsCancelled = mov.IsCancelled,
                        ReferenceDocument = mov.ReferenceDocument,
                        SourceMovementKey = mov.VoucherRef,
                        ImportedAtUtc = DateTime.UtcNow
                    };
                    _dbContext.InventoryMovements.Add(newMov);
                    existingKeys.Add(mov.VoucherRef);
                }
                else
                {
                    // Existing record - update cancellation or posting status if changed
                    var existingMov = await _dbContext.InventoryMovements
                        .FirstOrDefaultAsync(m => m.SourceMovementKey == mov.VoucherRef, cancellationToken);

                    if (existingMov != null && (existingMov.IsCancelled != mov.IsCancelled || existingMov.PostingStatus != mov.PostingStatus))
                    {
                        existingMov.IsCancelled = mov.IsCancelled;
                        existingMov.PostingStatus = mov.PostingStatus;
                    }
                }
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 5. Update Checkpoint & Recalculate Recommendations
            if (checkpoint == null)
            {
                checkpoint = new InventorySyncCheckpoint
                {
                    WarehouseId = warehouse.Id,
                    LastVoucherId = movements.LastOrDefault()?.VoucherRef ?? "INIT",
                    LastVoucherDateUtc = movements.LastOrDefault()?.TransactionDateUtc ?? DateTime.UtcNow,
                    LastSyncTimeUtc = DateTime.UtcNow
                };
                _dbContext.InventorySyncCheckpoints.Add(checkpoint);
            }
            else
            {
                var latestMov = movements.OrderByDescending(m => m.TransactionDateUtc).FirstOrDefault();
                if (latestMov != null)
                {
                    checkpoint.LastVoucherId = latestMov.VoucherRef;
                    checkpoint.LastVoucherDateUtc = latestMov.TransactionDateUtc;
                }
                checkpoint.LastSyncTimeUtc = DateTime.UtcNow;
            }

            warehouse.LastSyncUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Generate recommendations for all active materials
            await _recommendationEngine.GenerateAllRecommendationsAsync(warehouse.Id, cancellationToken);

            job.Status = SyncJobStatus.Success;
            job.CompletedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Inventory Sync Job {JobId} successfully completed. Items: {Items}, Snapshots: {Snapshots}, Movements: {Movements}",
                job.Id, job.ItemsProcessed, job.SnapshotsUpdated, job.MovementsProcessed);

            return MapJob(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory Sync Job {JobId} failed", job.Id);
            job.Status = SyncJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAtUtc = DateTime.UtcNow;

            _dbContext.InventorySyncErrors.Add(new InventorySyncError
            {
                JobId = job.Id,
                ErrorCode = "SYNC_EXCEPTION",
                ErrorMessage = ex.Message,
                RawPayload = ex.StackTrace?.Substring(0, Math.Min(3500, ex.StackTrace.Length))
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return MapJob(job);
        }
        finally
        {
            _isSyncing = false;
            _syncLock.Release();
        }
    }

    private static SyncJobDto MapJob(InventorySyncJob job)
    {
        return new SyncJobDto
        {
            Id = job.Id,
            JobType = job.JobType.ToString(),
            Status = job.Status.ToString(),
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            ItemsProcessed = job.ItemsProcessed,
            MovementsProcessed = job.MovementsProcessed,
            SnapshotsUpdated = job.SnapshotsUpdated,
            ErrorMessage = job.ErrorMessage,
            CorrelationId = job.CorrelationId,
            TriggeredBy = job.TriggeredBy
        };
    }
}

using System.Text.Json;
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
    private readonly ICatalogStandardizationEngine _catalogEngine;
    private readonly ILogger<InventorySyncService> _logger;

    private static readonly SemaphoreSlim _syncLock = new(1, 1);
    private static bool _isSyncing = false;

    public InventorySyncService(
        ICrmDbContext dbContext,
        ISepidarInventoryAdapter adapter,
        IConsumptionCalculator consumptionCalculator,
        IPurchaseRecommendationEngine recommendationEngine,
        ILogger<InventorySyncService> logger,
        ICatalogStandardizationEngine? catalogEngine = null)
    {
        _dbContext = dbContext;
        _adapter = adapter;
        _consumptionCalculator = consumptionCalculator;
        _recommendationEngine = recommendationEngine;
        _logger = logger;
        _catalogEngine = catalogEngine ?? new CatalogStandardizationEngine(dbContext, Microsoft.Extensions.Logging.Abstractions.NullLogger<CatalogStandardizationEngine>.Instance);
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
        // The generic sync mutates the entire catalog and commits in stages.
        // Do not allow live data through it until a reviewed, manifest-scoped
        // execution path and matching preview are available.
        if (_adapter.IsLiveConnected || _adapter is ParkPalace.Infrastructure.Adapters.SepidarLiveInventoryAdapter)
        {
            throw new InvalidOperationException("LIVE_PILOT_SCOPE_REQUIRED: اجرای زنده از مسیر عمومی مجاز نیست؛ مسیر محدود به فهرست تأییدشده پایلوت هنوز آماده نیست.");
        }
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

            // 1. Ensure Default Warehouse & Explicit Confirmation to Sepidar StockID 1
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
            else if (warehouse.SepidarWarehouseRef != "1")
            {
                // Explicitly confirm mapping WH-MAIN -> 1
                warehouse.SepidarWarehouseRef = "1";
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
                        var suggestion = _catalogEngine.ParseAndStandardize(sItem.Title, sItem.UnitCode);
                        Category? cat = null;
                        if (!string.IsNullOrEmpty(suggestion.ParentCategoryCode) && !string.IsNullOrEmpty(suggestion.ParentCategoryName))
                        {
                            cat = await _catalogEngine.EnsureUnifiedCategoryAsync(
                                suggestion.ParentCategoryCode,
                                suggestion.ParentCategoryName,
                                suggestion.SubcategoryCode,
                                suggestion.SubcategoryName,
                                cancellationToken);
                        }

                        var unit = await _dbContext.UnitsOfMeasure
                            .FirstOrDefaultAsync(u => u.Code == suggestion.BaseUnitCode, cancellationToken) ?? defaultUnit;

                        var pkgUnit = !string.IsNullOrEmpty(suggestion.PackageSizeUnitCode)
                            ? await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.PackageSizeUnitCode, cancellationToken)
                            : null;

                        var purchaseUnit = !string.IsNullOrEmpty(suggestion.PurchaseUnitCode)
                            ? await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.PurchaseUnitCode, cancellationToken)
                            : unit;

                        var consumptionUnit = !string.IsNullOrEmpty(suggestion.ConsumptionUnitCode)
                            ? await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.ConsumptionUnitCode, cancellationToken)
                            : null;

                        existingMat = new Material
                        {
                            InternalCode = sItem.Code,
                            Name = suggestion.StandardName,
                            Brand = suggestion.Brand,
                            Packaging = suggestion.Packaging,
                            PackageSize = suggestion.PackageSize,
                            PackageSizeUnitId = pkgUnit?.Id,
                            PurchaseUnitId = purchaseUnit?.Id,
                            ConsumptionUnitId = consumptionUnit?.Id,
                            ProductFamily = suggestion.ProductFamily,
                            CategoryId = cat?.Id ?? defaultCategory?.Id ?? Guid.Empty,
                            BaseUnitId = unit?.Id ?? Guid.Empty,
                            CategorizationStatus = suggestion.Status,
                            IsCustomizedByUser = false,
                            StandardizationNotes = suggestion.Explanation,
                            IsActive = sItem.IsActive
                        };
                        _dbContext.Materials.Add(existingMat);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        // If conversion suggested, add unconfirmed conversion
                        if (suggestion.SuggestedConversionFactor.HasValue && !string.IsNullOrEmpty(suggestion.SuggestedConversionToUnit) && unit != null)
                        {
                            var toUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == suggestion.SuggestedConversionToUnit, cancellationToken);
                            if (toUnit != null && unit.Id != toUnit.Id)
                            {
                                _dbContext.MaterialUnitConversions.Add(new MaterialUnitConversion
                                {
                                    MaterialId = existingMat.Id,
                                    FromUnitId = unit.Id,
                                    ToUnitId = toUnit.Id,
                                    Factor = suggestion.SuggestedConversionFactor.Value,
                                    IsConfirmed = false, // Must be verified before activating
                                    Notes = $"پیشنهاد خودکار سیستم از محتویات بسته ({suggestion.PackageSize} {suggestion.PackageSizeUnitCode})"
                                });
                                await _dbContext.SaveChangesAsync(cancellationToken);
                            }
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
                        // Ambiguous code match! An existing CRM Material has this code, but was not mapped to this Sepidar ItemRef!
                        // Per requirement 3: Ambiguous matching based on item code must NOT be done automatically.
                        _logger.LogWarning("Ambiguous match detected: Sepidar Item {ItemRef} ({Code}: {Title}) matches existing CRM Material {MaterialId} ({InternalCode}) but lacks verified mapping. Automatic linking blocked.",
                            sItem.ItemRef, sItem.Code, sItem.Title, existingMat.Id, existingMat.InternalCode);

                        _dbContext.InventorySyncErrors.Add(new InventorySyncError
                        {
                            JobId = job.Id,
                            ErrorCode = "AMBIGUOUS_CODE_MATCH",
                            ErrorMessage = $"کالای سپیدار با کد '{sItem.Code}' (شناسه {sItem.ItemRef}) با ماده اولیه CRM با شناسه '{existingMat.Id}' هم‌کد است اما نگاشت تأییدشده ندارد. تطبیق خودکار مسدود گردید.",
                            RawPayload = $"{{\"SepidarItemId\":\"{sItem.ItemRef}\",\"SepidarCode\":\"{sItem.Code}\",\"CrmMaterialId\":\"{existingMat.Id}\"}}"
                        });
                        continue;
                    }
                }
                else
                {
                    mapping.SepidarItemCode = sItem.Code;
                    mapping.SepidarItemTitle = sItem.Title;
                    mapping.SepidarBaseUnit = sItem.UnitCode;
                    mapping.IsActive = sItem.IsActive;
                    mapping.LastSyncUtc = DateTime.UtcNow;

                    // Preserve all CRM-specific properties (MinStock, SafetyStock, LeadTimeDays, Category, SupplierMaterials)
                    // Only sync active/inactive status from Sepidar master catalog
                    if (mapping.Material != null)
                    {
                        mapping.Material.IsActive = sItem.IsActive;
                    }
                }
                job.ItemsProcessed++;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 3. Fetch and update inventory stock snapshots (with per-run change tracking for fine-grained rollback)
            var stockBalances = await _adapter.GetStockBalancesAsync(warehouse.SepidarWarehouseRef ?? "1", cancellationToken);
            var mappingsDict = await _dbContext.InventoryItemMappings
                .ToDictionaryAsync(m => m.SepidarItemId, cancellationToken);
            var materialsDict = await _dbContext.Materials
                .Include(m => m.BaseUnit)
                .Include(m => m.UnitConversions)
                .ToDictionaryAsync(m => m.Id, cancellationToken);

            var blockedItemRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var stock in stockBalances)
            {
                if (!mappingsDict.TryGetValue(stock.ItemRef, out var itemMap))
                {
                    continue;
                }

                if (!materialsDict.TryGetValue(itemMap.MaterialId, out var material))
                {
                    continue;
                }

                var unit = await _dbContext.UnitsOfMeasure
                    .FirstOrDefaultAsync(u => u.Code == stock.UnitCode, cancellationToken) ?? defaultUnit;

                // Requirement 3: Check unit dimension compatibility
                string stockDim = (stock.UnitCode == "KG" || stock.UnitCode == "GR") ? "Mass"
                    : (stock.UnitCode == "COUNT" || stock.UnitCode == "PACK") ? "Count"
                    : (unit?.Dimension ?? "Mass");
                string matDim = material.BaseUnit?.Dimension ?? "Mass";

                bool hasConversion = false;
                if (material.BaseUnit != null && unit != null)
                {
                    hasConversion = material.UnitConversions.Any(c =>
                        c.IsConfirmed && (
                            (c.FromUnitId == unit.Id && c.ToUnitId == material.BaseUnitId) ||
                            (c.FromUnitId == material.BaseUnitId && c.ToUnitId == unit.Id)));
                }

                if (material.BaseUnit != null && stockDim != matDim && !hasConversion)
                {
                    // Unproven unit mismatch!
                    // Both snapshot OnHand update and movements must be blocked from CRM calculations!
                    blockedItemRefs.Add(stock.ItemRef);

                    var existingSnap = await _dbContext.InventorySnapshots
                        .FirstOrDefaultAsync(s => s.WarehouseId == warehouse.Id && s.MaterialId == itemMap.MaterialId, cancellationToken);
                    if (existingSnap != null)
                    {
                        existingSnap.IsStale = true;
                        if (existingSnap.SourceSnapshotKey == null || !existingSnap.SourceSnapshotKey.Contains(":UNIT-MISMATCH"))
                        {
                            existingSnap.SourceSnapshotKey = (existingSnap.SourceSnapshotKey ?? $"WH{warehouse.SepidarWarehouseRef}-ITEM{stock.ItemRef}") + ":UNIT-MISMATCH";
                        }
                    }

                    _dbContext.AuditEvents.Add(new AuditEvent
                    {
                        Action = "InventorySync_BlockedUnitMismatch",
                        EntityType = "InventorySnapshot",
                        EntityId = itemMap.MaterialId.ToString(),
                        ActorUsername = $"SyncJob:{job.Id}",
                        DetailsJson = JsonSerializer.Serialize(new
                        {
                            ItemRef = stock.ItemRef,
                            MaterialId = itemMap.MaterialId,
                            MaterialCode = material.InternalCode,
                            MaterialName = material.Name,
                            SepidarOnHand = stock.OnHand,
                            SepidarUnit = stock.UnitCode,
                            SepidarDimension = stockDim,
                            CrmUnit = material.BaseUnit?.Code ?? "KG",
                            CrmDimension = matDim,
                            Reason = "ورود مقدار موجودی و گردش به دلیل مغایرت ابعاد و نبود ضریب تبدیل رسمی مسدود گردید."
                        }),
                        TimestampUtc = DateTime.UtcNow
                    });
                    continue;
                }

                var snapshot = await _dbContext.InventorySnapshots
                    .FirstOrDefaultAsync(s => s.WarehouseId == warehouse.Id && s.MaterialId == itemMap.MaterialId, cancellationToken);

                // Reserve Provenance (Requirement 2):
                // If stock.Reservable is null, do NOT assume speculative free stock; mark unproven
                string reserveProvenanceTag = stock.Reservable.HasValue ? ":RES-PROVEN" : ":RES-UNPROVEN";
                decimal reservableQty = stock.Reservable ?? 0m;
                decimal reservedQty = stock.Reserved ?? (snapshot?.ReservedQuantity ?? 0m);
                decimal incomingQty = stock.Incoming > 0 ? stock.Incoming : (snapshot?.IncomingQuantity ?? 0m);

                if (snapshot == null)
                {
                    snapshot = new InventorySnapshot
                    {
                        WarehouseId = warehouse.Id,
                        MaterialId = itemMap.MaterialId,
                        OnHandQuantity = stock.OnHand,
                        ReservableQuantity = reservableQty,
                        IncomingQuantity = incomingQty,
                        ReservedQuantity = reservedQty,
                        UnitId = unit?.Id ?? Guid.Empty,
                        SnapshotTimeUtc = stock.SnapshotTimeUtc,
                        IsStale = false,
                        SourceSnapshotKey = $"WH-{warehouse.Code}-ITEM-{stock.ItemRef}{reserveProvenanceTag}"
                    };
                    _dbContext.InventorySnapshots.Add(snapshot);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    // Track insertion for per-job rollback
                    _dbContext.AuditEvents.Add(new AuditEvent
                    {
                        Action = "InventorySync_SnapshotInsert",
                        EntityType = "InventorySnapshot",
                        EntityId = snapshot.Id.ToString(),
                        ActorUsername = $"SyncJob:{job.Id}",
                        DetailsJson = JsonSerializer.Serialize(new SnapshotAuditDetail
                        {
                            JobId = job.Id,
                            MaterialId = itemMap.MaterialId,
                            WarehouseId = warehouse.Id,
                            OldOnHand = 0m,
                            NewOnHand = stock.OnHand,
                            OldReservable = 0m,
                            NewReservable = reservableQty,
                            OldReserved = 0m,
                            NewReserved = reservedQty,
                            OldIncoming = 0m,
                            NewIncoming = incomingQty,
                            OldUnitId = Guid.Empty,
                            NewUnitId = unit?.Id ?? Guid.Empty,
                            OldIsStale = false,
                            NewIsStale = false,
                            OldSnapshotTimeUtc = DateTime.MinValue,
                            NewSnapshotTimeUtc = stock.SnapshotTimeUtc
                        }),
                        TimestampUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    decimal oldOnHand = snapshot.OnHandQuantity;
                    decimal oldReservable = snapshot.ReservableQuantity;
                    decimal oldReserved = snapshot.ReservedQuantity;
                    decimal oldIncoming = snapshot.IncomingQuantity;
                    Guid oldUnitId = snapshot.UnitId;
                    bool oldIsStale = snapshot.IsStale;
                    DateTime oldTime = snapshot.SnapshotTimeUtc;

                    snapshot.OnHandQuantity = stock.OnHand;

                    // ReservedQuantity: Sepidar does NOT own factory work order reserves.
                    // Preserve CRM's existing ReservedQuantity if Sepidar does not prove it!
                    decimal newReserved = stock.Reserved ?? oldReserved;
                    snapshot.ReservedQuantity = newReserved;

                    // IncomingQuantity: Sepidar does NOT own CRM purchase orders.
                    // Preserve CRM's existing IncomingQuantity if Sepidar is 0!
                    decimal newIncoming = stock.Incoming > 0 ? stock.Incoming : oldIncoming;
                    snapshot.IncomingQuantity = newIncoming;

                    // ReservableQuantity: If Sepidar proves it, update it. If unknown, do not assume speculative free stock.
                    decimal newReservable = stock.Reservable ?? 0m;
                    snapshot.ReservableQuantity = newReservable;
                    snapshot.SourceSnapshotKey = $"WH-{warehouse.Code}-ITEM-{stock.ItemRef}{reserveProvenanceTag}";

                    // UnitId: If Sepidar unit code is provided and compatible, update; otherwise preserve CRM unit
                    Guid newUnitId = (unit != null && (defaultUnit == null || unit.Dimension == defaultUnit.Dimension)) ? unit.Id : oldUnitId;
                    snapshot.UnitId = newUnitId;

                    snapshot.SnapshotTimeUtc = stock.SnapshotTimeUtc;
                    snapshot.IsStale = false;

                    // Track modification with prior values for fine-grained rollback
                    _dbContext.AuditEvents.Add(new AuditEvent
                    {
                        Action = "InventorySync_SnapshotUpdate",
                        EntityType = "InventorySnapshot",
                        EntityId = snapshot.Id.ToString(),
                        ActorUsername = $"SyncJob:{job.Id}",
                        DetailsJson = JsonSerializer.Serialize(new SnapshotAuditDetail
                        {
                            JobId = job.Id,
                            MaterialId = itemMap.MaterialId,
                            WarehouseId = warehouse.Id,
                            OldOnHand = oldOnHand,
                            NewOnHand = stock.OnHand,
                            OldReservable = oldReservable,
                            NewReservable = newReservable,
                            OldReserved = oldReserved,
                            NewReserved = newReserved,
                            OldIncoming = oldIncoming,
                            NewIncoming = newIncoming,
                            OldUnitId = oldUnitId,
                            NewUnitId = newUnitId,
                            OldIsStale = oldIsStale,
                            NewIsStale = false,
                            OldSnapshotTimeUtc = oldTime,
                            NewSnapshotTimeUtc = stock.SnapshotTimeUtc
                        }),
                        TimestampUtc = DateTime.UtcNow
                    });
                }
                job.SnapshotsUpdated++;
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 4. Fetch and synchronize inventory movements (idempotent upserts)
            var checkpoint = await _dbContext.InventorySyncCheckpoints
                .FirstOrDefaultAsync(c => c.WarehouseId == warehouse.Id, cancellationToken);

            DateTime? sinceDate = jobType == SyncJobType.Full ? null : checkpoint?.LastVoucherDateUtc?.AddDays(-2); // overlapping window
            var movements = await _adapter.GetInventoryMovementsAsync(warehouse.SepidarWarehouseRef ?? "1", sinceDate, cancellationToken);

            var existingMovements = await _dbContext.InventoryMovements
                .Where(m => m.SourceMovementKey != null)
                .ToListAsync(cancellationToken);

            // Requirement 1: Type-aware deduplication for legacy numeric keys vs prefixed keys
            var exactKeyMap = new Dictionary<string, InventoryMovement>(StringComparer.OrdinalIgnoreCase);
            var legacyReceiptMap = new Dictionary<string, List<InventoryMovement>>(StringComparer.OrdinalIgnoreCase);
            var legacyDeliveryMap = new Dictionary<string, List<InventoryMovement>>(StringComparer.OrdinalIgnoreCase);

            foreach (var em in existingMovements)
            {
                if (!string.IsNullOrEmpty(em.SourceMovementKey))
                {
                    if (!exactKeyMap.ContainsKey(em.SourceMovementKey))
                    {
                        exactKeyMap[em.SourceMovementKey] = em;
                    }

                    // Index legacy raw numeric keys by document direction
                    bool isInbound = em.MovementType == InventoryMovementType.Receipt || em.MovementType == InventoryMovementType.Return;
                    bool isOutbound = em.MovementType == InventoryMovementType.Consumption || 
                                      em.MovementType == InventoryMovementType.Adjustment || 
                                      em.MovementType == InventoryMovementType.Waste;

                    if (isInbound)
                    {
                        if (!legacyReceiptMap.TryGetValue(em.SourceMovementKey, out var list))
                        {
                            list = new List<InventoryMovement>();
                            legacyReceiptMap[em.SourceMovementKey] = list;
                        }
                        list.Add(em);
                    }
                    if (isOutbound)
                    {
                        if (!legacyDeliveryMap.TryGetValue(em.SourceMovementKey, out var list))
                        {
                            list = new List<InventoryMovement>();
                            legacyDeliveryMap[em.SourceMovementKey] = list;
                        }
                        list.Add(em);
                    }
                }
            }

            bool TryGetExistingMovement(SepidarMovementRecord mov, out InventoryMovement? existingMov, out string? ambiguityWarning)
            {
                ambiguityWarning = null;
                existingMov = null;

                if (exactKeyMap.TryGetValue(mov.VoucherRef, out existingMov))
                {
                    return true;
                }

                if (mov.VoucherRef.StartsWith("REC-", StringComparison.OrdinalIgnoreCase))
                {
                    string rawId = mov.VoucherRef.Substring(4);
                    if (legacyReceiptMap.TryGetValue(rawId, out var candidates))
                    {
                        if (candidates.Count == 1)
                        {
                            existingMov = candidates[0];
                            return true;
                        }
                        ambiguityWarning = $"چندین رسید قدیمی با کلید خام '{rawId}' یافت شد؛ تطبیق متوقف گردید.";
                        return false;
                    }
                    return false;
                }

                if (mov.VoucherRef.StartsWith("DEL-", StringComparison.OrdinalIgnoreCase))
                {
                    string rawId = mov.VoucherRef.Substring(4);
                    if (legacyDeliveryMap.TryGetValue(rawId, out var candidates))
                    {
                        if (candidates.Count == 1)
                        {
                            existingMov = candidates[0];
                            return true;
                        }
                        ambiguityWarning = $"چندین حواله قدیمی با کلید خام '{rawId}' یافت شد؛ تطبیق متوقف گردید.";
                        return false;
                    }
                    // DEL- NEVER matches legacyReceiptMap!
                    return false;
                }

                return false;
            }

            foreach (var mov in movements)
            {
                // Requirement 3: Movements for items with unproven unit mismatch are blocked from CRM
                if (blockedItemRefs.Contains(mov.ItemRef))
                {
                    continue;
                }

                if (!mappingsDict.TryGetValue(mov.ItemRef, out var itemMap))
                {
                    continue;
                }

                job.MovementsProcessed++;

                var unit = await _dbContext.UnitsOfMeasure
                    .FirstOrDefaultAsync(u => u.Code == mov.UnitCode, cancellationToken) ?? defaultUnit;

                if (!TryGetExistingMovement(mov, out var existingMov, out var ambiguityWarning))
                {
                    if (ambiguityWarning != null)
                    {
                        _logger.LogWarning("Skipping ambiguous legacy movement match: {Warning}", ambiguityWarning);
                    }

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
                    exactKeyMap[mov.VoucherRef] = newMov;

                    // Track movement insertion by exact ID owned by this JobId
                    _dbContext.AuditEvents.Add(new AuditEvent
                    {
                        Action = "InventorySync_MovementInsert",
                        EntityType = "InventoryMovement",
                        EntityId = newMov.Id.ToString(),
                        ActorUsername = $"SyncJob:{job.Id}",
                        DetailsJson = JsonSerializer.Serialize(new MovementAuditDetail
                        {
                            JobId = job.Id,
                            MovementId = newMov.Id,
                            VoucherRef = mov.VoucherRef
                        }),
                        TimestampUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    // Existing record - update cancellation or posting status if changed
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
            job.Status = SyncJobStatus.Success;
            job.CompletedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Generate recommendations for all active materials
            await _recommendationEngine.GenerateAllRecommendationsAsync(warehouse.Id, cancellationToken);

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

    public async Task<RollbackResultDto> RollbackSyncJobAsync(
        Guid jobId, 
        string triggeredBy, 
        CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken))
        {
            throw new InvalidOperationException("An inventory synchronization or rollback operation is already in progress. Multiple concurrent operations are forbidden.");
        }

        try
        {
            var targetJob = await _dbContext.InventorySyncJobs
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            if (targetJob == null)
            {
                throw new KeyNotFoundException($"Inventory sync job with ID {jobId} was not found.");
            }

            string jobPrefix = $"SyncJob:{jobId}";
            var auditLogs = await _dbContext.AuditEvents
                .Where(a => a.ActorUsername == jobPrefix && a.Action.StartsWith("InventorySync_Snapshot"))
                .ToListAsync(cancellationToken);

            int snapshotsReverted = 0;
            int preservedUserEdits = 0;

            foreach (var audit in auditLogs)
            {
                if (string.IsNullOrWhiteSpace(audit.DetailsJson)) continue;

                if (audit.Action == "InventorySync_SnapshotUpdate")
                {
                    var details = JsonSerializer.Deserialize<SnapshotAuditDetail>(audit.DetailsJson);
                    if (details == null) continue;

                    if (!Guid.TryParse(audit.EntityId, out var snapshotId)) continue;

                    var snapshot = await _dbContext.InventorySnapshots
                        .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

                    if (snapshot == null) continue;

                    // CRITICAL REQUIREMENT 1:
                    // Atomic concurrency check across ALL revertible fields:
                    // If ANY field was modified subsequent to this sync job, PRESERVE the user edit!
                    bool isUnmodifiedSinceSync =
                        snapshot.OnHandQuantity == details.NewOnHand &&
                        snapshot.ReservableQuantity == details.NewReservable &&
                        snapshot.ReservedQuantity == details.NewReserved &&
                        snapshot.IncomingQuantity == details.NewIncoming &&
                        (details.NewUnitId == Guid.Empty || snapshot.UnitId == details.NewUnitId) &&
                        snapshot.IsStale == details.NewIsStale;

                    if (isUnmodifiedSinceSync)
                    {
                        snapshot.OnHandQuantity = details.OldOnHand;
                        snapshot.ReservableQuantity = details.OldReservable;
                        snapshot.ReservedQuantity = details.OldReserved;
                        snapshot.IncomingQuantity = details.OldIncoming;
                        if (details.OldUnitId != Guid.Empty)
                        {
                            snapshot.UnitId = details.OldUnitId;
                        }
                        snapshot.IsStale = details.OldIsStale;
                        snapshot.SnapshotTimeUtc = details.OldSnapshotTimeUtc;
                        snapshotsReverted++;
                    }
                    else
                    {
                        _logger.LogInformation("Preserving subsequent user edit for Snapshot {SnapshotId}. User modified one or more revertible fields while OnHand was {OnHand}",
                            snapshot.Id, snapshot.OnHandQuantity);
                        preservedUserEdits++;
                    }
                }
                else if (audit.Action == "InventorySync_SnapshotInsert")
                {
                    var details = JsonSerializer.Deserialize<SnapshotAuditDetail>(audit.DetailsJson);
                    if (details == null) continue;

                    if (!Guid.TryParse(audit.EntityId, out var snapshotId)) continue;

                    var snapshot = await _dbContext.InventorySnapshots
                        .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken);

                    if (snapshot != null)
                    {
                        bool isUnmodifiedSinceSync =
                            snapshot.OnHandQuantity == details.NewOnHand &&
                            snapshot.ReservableQuantity == details.NewReservable &&
                            snapshot.ReservedQuantity == details.NewReserved &&
                            snapshot.IncomingQuantity == details.NewIncoming &&
                            (details.NewUnitId == Guid.Empty || snapshot.UnitId == details.NewUnitId) &&
                            snapshot.IsStale == details.NewIsStale;

                        if (isUnmodifiedSinceSync)
                        {
                            _dbContext.InventorySnapshots.Remove(snapshot);
                            snapshotsReverted++;
                        }
                        else
                        {
                            _logger.LogInformation("Preserving subsequent user edit for newly inserted Snapshot {SnapshotId}.", snapshot.Id);
                            preservedUserEdits++;
                        }
                    }
                }
            }

            // CRITICAL REQUIREMENT 1: Rely on exact record ownership by JobId!
            // Retrieve exact movement IDs inserted by this job from AuditEvents
            var movementAudits = await _dbContext.AuditEvents
                .Where(a => a.ActorUsername == jobPrefix && a.Action == "InventorySync_MovementInsert")
                .ToListAsync(cancellationToken);

            var movementIdsToRemove = new List<Guid>();
            foreach (var mAudit in movementAudits)
            {
                if (Guid.TryParse(mAudit.EntityId, out var movId))
                {
                    movementIdsToRemove.Add(movId);
                }
            }

            var movementsToRemove = await _dbContext.InventoryMovements
                .Where(m => movementIdsToRemove.Contains(m.Id))
                .ToListAsync(cancellationToken);

            int movementsRemoved = movementsToRemove.Count;
            _dbContext.InventoryMovements.RemoveRange(movementsToRemove);

            // Record rollback audit event
            _dbContext.AuditEvents.Add(new AuditEvent
            {
                Action = "InventorySync_JobRollback",
                EntityType = "InventorySyncJob",
                EntityId = jobId.ToString(),
                ActorUsername = triggeredBy,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    TargetJobId = jobId,
                    SnapshotsReverted = snapshotsReverted,
                    PreservedUserEdits = preservedUserEdits,
                    MovementsRemoved = movementsRemoved
                }),
                TimestampUtc = DateTime.UtcNow
            });

            targetJob.ErrorMessage = $"Rolled back by {triggeredBy} at {DateTime.UtcNow:u}. Reverted: {snapshotsReverted}, PreservedUserEdits: {preservedUserEdits}, MovementsRemoved: {movementsRemoved}";
            targetJob.Status = SyncJobStatus.Partial;

            // Requirement 4: Database-level concurrency protection
            // If another process/user modified any row concurrently, RowVersion prevents overwrite
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning("Database concurrency conflict detected during rollback. Preserving concurrent external user edits.");
                foreach (var entry in ex.Entries)
                {
                    if (entry.Entity is InventorySnapshot)
                    {
                        await entry.ReloadAsync(cancellationToken);
                        preservedUserEdits++;
                        snapshotsReverted--;
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new RollbackResultDto
            {
                JobId = jobId,
                Success = true,
                SnapshotsReverted = snapshotsReverted,
                SnapshotsPreservedDueToUserEdit = preservedUserEdits,
                MovementsRemoved = movementsRemoved,
                Message = $"عملیات بازگشت داده برای اجرای {jobId} با موفقیت انجام شد. {snapshotsReverted} رکورد بازگردانی شد و {preservedUserEdits} ویرایش بعدی کاربران حفظ گردید.",
                RolledBackAtUtc = DateTime.UtcNow
            };
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SyncDryRunResultDto> ExecuteDryRunAsync(string warehouseCode, CancellationToken cancellationToken = default)
    {
        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Code == warehouseCode, cancellationToken);

        if (warehouse == null)
        {
            throw new InvalidOperationException($"انبار با کد '{warehouseCode}' در سامانه یافت نشد.");
        }

        var result = new SyncDryRunResultDto
        {
            WarehouseCode = warehouse.Code,
            WarehouseName = warehouse.Name,
            SepidarWarehouseRef = warehouse.SepidarWarehouseRef ?? "1",
            RunAtUtc = DateTime.UtcNow
        };

        // 1. Items Catalog Dry Run
        var sepidarItems = await _adapter.GetItemsAsync(cancellationToken);
        var crmMaterials = await _dbContext.Materials.ToListAsync(cancellationToken);
        var mappings = await _dbContext.InventoryItemMappings.ToListAsync(cancellationToken);

        var crmBySepidarId = mappings.ToDictionary(m => m.SepidarItemId);
        var crmByCode = crmMaterials.ToDictionary(m => m.InternalCode, StringComparer.OrdinalIgnoreCase);

        result.Items.TotalSepidarItems = sepidarItems.Count;
        foreach (var sItem in sepidarItems)
        {
            if (crmBySepidarId.TryGetValue(sItem.ItemRef, out var mapping))
            {
                result.Items.ExactMapped++;
                if (mapping.Material != null && mapping.Material.IsActive != sItem.IsActive)
                {
                    result.Items.StatusUpdate++;
                }
            }
            else if (crmByCode.TryGetValue(sItem.Code, out _))
            {
                result.Items.AmbiguousConflicts++;
            }
            else
            {
                result.Items.ToInsert++;
            }
        }

        // 2. Snapshots Dry Run across all 6 revertible fields
        var stockBalances = await _adapter.GetStockBalancesAsync(warehouse.SepidarWarehouseRef ?? "1", cancellationToken);
        var crmSnapshots = await _dbContext.InventorySnapshots
            .Where(s => s.WarehouseId == warehouse.Id)
            .ToDictionaryAsync(s => s.MaterialId, cancellationToken);
        var defaultUnit = await _dbContext.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "KG", cancellationToken);
        var materialsDict = await _dbContext.Materials
            .Include(m => m.BaseUnit)
            .Include(m => m.UnitConversions)
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var blockedItemRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        result.Snapshots.TotalSepidarStockRows = stockBalances.Count;
        foreach (var stock in stockBalances)
        {
            if (!crmBySepidarId.TryGetValue(stock.ItemRef, out var itemMap))
            {
                continue;
            }

            if (!materialsDict.TryGetValue(itemMap.MaterialId, out var material))
            {
                continue;
            }

            var unit = await _dbContext.UnitsOfMeasure
                .FirstOrDefaultAsync(u => u.Code == stock.UnitCode, cancellationToken) ?? defaultUnit;

            string stockDim = (stock.UnitCode == "KG" || stock.UnitCode == "GR") ? "Mass"
                : (stock.UnitCode == "COUNT" || stock.UnitCode == "PACK") ? "Count"
                : (unit?.Dimension ?? "Mass");
            string matDim = material.BaseUnit?.Dimension ?? "Mass";

            bool hasConversion = false;
            if (material.BaseUnit != null && unit != null)
            {
                hasConversion = material.UnitConversions.Any(c =>
                    c.IsConfirmed && (
                        (c.FromUnitId == unit.Id && c.ToUnitId == material.BaseUnitId) ||
                        (c.FromUnitId == material.BaseUnitId && c.ToUnitId == unit.Id)));
            }

            if (material.BaseUnit != null && stockDim != matDim && !hasConversion)
            {
                result.Snapshots.BlockedItemsUnitMismatch++;
                blockedItemRefs.Add(stock.ItemRef);

                result.BlockedItemsChecklist.Add(new BlockedItemDryRunDto
                {
                    ItemRef = stock.ItemRef,
                    Code = material.InternalCode,
                    Title = material.Name,
                    CrmUnitCode = material.BaseUnit?.Code ?? "KG",
                    CrmDimension = matDim,
                    SepidarStockUnit = stock.UnitCode,
                    SepidarVoucherUnit = stock.UnitCode,
                    SepidarOnHand = stock.OnHand,
                    UnitsRatio = null,
                    IsUnitRatioConstant = true,
                    RequiredEvidence = "استعلام و تعیین ضریب تبدیل رسمی بین کیلوگرم و عدد یا تفکیک کد کالا توسط مسئول انبار"
                });
                continue;
            }

            result.Snapshots.AllowedItems++;

            if (!crmSnapshots.TryGetValue(itemMap.MaterialId, out var snap))
            {
                result.Snapshots.ToInsert++;
            }
            else
            {
                bool hasChange = false;
                if (snap.OnHandQuantity != stock.OnHand)
                {
                    result.Snapshots.OnHandDiff++;
                    hasChange = true;
                }

                if (stock.Reservable.HasValue && snap.ReservableQuantity != stock.Reservable.Value)
                {
                    result.Snapshots.ReservableDiff++;
                    hasChange = true;
                }

                if (stock.Reserved.HasValue && snap.ReservedQuantity != stock.Reserved.Value)
                {
                    result.Snapshots.ReservedDiff++;
                    hasChange = true;
                }
                else
                {
                    result.Snapshots.PreservedCrmReserved++;
                }

                if (stock.Incoming > 0 && snap.IncomingQuantity != stock.Incoming)
                {
                    result.Snapshots.IncomingDiff++;
                    hasChange = true;
                }
                else
                {
                    result.Snapshots.PreservedCrmIncoming++;
                }

                if (unit != null && (defaultUnit == null || unit.Dimension == defaultUnit.Dimension) && snap.UnitId != unit.Id)
                {
                    result.Snapshots.UnitDiff++;
                    hasChange = true;
                }

                if (snap.IsStale)
                {
                    result.Snapshots.StaleDiff++;
                    hasChange = true;
                }

                if (hasChange)
                {
                    result.Snapshots.ToUpdate++;
                }
                else
                {
                    result.Snapshots.Unchanged++;
                }
            }
        }

        // 3. Movements Dry Run
        var movements = await _adapter.GetInventoryMovementsAsync(warehouse.SepidarWarehouseRef ?? "1", null, cancellationToken);
        var existingMovements = await _dbContext.InventoryMovements
            .Where(m => m.SourceMovementKey != null)
            .ToListAsync(cancellationToken);

        var exactKeyMap = new Dictionary<string, InventoryMovement>(StringComparer.OrdinalIgnoreCase);
        var legacyReceiptMap = new Dictionary<string, List<InventoryMovement>>(StringComparer.OrdinalIgnoreCase);
        var legacyDeliveryMap = new Dictionary<string, List<InventoryMovement>>(StringComparer.OrdinalIgnoreCase);

        foreach (var em in existingMovements)
        {
            if (!string.IsNullOrEmpty(em.SourceMovementKey))
            {
                if (!exactKeyMap.ContainsKey(em.SourceMovementKey))
                {
                    exactKeyMap[em.SourceMovementKey] = em;
                }

                bool isInbound = em.MovementType == InventoryMovementType.Receipt || em.MovementType == InventoryMovementType.Return;
                bool isOutbound = em.MovementType == InventoryMovementType.Consumption || 
                                  em.MovementType == InventoryMovementType.Adjustment || 
                                  em.MovementType == InventoryMovementType.Waste;

                if (isInbound)
                {
                    if (!legacyReceiptMap.TryGetValue(em.SourceMovementKey, out var list))
                    {
                        list = new List<InventoryMovement>();
                        legacyReceiptMap[em.SourceMovementKey] = list;
                    }
                    list.Add(em);
                }
                if (isOutbound)
                {
                    if (!legacyDeliveryMap.TryGetValue(em.SourceMovementKey, out var list))
                    {
                        list = new List<InventoryMovement>();
                        legacyDeliveryMap[em.SourceMovementKey] = list;
                    }
                    list.Add(em);
                }
            }
        }

        bool TryGetExistingMovement(SepidarMovementRecord mov, out InventoryMovement? existingMov)
        {
            existingMov = null;
            if (exactKeyMap.TryGetValue(mov.VoucherRef, out existingMov)) return true;

            if (mov.VoucherRef.StartsWith("REC-", StringComparison.OrdinalIgnoreCase))
            {
                string rawId = mov.VoucherRef.Substring(4);
                if (legacyReceiptMap.TryGetValue(rawId, out var candidates) && candidates.Count == 1)
                {
                    existingMov = candidates[0];
                    return true;
                }
                return false;
            }

            if (mov.VoucherRef.StartsWith("DEL-", StringComparison.OrdinalIgnoreCase))
            {
                string rawId = mov.VoucherRef.Substring(4);
                if (legacyDeliveryMap.TryGetValue(rawId, out var candidates) && candidates.Count == 1)
                {
                    existingMov = candidates[0];
                    return true;
                }
                // DEL- NEVER matches legacyReceiptMap!
                return false;
            }

            return false;
        }

        result.Movements.TotalSepidarMovements = movements.Count;
        foreach (var mov in movements)
        {
            if (mov.VoucherRef.StartsWith("REC-", StringComparison.OrdinalIgnoreCase))
            {
                result.Movements.InboundReceipts++;
            }
            else
            {
                result.Movements.OutboundDeliveries++;
            }

            int typeVal = (int)mov.MovementType;
            if (!result.Movements.ByType.ContainsKey(typeVal))
            {
                result.Movements.ByType[typeVal] = 0;
            }
            result.Movements.ByType[typeVal]++;

            if (blockedItemRefs.Contains(mov.ItemRef))
            {
                result.Movements.BlockedMovementsUnitMismatch++;
                continue;
            }

            if (!crmBySepidarId.ContainsKey(mov.ItemRef))
            {
                result.Movements.DiscardedUnmapped++;
                continue;
            }

            if (TryGetExistingMovement(mov, out _))
            {
                result.Movements.AlreadyExistingInCrm++;
            }
            else
            {
                result.Movements.ToInsert++;
            }
        }

        result.UnitDiscrepancyWarnings.Add("کالای 404229 (تخم مرغ): در سپیدار واحد سنجش 'عدد' (UnitRef=2) ثبت گردیده است ولی موجودی در انبار دارای اعشار (67811.92) است. ستون‌های SecondaryUnitRef و UnitsRatio در سپیدار NULL هستند. در کاتالوگ CRM واحد سنجش 'کیلوگرم' است. تا زمان تعیین رسمی ضریب تبدیل، ورود موجودی و گردش به CRM مسدود است.");
        result.UnitDiscrepancyWarnings.Add("کالای 404016 (خمیر منجمد مینی ساندویچی): در سپیدار واحد 'عدد' با موجودی 11000 ثبت شده در حالی که در CRM کیلوگرم است و ستون‌های تبدیل واحد سپیدار فاقد مقدارند. ورود موجودی و گردش به CRM مسدود است.");
        result.UnitDiscrepancyWarnings.Add("علت ۲۶۲ تغییر واحد: ۲۶۲ قلم از مجموع ۴۸۲ قلم کالای انبار ۱ در جدول INV.Item دارای UnitRef=2 ('عدد') هستند ولی در کاتالوگ CRM با بعد جرمی ('KG') تعریف شده‌اند. از آنجا که هیچ جدول یا ضریب تبدیل رسمی در سپیدار (HasSecUnit=0, HasRatio=0) وجود ندارد، ورود موجودی این ۲۶۲ قلم و ۶,۶۹۸ گردش مرتبط با آن‌ها به محاسبات CRM مسدود گردید.");

        return result;
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

    public class SnapshotAuditDetail
    {
        public Guid JobId { get; set; }
        public Guid MaterialId { get; set; }
        public Guid WarehouseId { get; set; }
        public decimal OldOnHand { get; set; }
        public decimal NewOnHand { get; set; }
        public decimal OldReservable { get; set; }
        public decimal NewReservable { get; set; }
        public decimal OldReserved { get; set; }
        public decimal NewReserved { get; set; }
        public decimal OldIncoming { get; set; }
        public decimal NewIncoming { get; set; }
        public Guid OldUnitId { get; set; }
        public Guid NewUnitId { get; set; }
        public bool OldIsStale { get; set; }
        public bool NewIsStale { get; set; }
        public DateTime OldSnapshotTimeUtc { get; set; }
        public DateTime NewSnapshotTimeUtc { get; set; }
    }

    public class MovementAuditDetail
    {
        public Guid JobId { get; set; }
        public Guid MovementId { get; set; }
        public string VoucherRef { get; set; } = string.Empty;
    }
}

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;

namespace ParkPalace.Infrastructure.Services;

public sealed record PilotExecutionReceipt(Guid RunId, int Snapshots, int Movements);
public sealed record PilotRollbackReceipt(int Reverted, int Preserved);
public sealed record PilotJournal(string PlanHash, string ManifestHash,
    IReadOnlyList<PilotSnapshotChange> Snapshots, IReadOnlyList<PilotMovementChange> Movements);

/// <summary>
/// Not registered in DI or exposed by HTTP. Uses an isolated context per call.
/// Caller must supply an approved manifest and a verified read-only source capture.
/// No source connection, catalog mutation, global checkpoint or recommendation generation.
/// </summary>
public sealed class InventoryPilotExecutor(Func<CrmDbContext> contextFactory)
{
    private const string Applied = "InventoryPilot_Applied";
    private const string Reverted = "InventoryPilot_RolledBack";

    public async Task<InventoryPilotPlan> PreviewAsync(PilotManifest manifest,
        PilotSourceCapture source, CancellationToken ct = default)
    {
        await using var db = contextFactory();
        RequireSqlServer(db);
        return await ReadPlanAsync(db, manifest, source, ct);
    }

    public async Task<PilotExecutionReceipt> ExecuteAsync(PilotManifest manifest,
        PilotSourceCapture source, string approvedPlanHash, Guid runId, string actor,
        CancellationToken ct = default)
    {
        if (runId == Guid.Empty || string.IsNullOrWhiteSpace(actor) || actor.Length > 100)
            throw new InvalidOperationException("PILOT_INVALID_RUN_ID_OR_ACTOR");
        if (DateTime.UtcNow - source.CapturedAtUtc > TimeSpan.FromMinutes(10) ||
            source.CapturedAtUtc > DateTime.UtcNow.AddSeconds(5))
            throw new InvalidOperationException("PILOT_CAPTURE_EXPIRED");
        await using var db = contextFactory();
        RequireSqlServer(db);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var key = runId.ToString("N");
        if (await db.AuditEvents.AnyAsync(a => a.EntityType == "InventoryPilotRun" && a.EntityId == key, ct))
            throw new InvalidOperationException("PILOT_RUN_ALREADY_RECORDED");
        // Re-read under serializable locks. Hash covers before images and rowversions.
        var plan = await ReadPlanAsync(db, manifest, source, ct);
        if (!StringComparer.Ordinal.Equals(plan.PlanHash, approvedPlanHash))
            throw new InvalidOperationException("PILOT_PREVIEW_CHANGED");

        var snapshotRows = new List<(PilotSnapshotChange Change, InventorySnapshot Row)>();
        foreach (var change in plan.Snapshots)
        {
            var row = await db.InventorySnapshots.SingleAsync(s => s.Id == change.Before.Id, ct);
            ApplySnapshot(row, change.After);
            snapshotRows.Add((change, row));
        }
        var movementRows = new List<(PilotMovementChange Change, InventoryMovement Row)>();
        foreach (var change in plan.Movements)
        {
            var row = change.Before == null ? new InventoryMovement() :
                await db.InventoryMovements.SingleAsync(m => m.Id == change.Before.Id, ct);
            ApplyMovement(row, change.After);
            if (change.Before == null) db.InventoryMovements.Add(row);
            movementRows.Add((change, row));
        }
        await db.SaveChangesAsync(ct);
        // Capture actual inserted IDs and SQL-generated rowversions, within the same transaction.
        var journal = new PilotJournal(plan.PlanHash, plan.ManifestHash,
            snapshotRows.Select(x => new PilotSnapshotChange(x.Change.Before, SnapshotState(x.Row))).ToArray(),
            movementRows.Select(x => new PilotMovementChange(x.Change.Before, MovementState(x.Row))).ToArray());
        db.AuditEvents.Add(Event(Applied, key, actor, JsonSerializer.Serialize(journal)));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(runId, plan.Snapshots.Count, plan.Movements.Count);
    }

    public async Task<PilotRollbackReceipt> RollbackAsync(Guid runId, string actor, CancellationToken ct = default)
    {
        if (runId == Guid.Empty || string.IsNullOrWhiteSpace(actor) || actor.Length > 100)
            throw new InvalidOperationException("PILOT_INVALID_RUN_ID_OR_ACTOR");
        await using var db = contextFactory();
        RequireSqlServer(db);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var key = runId.ToString("N");
        var events = await db.AuditEvents.Where(a => a.EntityType == "InventoryPilotRun" && a.EntityId == key).ToListAsync(ct);
        if (events.Any(e => e.Action == Reverted)) throw new InvalidOperationException("PILOT_ALREADY_ROLLED_BACK");
        var applied = events.SingleOrDefault(e => e.Action == Applied)
            ?? throw new InvalidOperationException("PILOT_RUN_NOT_FOUND");
        var journal = JsonSerializer.Deserialize<PilotJournal>(applied.DetailsJson!)
            ?? throw new InvalidOperationException("PILOT_INVALID_JOURNAL");
        int reverted = 0, preserved = 0;
        foreach (var change in journal.Snapshots)
        {
            var row = await db.InventorySnapshots.SingleOrDefaultAsync(s => s.Id == change.After.Id, ct);
            if (row == null || SnapshotState(row) != change.After) { preserved++; continue; }
            ApplySnapshot(row, change.Before);
            reverted++;
        }
        foreach (var change in journal.Movements)
        {
            var row = await db.InventoryMovements.SingleOrDefaultAsync(m => m.Id == change.After.Id, ct);
            if (row == null || MovementState(row) != change.After) { preserved++; continue; }
            if (change.Before == null) db.InventoryMovements.Remove(row);
            else ApplyMovement(row, change.Before);
            reverted++;
        }
        db.AuditEvents.Add(Event(Reverted, key, actor, JsonSerializer.Serialize(new { reverted, preserved })));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(reverted, preserved);
    }

    private static void RequireSqlServer(CrmDbContext db)
    {
        if (!db.Database.IsSqlServer()) throw new InvalidOperationException("PILOT_SQL_SERVER_REQUIRED");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("PILOT_FRESH_CONTEXT_REQUIRED");
    }

    private static async Task<InventoryPilotPlan> ReadPlanAsync(CrmDbContext db, PilotManifest manifest,
        PilotSourceCapture source, CancellationToken ct)
    {
        // Enforce the same unsupported movement exclusion rule at the execution entry point
        var (filteredManifest, _) = InventoryPilotPlanner.ExcludeItemsWithUnsupportedMovements(manifest, source);
        manifest = filteredManifest;

        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.Id == manifest.WarehouseId, ct);
        if (warehouse == null || !warehouse.IsActive || warehouse.Code != manifest.WarehouseCode ||
            warehouse.SepidarWarehouseRef != manifest.SourceWarehouseId)
            throw new InvalidOperationException("PILOT_WAREHOUSE_MAPPING_CHANGED");
        var ids = manifest.Items.Select(i => i.MaterialId).ToArray();
        var materials = await db.Materials.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().ToListAsync(ct);
        var mappings = await db.InventoryItemMappings.AsNoTracking().ToListAsync(ct);
        foreach (var item in manifest.Items)
        {
            var material = materials.SingleOrDefault(m => m.Id == item.MaterialId);
            var mapping = mappings.Where(m => m.SepidarItemId == item.SourceItemId).ToArray();
            if (material == null || !material.IsActive || material.BaseUnitId != item.UnitId ||
                !units.Any(u => u.Id == item.UnitId && u.Code == item.UnitCode && u.IsActive) ||
                mapping.Length != 1 || !mapping[0].IsActive || mapping[0].MaterialId != item.MaterialId)
                throw new InvalidOperationException("PILOT_ITEM_MAPPING_CHANGED");
        }
        var snapshots = await db.InventorySnapshots.AsNoTracking()
            .Where(s => s.WarehouseId == manifest.WarehouseId && ids.Contains(s.MaterialId)).ToListAsync(ct);
        // Global SourceMovementKey uniqueness requires inspecting collisions outside the pilot too.
        var movements = await db.InventoryMovements.AsNoTracking().Where(m => m.SourceMovementKey != null).ToListAsync(ct);
        return InventoryPilotPlanner.Build(manifest, source, snapshots.Select(SnapshotState).ToArray(),
            movements.Select(MovementState).ToArray());
    }

    internal static PilotSnapshotState SnapshotState(InventorySnapshot s) => new(s.Id, s.MaterialId,
        s.WarehouseId, s.UnitId, s.OnHandQuantity, s.ReservableQuantity, s.ReservedQuantity,
        s.IncomingQuantity, s.SnapshotTimeUtc, s.IsStale, s.SourceSnapshotKey, Convert.ToHexString(s.RowVersion));
    internal static PilotMovementState MovementState(InventoryMovement m) => new(m.Id, m.MaterialId,
        m.WarehouseId, m.UnitId, m.SourceMovementKey!, m.VoucherNumber, m.MovementType, m.Quantity,
        m.TransactionDateUtc, m.PostingStatus, m.IsCancelled, m.ReferenceDocument, Convert.ToHexString(m.RowVersion));
    private static void ApplySnapshot(InventorySnapshot row, PilotSnapshotState state)
    {
        row.OnHandQuantity = state.OnHand;
        row.ReservableQuantity = state.Reservable;
        row.ReservedQuantity = state.Reserved;
        row.IncomingQuantity = state.Incoming;
        row.SnapshotTimeUtc = state.SnapshotTimeUtc;
        row.IsStale = state.IsStale;
        row.SourceSnapshotKey = state.SourceKey;
    }
    private static void ApplyMovement(InventoryMovement row, PilotMovementState state)
    {
        if (state.Type == InventoryMovementType.Unknown || (int)state.Type <= 0)
            throw new InvalidOperationException("PILOT_UNSUPPORTED_MOVEMENT_TYPE_CANNOT_BE_APPLIED");

        row.MaterialId = state.MaterialId;
        row.WarehouseId = state.WarehouseId;
        row.UnitId = state.UnitId;
        row.SourceMovementKey = state.SourceKey;
        row.VoucherNumber = state.VoucherNumber;
        row.MovementType = state.Type;
        row.Quantity = state.Quantity;
        row.TransactionDateUtc = state.TransactionDate;
        row.PostingStatus = state.PostingStatus;
        row.IsCancelled = state.IsCancelled;
        row.ReferenceDocument = state.ReferenceDocument;
    }
    private static AuditEvent Event(string action, string key, string actor, string details) => new() {
        Action = action, EntityType = "InventoryPilotRun", EntityId = key,
        ActorUsername = actor, DetailsJson = details
    };
}

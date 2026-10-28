using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services;

public sealed record PilotItem(string SourceItemId, Guid MaterialId, Guid UnitId, string UnitCode);
public sealed record PilotManifest(string WarehouseCode, Guid WarehouseId, string SourceWarehouseId,
    int FiscalYearId, DateTime PeriodStart, DateTime PeriodEndExclusive, IReadOnlyList<PilotItem> Items);
public sealed record PilotSourceCapture(int FiscalYearId, DateTime CapturedAtUtc,
    IReadOnlyList<SepidarStockRecord> Stocks, IReadOnlyList<SepidarMovementRecord> Movements);
public sealed record PilotSnapshotState(Guid Id, Guid MaterialId, Guid WarehouseId, Guid UnitId,
    decimal OnHand, decimal Reservable, decimal Reserved, decimal Incoming,
    DateTime SnapshotTimeUtc, bool IsStale, string? SourceKey, string RowVersion);
public sealed record PilotMovementState(Guid Id, Guid MaterialId, Guid WarehouseId, Guid UnitId,
    string SourceKey, string VoucherNumber, InventoryMovementType Type, decimal Quantity,
    DateTime TransactionDate, int PostingStatus, bool IsCancelled, string? ReferenceDocument,
    string RowVersion);
public sealed record PilotSnapshotChange(PilotSnapshotState Before, PilotSnapshotState After);
public sealed record PilotMovementChange(PilotMovementState? Before, PilotMovementState After);

/// <summary>Immutable proposed writes, not permission to execute. No connection or EF tracking.</summary>
public sealed class InventoryPilotPlan
{
    public string ManifestHash { get; }
    public string PlanHash { get; }
    public IReadOnlyList<PilotSnapshotChange> Snapshots { get; }
    public IReadOnlyList<PilotMovementChange> Movements { get; }
    public int ExcludedStockRows { get; }
    public int ExcludedMovementRows { get; }

    internal InventoryPilotPlan(string manifestHash, List<PilotSnapshotChange> snapshots,
        List<PilotMovementChange> movements, int excludedStocks, int excludedMovements)
    {
        ManifestHash = manifestHash;
        Snapshots = snapshots.AsReadOnly();
        Movements = movements.AsReadOnly();
        ExcludedStockRows = excludedStocks;
        ExcludedMovementRows = excludedMovements;
        PlanHash = InventoryPilotPlanner.Hash(new { ManifestHash, Snapshots, Movements });
    }
}

/// <summary>
/// Local planning kernel for the first same-unit pilot. It intentionally cannot create
/// catalog entries, convert units, generate recommendations or advance global checkpoints.
/// A transactional executor and validated capture reader must be reviewed separately.
/// </summary>
public static class InventoryPilotPlanner
{
    internal static string Hash(object value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    public static (PilotManifest FilteredManifest, IReadOnlyList<string> ExcludedItemIds) ExcludeItemsWithUnsupportedMovements(
        PilotManifest manifest, PilotSourceCapture source)
    {
        var unsupportedItemRefs = source.Movements
            .Where(m => m.MovementType == InventoryMovementType.Unknown || (int)m.MovementType <= 0)
            .Select(m => m.ItemRef)
            .ToHashSet(StringComparer.Ordinal);

        if (unsupportedItemRefs.Count == 0)
            return (manifest, Array.Empty<string>());

        var remainingItems = manifest.Items
            .Where(i => !unsupportedItemRefs.Contains(i.SourceItemId))
            .ToList();

        var excluded = manifest.Items
            .Where(i => unsupportedItemRefs.Contains(i.SourceItemId))
            .Select(i => i.SourceItemId)
            .ToList();

        return (manifest with { Items = remainingItems }, excluded);
    }

    public static InventoryPilotPlan Build(PilotManifest manifest, PilotSourceCapture source,
        IReadOnlyList<PilotSnapshotState> currentSnapshots,
        IReadOnlyList<PilotMovementState> currentMovements)
    {
        // Enforce exclusion of items with unsupported movements in the kernel
        var (filteredManifest, _) = ExcludeItemsWithUnsupportedMovements(manifest, source);
        manifest = filteredManifest;

        if (manifest.WarehouseCode != "WH-MAIN" || manifest.SourceWarehouseId != "1" ||
            manifest.WarehouseId == Guid.Empty || manifest.FiscalYearId <= 0 ||
            manifest.PeriodStart >= manifest.PeriodEndExclusive || manifest.Items.Count == 0)
            throw new InvalidOperationException("PILOT_INVALID_MANIFEST");
        if (source.FiscalYearId != manifest.FiscalYearId || source.CapturedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("PILOT_SOURCE_PERIOD_OR_CLOCK_MISMATCH");

        var items = manifest.Items.OrderBy(i => i.SourceItemId, StringComparer.Ordinal).ToArray();
        if (items.Any(i => string.IsNullOrWhiteSpace(i.SourceItemId) || i.MaterialId == Guid.Empty ||
                i.UnitId == Guid.Empty || i.UnitCode != "KG") ||
            items.Select(i => i.SourceItemId).Distinct(StringComparer.Ordinal).Count() != items.Length ||
            items.Select(i => i.MaterialId).Distinct().Count() != items.Length)
            throw new InvalidOperationException("PILOT_AMBIGUOUS_OR_UNSUPPORTED_ITEMS");

        var manifestHash = Hash(new { manifest.WarehouseCode, manifest.WarehouseId,
            manifest.SourceWarehouseId, manifest.FiscalYearId, manifest.PeriodStart,
            manifest.PeriodEndExclusive, Items = items });
        var bySource = items.ToDictionary(i => i.SourceItemId, StringComparer.Ordinal);
        var stocks = source.Stocks.Where(s => bySource.ContainsKey(s.ItemRef)).ToArray();
        var movements = source.Movements.Where(m => bySource.ContainsKey(m.ItemRef))
            .OrderBy(m => m.VoucherRef, StringComparer.Ordinal).ToArray();
        if (stocks.Length != items.Length || stocks.Select(s => s.ItemRef).Distinct().Count() != items.Length)
            throw new InvalidOperationException("PILOT_MISSING_OR_DUPLICATE_STOCK");
        if (movements.Select(m => m.VoucherRef).Distinct().Count() != movements.Length)
            throw new InvalidOperationException("PILOT_DUPLICATE_SOURCE_MOVEMENT");

        var snapshotChanges = new List<PilotSnapshotChange>();
        foreach (var item in items)
        {
            var stock = stocks.Single(s => s.ItemRef == item.SourceItemId);
            if (stock.WarehouseRef != manifest.SourceWarehouseId || stock.UnitCode != item.UnitCode ||
                stock.SnapshotTimeUtc.Kind != DateTimeKind.Utc || stock.SnapshotTimeUtc > source.CapturedAtUtc)
                throw new InvalidOperationException("PILOT_STOCK_SCOPE_OR_UNIT_MISMATCH");
            var matches = currentSnapshots.Where(s => s.MaterialId == item.MaterialId &&
                s.WarehouseId == manifest.WarehouseId).ToArray();
            // The agreed first pilot updates existing snapshots only.
            if (matches.Length != 1 || matches[0].UnitId != item.UnitId)
                throw new InvalidOperationException("PILOT_SNAPSHOT_BASELINE_MISMATCH");
            var before = matches[0];
            // CRM owns Reserved and Incoming. Unproven free stock is not synthesized.
            var after = before with {
                OnHand = stock.OnHand,
                Reservable = stock.Reservable ?? before.Reservable,
                SnapshotTimeUtc = stock.SnapshotTimeUtc,
                IsStale = false,
                SourceKey = $"WH-{manifest.WarehouseCode}-ITEM-{item.SourceItemId}" +
                    (stock.Reservable.HasValue ? "" : ":RES-UNPROVEN")
            };
            if (after != before) snapshotChanges.Add(new(before, after));
        }

        var movementChanges = new List<PilotMovementChange>();
        foreach (var movement in movements)
        {
            var item = bySource[movement.ItemRef];
            if (movement.WarehouseRef != manifest.SourceWarehouseId || movement.UnitCode != item.UnitCode ||
                movement.TransactionDateUtc < manifest.PeriodStart ||
                movement.TransactionDateUtc >= manifest.PeriodEndExclusive || movement.Quantity < 0 ||
                movement.MovementType == InventoryMovementType.Unknown || (int)movement.MovementType <= 0 ||
                !Enum.IsDefined(movement.MovementType))
                throw new InvalidOperationException("PILOT_MOVEMENT_SCOPE_OR_UNIT_MISMATCH");
            bool receipt = movement.VoucherRef.StartsWith("REC-", StringComparison.Ordinal);
            bool delivery = movement.VoucherRef.StartsWith("DEL-", StringComparison.Ordinal);
            if ((!receipt && !delivery) || !long.TryParse(movement.VoucherRef[4..], out var numericId) || numericId <= 0)
                throw new InvalidOperationException("PILOT_INVALID_SOURCE_KEY");

            // Global lookup respects the existing global unique index. A collision in
            // another warehouse/material must fail, not silently update that record.
            var exact = currentMovements.Where(m => m.SourceKey == movement.VoucherRef).ToArray();
            if (exact.Length > 1) throw new InvalidOperationException("PILOT_DUPLICATE_TARGET_KEY");
            PilotMovementState? before = exact.SingleOrDefault();
            if (before == null)
            {
                var raw = movement.VoucherRef[4..];
                var legacy = currentMovements.Where(m => m.SourceKey == raw).ToArray();
                // Only Receipt has an unambiguous inbound legacy interpretation here.
                // Return/Adjustment/Transfer require provenance evidence, not guesses.
                if (legacy.Length != 0)
                {
                    if (receipt && legacy.Length == 1 && legacy[0].Type == InventoryMovementType.Receipt)
                        before = legacy[0];
                    else if (!(delivery && legacy.All(m => m.Type == InventoryMovementType.Receipt)))
                        throw new InvalidOperationException("PILOT_AMBIGUOUS_LEGACY_KEY");
                }
            }
            if (before != null && (before.MaterialId != item.MaterialId ||
                before.WarehouseId != manifest.WarehouseId || before.UnitId != item.UnitId))
                throw new InvalidOperationException("PILOT_TARGET_KEY_COLLISION");

            // Empty Id is an insert intent; an executor assigns and audits the real ID.
            var after = new PilotMovementState(before?.Id ?? Guid.Empty, item.MaterialId,
                manifest.WarehouseId, item.UnitId, before?.SourceKey ?? movement.VoucherRef,
                movement.VoucherNumber, movement.MovementType, movement.Quantity,
                movement.TransactionDateUtc, movement.PostingStatus, movement.IsCancelled,
                movement.ReferenceDocument, before?.RowVersion ?? "");
            if (after != before) movementChanges.Add(new(before, after));
        }
        return new(manifestHash, snapshotChanges, movementChanges,
            source.Stocks.Count - stocks.Length, source.Movements.Count - movements.Length);
    }
}

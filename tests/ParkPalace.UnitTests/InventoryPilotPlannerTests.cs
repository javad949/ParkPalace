using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class InventoryPilotPlannerTests
{
    private static readonly Guid Warehouse = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Material = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid Unit = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTime Now = new(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc);
    private static PilotManifest Manifest => new("WH-MAIN", Warehouse, "1", 10,
        new(2026, 3, 21), new(2027, 3, 21), new[] { new PilotItem("1", Material, Unit, "KG") });
    private static PilotSnapshotState Snapshot => new(Guid.NewGuid(), Material, Warehouse, Unit,
        100, 70, 30, 50, Now.AddHours(-1), false, "old-source", "version-1");
    private static SepidarStockRecord Stock => new("1", "1", 100, null, 999, 999, "KG", Now);
    private static SepidarMovementRecord Movement(string key = "REC-5", string item = "1") =>
        new(key, "5", item, "1", key.StartsWith("REC") ? InventoryMovementType.Receipt :
            InventoryMovementType.Consumption, 12, "KG", Now.AddDays(-1), 4, false, null);
    private static PilotSourceCapture Source(params SepidarMovementRecord[] movements) =>
        new(10, Now, new[] { Stock }, movements);
    private static PilotMovementState Existing(string key = "5") =>
        new(Guid.NewGuid(), Material, Warehouse, Unit, key, "5", InventoryMovementType.Receipt,
            12, Now.AddDays(-1), 4, false, null, "version-1");

    [Fact]
    public void FiltersOutsideManifest_AndCountsProvenanceAndTimeChangesWithoutChangingCrmOwnership()
    {
        var baseline = Snapshot;
        var source = Source(Movement(), Movement("DEL-8", "outside")) with {
            Stocks = new[] { Stock, Stock with { ItemRef = "outside", UnitCode = "COUNT" } }
        };
        var plan = InventoryPilotPlanner.Build(Manifest, source, new[] { baseline }, Array.Empty<PilotMovementState>());
        Assert.Equal(1, plan.ExcludedStockRows);
        Assert.Equal(1, plan.ExcludedMovementRows);
        var change = Assert.Single(plan.Snapshots);
        Assert.Equal(baseline.OnHand, change.After.OnHand);
        Assert.Equal(30, change.After.Reserved);
        Assert.Equal(50, change.After.Incoming);
        Assert.Equal(70, change.After.Reservable);
        Assert.EndsWith(":RES-UNPROVEN", change.After.SourceKey);
        Assert.Equal(Now, change.After.SnapshotTimeUtc);
        Assert.Equal("old-source", baseline.SourceKey); // input not mutated
        Assert.Single(plan.Movements);
    }

    [Fact]
    public void ReceiptAndDeliveryWithSameNumericId_AreNotMerged()
    {
        var plan = InventoryPilotPlanner.Build(Manifest, Source(Movement(), Movement("DEL-5")),
            new[] { Snapshot }, new[] { Existing() });
        var insert = Assert.Single(plan.Movements);
        Assert.Null(insert.Before);
        Assert.Equal("DEL-5", insert.After.SourceKey);
    }

    [Fact]
    public void TargetKeyInAnotherWarehouse_IsAConflict()
    {
        var existing = Existing("REC-5") with { WarehouseId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() => InventoryPilotPlanner.Build(Manifest,
            Source(Movement()), new[] { Snapshot }, new[] { existing }));
    }

    [Fact]
    public void AmbiguousLegacyTransfer_StopsInsteadOfGuessing()
    {
        var existing = Existing() with { Type = InventoryMovementType.Transfer };
        Assert.Throws<InvalidOperationException>(() => InventoryPilotPlanner.Build(Manifest,
            Source(Movement()), new[] { Snapshot }, new[] { existing }));
    }

    [Fact]
    public void MissingStockOrChangedUnits_StopTheWholePlan()
    {
        foreach (var stocks in new IReadOnlyList<SepidarStockRecord>[] {
            Array.Empty<SepidarStockRecord>(), new[] { Stock with { UnitCode = "GR" } }, new[] { Stock, Stock } })
            Assert.Throws<InvalidOperationException>(() => InventoryPilotPlanner.Build(Manifest,
                Source() with { Stocks = stocks }, new[] { Snapshot }, Array.Empty<PilotMovementState>()));
    }

    [Fact]
    public void WrongFiscalYearOrMovementOutsidePeriod_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => InventoryPilotPlanner.Build(Manifest,
            Source() with { FiscalYearId = 9 }, new[] { Snapshot }, Array.Empty<PilotMovementState>()));
        Assert.Throws<InvalidOperationException>(() => InventoryPilotPlanner.Build(Manifest,
            Source(Movement() with { TransactionDateUtc = new DateTime(2025, 1, 1) }),
            new[] { Snapshot }, Array.Empty<PilotMovementState>()));
    }

    [Fact]
    public void HashIsDeterministic_AndBindsConcurrentChangesToCrmOwnedFields()
    {
        var baseline = Snapshot;
        var first = InventoryPilotPlanner.Build(Manifest, Source(Movement()), new[] { baseline }, Array.Empty<PilotMovementState>());
        var repeat = InventoryPilotPlanner.Build(Manifest, Source(Movement()), new[] { baseline }, Array.Empty<PilotMovementState>());
        Assert.Equal(first.PlanHash, repeat.PlanHash);
        var changed = InventoryPilotPlanner.Build(Manifest, Source(Movement()),
            new[] { baseline with { Reserved = 45, RowVersion = "version-2" } }, Array.Empty<PilotMovementState>());
        Assert.NotEqual(first.PlanHash, changed.PlanHash);
        Assert.Equal(first.ManifestHash, changed.ManifestHash);
    }

    [Fact]
    public void AlreadyAppliedCapture_HasNoWrites()
    {
        var first = InventoryPilotPlanner.Build(Manifest, Source(Movement()), new[] { Snapshot }, Array.Empty<PilotMovementState>());
        var second = InventoryPilotPlanner.Build(Manifest, Source(Movement()),
            first.Snapshots.Select(s => s.After).ToArray(),
            first.Movements.Select(m => m.After with { Id = Guid.NewGuid(), RowVersion = "saved" }).ToArray());
        Assert.Empty(second.Snapshots);
        Assert.Empty(second.Movements);
    }
}

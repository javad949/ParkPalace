using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.DTOs;

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public int TotalItemsCount { get; set; }
    public int CriticalItemsCount { get; set; }
}

public class InventoryItemDto
{
    public Guid MaterialId { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public decimal ReservableQuantity { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinStock { get; set; }
    public decimal DailyConsumption7d { get; set; }
    public decimal DailyConsumption30d { get; set; }
    public bool IsBelowSafetyStock { get; set; }
    public bool IsStale { get; set; }
    public bool IsUnitMismatch { get; set; }
    public string? StockStatus { get; set; }
    public DateTime? LastSyncUtc { get; set; }
}

public class InventoryItemDetailDto : InventoryItemDto
{
    public string? Brand { get; set; }
    public string? Specifications { get; set; }
    public string? Description { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal IncomingQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public string? SepidarItemCode { get; set; }
    public string? SepidarItemId { get; set; }
    public ReplenishmentPolicyDto? ReplenishmentPolicy { get; set; }
    public List<SupplierMaterialSummaryDto> Suppliers { get; set; } = new();
}

public class InventoryMovementDto
{
    public Guid Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public int MovementTypeValue { get; set; }
    public decimal Quantity { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public DateTime TransactionDateUtc { get; set; }
    public int PostingStatus { get; set; }
    public bool IsCancelled { get; set; }
    public string? ReferenceDocument { get; set; }
}

public class InventoryConsumptionDto
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public decimal Consumption7dTotal { get; set; }
    public decimal Consumption7dAverage { get; set; }
    public decimal Consumption14dTotal { get; set; }
    public decimal Consumption14dAverage { get; set; }
    public decimal Consumption30dTotal { get; set; }
    public decimal Consumption30dAverage { get; set; }
    public int ActiveDays30d { get; set; }
    public int ZeroDays30d { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
}

public class ReplenishmentPolicyDto
{
    public Guid MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int ReviewPeriodDays { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinOrderQuantity { get; set; }
    public decimal? PackageMultiple { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public string? Notes { get; set; }
}

public class UpdateReplenishmentPolicyRequest
{
    public int ReviewPeriodDays { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinOrderQuantity { get; set; }
    public decimal? PackageMultiple { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? Notes { get; set; }
}

public class PurchaseRecommendationDto
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public decimal CurrentOnHand { get; set; }
    public decimal? ReservableOnHand { get; set; }
    public decimal IncomingOrders { get; set; }
    public decimal AverageDailyConsumption { get; set; }
    public int LeadTimeDays { get; set; }
    public int ReviewPeriodDays { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal TargetStock { get; set; }
    public decimal InventoryPosition { get; set; }
    public decimal RawSuggestedQuantity { get; set; }
    public decimal RoundedSuggestedQuantity { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RecommendationExplanation { get; set; }
    public string? DataWarning { get; set; }
    public bool IsDefinitive { get; set; } = true;
    public bool RequiresManualReview { get; set; } = false;
    public string? ReserveStatusDescription { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

public class SyncStatusDto
{
    public bool IsSyncing { get; set; }
    public DateTime? LastSuccessfulSyncUtc { get; set; }
    public bool IsStale { get; set; }
    public int StaleWarningHours { get; set; } = 24;
    public SyncJobDto? LatestJob { get; set; }
}

public class SyncJobDto
{
    public Guid Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int ItemsProcessed { get; set; }
    public int MovementsProcessed { get; set; }
    public int SnapshotsUpdated { get; set; }
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
}

public class RollbackResultDto
{
    public Guid JobId { get; set; }
    public bool Success { get; set; }
    public int SnapshotsReverted { get; set; }
    public int SnapshotsPreservedDueToUserEdit { get; set; }
    public int MovementsRemoved { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RolledBackAtUtc { get; set; }
}

public class SyncDryRunResultDto
{
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string SepidarWarehouseRef { get; set; } = string.Empty;
    public int FiscalYearId { get; set; }
    public string FiscalYearTitle { get; set; } = string.Empty;
    public DateTime RunAtUtc { get; set; } = DateTime.UtcNow;

    public ItemCatalogDryRunDto Items { get; set; } = new();
    public SnapshotsDryRunDto Snapshots { get; set; } = new();
    public MovementsDryRunDto Movements { get; set; } = new();
    public List<BlockedItemDryRunDto> BlockedItemsChecklist { get; set; } = new();
    public List<string> UnitDiscrepancyWarnings { get; set; } = new();
}

public class ItemCatalogDryRunDto
{
    public int TotalSepidarItems { get; set; }
    public int ExactMapped { get; set; }
    public int ToInsert { get; set; }
    public int StatusUpdate { get; set; }
    public int AmbiguousConflicts { get; set; }
}

public class SnapshotsDryRunDto
{
    public int TotalSepidarStockRows { get; set; }
    public int AllowedItems { get; set; }
    public int BlockedItemsUnitMismatch { get; set; }
    public int ToInsert { get; set; }
    public int ToUpdate { get; set; }
    public int Unchanged { get; set; }
    public int OnHandDiff { get; set; }
    public int ReservableDiff { get; set; }
    public int ReservedDiff { get; set; }
    public int IncomingDiff { get; set; }
    public int UnitDiff { get; set; }
    public int StaleDiff { get; set; }
    public int PreservedCrmReserved { get; set; }
    public int PreservedCrmIncoming { get; set; }
}

public class MovementsDryRunDto
{
    public int TotalSepidarMovements { get; set; }
    public int InboundReceipts { get; set; }
    public int OutboundDeliveries { get; set; }
    public int AlreadyExistingInCrm { get; set; }
    public int ToInsert { get; set; }
    public int BlockedMovementsUnitMismatch { get; set; }
    public int DiscardedUnmapped { get; set; }
    public Dictionary<int, int> ByType { get; set; } = new();
}

public class BlockedItemDryRunDto
{
    public string ItemRef { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CrmUnitCode { get; set; } = string.Empty;
    public string CrmDimension { get; set; } = string.Empty;
    public string SepidarStockUnit { get; set; } = string.Empty;
    public string SepidarVoucherUnit { get; set; } = string.Empty;
    public decimal SepidarOnHand { get; set; }
    public double? UnitsRatio { get; set; }
    public bool? IsUnitRatioConstant { get; set; }
    public string RequiredEvidence { get; set; } = string.Empty;
}


using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Common;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Data;

public class CrmDbContext : DbContext, ICrmDbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IntegrationSyncRun> IntegrationSyncRuns => Set<IntegrationSyncRun>();

    // Phase 3: Catalog & Materials
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialUnitConversion> MaterialUnitConversions => Set<MaterialUnitConversion>();
    public DbSet<ExternalItemMapping> ExternalItemMappings => Set<ExternalItemMapping>();

    // Phase 3: Suppliers & Parties
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyContact> PartyContacts => Set<PartyContact>();
    public DbSet<PartyPhone> PartyPhones => Set<PartyPhone>();
    public DbSet<PartyAddress> PartyAddresses => Set<PartyAddress>();
    public DbSet<SupplierMaterial> SupplierMaterials => Set<SupplierMaterial>();
    public DbSet<SupplierPriceHistory> SupplierPriceHistories => Set<SupplierPriceHistory>();
    public DbSet<PartyDocument> PartyDocuments => Set<PartyDocument>();

    // Phase 4: Inventory & Warehouse
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<InventoryItemMapping> InventoryItemMappings => Set<InventoryItemMapping>();
    public DbSet<InventorySnapshot> InventorySnapshots => Set<InventorySnapshot>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<InventorySyncJob> InventorySyncJobs => Set<InventorySyncJob>();
    public DbSet<InventorySyncCheckpoint> InventorySyncCheckpoints => Set<InventorySyncCheckpoint>();
    public DbSet<InventorySyncError> InventorySyncErrors => Set<InventorySyncError>();
    public DbSet<MaterialConsumptionSummary> MaterialConsumptionSummaries => Set<MaterialConsumptionSummary>();
    public DbSet<ReplenishmentPolicy> ReplenishmentPolicies => Set<ReplenishmentPolicy>();
    public DbSet<PurchaseRecommendation> PurchaseRecommendations => Set<PurchaseRecommendation>();

    // Phase 5: Procurement, Orders, Receipts, Invoices & 3-Way Matching
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<PurchaseRequestLine> PurchaseRequestLines => Set<PurchaseRequestLine>();
    public DbSet<RequestForQuotation> RequestForQuotations => Set<RequestForQuotation>();
    public DbSet<RfqSupplier> RfqSuppliers => Set<RfqSupplier>();
    public DbSet<SupplierQuotation> SupplierQuotations => Set<SupplierQuotation>();
    public DbSet<SupplierQuotationLine> SupplierQuotationLines => Set<SupplierQuotationLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderAudit> PurchaseOrderAudits => Set<PurchaseOrderAudit>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines => Set<SupplierInvoiceLine>();
    public DbSet<ThreeWayMatchResult> ThreeWayMatchResults => Set<ThreeWayMatchResult>();
    public DbSet<ThreeWayMatchLineVariance> ThreeWayMatchLineVariances => Set<ThreeWayMatchLineVariance>();
    public DbSet<SepidarFinancialReconciliation> SepidarFinancialReconciliations => Set<SepidarFinancialReconciliation>();

    // Phase 6: Telephony, Calls & PBX
    public DbSet<CallRecord> CallRecords => Set<CallRecord>();
    public DbSet<CallNote> CallNotes => Set<CallNote>();
    public DbSet<CallAudit> CallAudits => Set<CallAudit>();
    public DbSet<UserExtension> UserExtensions => Set<UserExtension>();
    public DbSet<TelephonyGroup> TelephonyGroups => Set<TelephonyGroup>();
    public DbSet<TelephonyGroupMember> TelephonyGroupMembers => Set<TelephonyGroupMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

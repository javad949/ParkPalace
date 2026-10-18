using Microsoft.EntityFrameworkCore;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.Interfaces;

public interface ICrmDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<IntegrationSyncRun> IntegrationSyncRuns { get; }

    // Phase 3: Catalog & Materials
    DbSet<Category> Categories { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<Material> Materials { get; }
    DbSet<MaterialUnitConversion> MaterialUnitConversions { get; }
    DbSet<ExternalItemMapping> ExternalItemMappings { get; }

    // Phase 3: Suppliers & Parties
    DbSet<Party> Parties { get; }
    DbSet<PartyContact> PartyContacts { get; }
    DbSet<PartyPhone> PartyPhones { get; }
    DbSet<PartyAddress> PartyAddresses { get; }
    DbSet<SupplierMaterial> SupplierMaterials { get; }
    DbSet<SupplierPriceHistory> SupplierPriceHistories { get; }
    DbSet<PartyDocument> PartyDocuments { get; }

    // Phase 4: Inventory & Warehouse
    DbSet<Warehouse> Warehouses { get; }
    DbSet<InventoryItemMapping> InventoryItemMappings { get; }
    DbSet<InventorySnapshot> InventorySnapshots { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<InventorySyncJob> InventorySyncJobs { get; }
    DbSet<InventorySyncCheckpoint> InventorySyncCheckpoints { get; }
    DbSet<InventorySyncError> InventorySyncErrors { get; }
    DbSet<MaterialConsumptionSummary> MaterialConsumptionSummaries { get; }
    DbSet<ReplenishmentPolicy> ReplenishmentPolicies { get; }
    DbSet<PurchaseRecommendation> PurchaseRecommendations { get; }

    // Phase 5: Procurement, Orders, Receipts, Invoices & 3-Way Matching
    DbSet<PurchaseRequest> PurchaseRequests { get; }
    DbSet<PurchaseRequestLine> PurchaseRequestLines { get; }
    DbSet<RequestForQuotation> RequestForQuotations { get; }
    DbSet<RfqSupplier> RfqSuppliers { get; }
    DbSet<SupplierQuotation> SupplierQuotations { get; }
    DbSet<SupplierQuotationLine> SupplierQuotationLines { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<PurchaseOrderAudit> PurchaseOrderAudits { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }
    DbSet<SupplierInvoice> SupplierInvoices { get; }
    DbSet<SupplierInvoiceLine> SupplierInvoiceLines { get; }
    DbSet<ThreeWayMatchResult> ThreeWayMatchResults { get; }
    DbSet<ThreeWayMatchLineVariance> ThreeWayMatchLineVariances { get; }
    DbSet<SepidarFinancialReconciliation> SepidarFinancialReconciliations { get; }

    // Phase 6: Telephony, Calls & PBX
    DbSet<CallRecord> CallRecords { get; }
    DbSet<CallNote> CallNotes { get; }
    DbSet<CallAudit> CallAudits { get; }
    DbSet<UserExtension> UserExtensions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

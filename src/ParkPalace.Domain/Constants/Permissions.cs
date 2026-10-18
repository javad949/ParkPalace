namespace ParkPalace.Domain.Constants;

public static class Permissions
{
    // Materials / Catalog
    public const string MaterialsView = "Materials.View";
    public const string MaterialsManage = "Materials.Manage";

    // Suppliers
    public const string SuppliersView = "Suppliers.View";
    public const string SuppliersManage = "Suppliers.Manage";
    public const string SuppliersPricesView = "Suppliers.PricesView";
    public const string SuppliersPricesManage = "Suppliers.PricesManage";

    // Procurement & Orders (Phase 2 & Phase 5)
    public const string OrdersView = "Orders.View";
    public const string OrdersCreate = "Orders.Create";
    public const string OrdersIssue = "Orders.Issue";
    public const string OrdersCancel = "Orders.Cancel";

    // RFQ & Quotations (Phase 5)
    public const string QuotesView = "Quotes.View";
    public const string QuotesManage = "Quotes.Manage";

    // Warehouse & Stock / Receipts (Phase 4 & Phase 5)
    public const string WarehouseView = "Warehouse.View";
    public const string WarehouseReceive = "Warehouse.Receive";
    public const string ReceiptsView = "Warehouse.ReceiptsView";
    public const string ReceiptsManage = "Warehouse.ReceiptsManage";
    public const string InventoryView = "Inventory.View";
    public const string InventoryManage = "Inventory.Manage";
    public const string InventorySync = "Inventory.Sync";

    // Recommendations & Policies (Phase 4)
    public const string RecommendationsView = "Procurement.RecommendationsView";
    public const string PoliciesManage = "Procurement.PoliciesManage";

    // Finance, Invoices & 3-Way Matching (Phase 5)
    public const string FinanceView = "Finance.View";
    public const string FinanceReconcile = "Finance.Reconcile";
    public const string InvoicesView = "Invoices.View";
    public const string InvoicesManage = "Invoices.Manage";
    public const string MatchingView = "Finance.MatchingView";
    public const string MatchingResolve = "Finance.MatchingResolve";

    // Telephony / Calls (Phase 6)
    public const string CallsView = "Calls.View";
    public const string CallsManage = "Calls.Manage";
    public const string CallsSupervise = "Calls.Supervise";
    public const string CallsRecordingsListen = "Calls.RecordingsListen";
    public const string CallsConfig = "Calls.Config";

    // Reports & Dashboards (Phase 7)
    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string ReportsProcurement = "Reports.Procurement";
    public const string ReportsManager = "Reports.Manager";
    public const string ReportsWarehouse = "Reports.Warehouse";
    public const string ReportsFinance = "Reports.Finance";
    public const string ReportsAdmin = "Reports.Admin";

    // Administration
    public const string AdminUsers = "Admin.Users";
    public const string AdminSettings = "Admin.Settings";
    public const string AdminAudit = "Admin.Audit";

    public static readonly string[] All = {
        MaterialsView, MaterialsManage,
        SuppliersView, SuppliersManage, SuppliersPricesView, SuppliersPricesManage,
        OrdersView, OrdersCreate, OrdersIssue, OrdersCancel,
        QuotesView, QuotesManage,
        WarehouseView, WarehouseReceive, ReceiptsView, ReceiptsManage,
        InventoryView, InventoryManage, InventorySync,
        RecommendationsView, PoliciesManage,
        FinanceView, FinanceReconcile, InvoicesView, InvoicesManage, MatchingView, MatchingResolve,
        CallsView, CallsManage, CallsSupervise, CallsRecordingsListen, CallsConfig,
        ReportsView, ReportsExport, ReportsProcurement, ReportsManager, ReportsWarehouse, ReportsFinance, ReportsAdmin,
        AdminUsers, AdminSettings, AdminAudit
    };
}


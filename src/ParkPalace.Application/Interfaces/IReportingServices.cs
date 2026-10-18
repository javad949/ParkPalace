using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IReportingService
{
    Task<ProcurementSummaryReportDto> GetProcurementSummaryAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<List<PurchaseOrderTrendItemDto>> GetPurchaseOrderTrendsAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<List<SupplierPerformanceReportItemDto>> GetSupplierPerformanceAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<List<MaterialConsumptionReportItemDto>> GetMaterialConsumptionAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<InventoryHealthReportDto> GetInventoryHealthAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<FinanceReconciliationReportDto> GetFinanceReconciliationReportAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<TelephonyActivityReportDto> GetTelephonyActivityReportAsync(ReportFilterParams filters, CancellationToken cancellationToken = default);
    Task<IntegrationHealthReportDto> GetIntegrationHealthAsync(CancellationToken cancellationToken = default);
    Task<RoleDashboardDto> GetRoleDashboardAsync(string roleName, ReportFilterParams filters, CancellationToken cancellationToken = default);
}

public interface IExportService
{
    Task<ExportResultDto> ExportReportAsync(ExportRequestDto request, string requestedByUsername, CancellationToken cancellationToken = default);
}

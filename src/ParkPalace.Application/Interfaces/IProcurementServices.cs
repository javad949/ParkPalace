using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetOrdersAsync(int page, int pageSize, string? status, string? search, CancellationToken ct = default);
    Task<PurchaseOrderDto?> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreateOrderAsync(CreatePurchaseOrderRequest req, string username, CancellationToken ct = default);
    Task<PurchaseOrderDto> IssueOrderAsync(Guid id, string username, string concurrencyToken, bool manualReviewConfirmed = false, CancellationToken ct = default);
    Task<PurchaseOrderDto> CancelOrderAsync(Guid id, CancelPurchaseOrderRequest req, string username, CancellationToken ct = default);
}

public interface IGoodsReceiptService
{
    Task<PagedResult<GoodsReceiptDto>> GetReceiptsAsync(int page, int pageSize, Guid? poId, CancellationToken ct = default);
    Task<GoodsReceiptDto?> GetReceiptByIdAsync(Guid id, CancellationToken ct = default);
    Task<GoodsReceiptDto> CreateReceiptAsync(CreateGoodsReceiptRequest req, string username, CancellationToken ct = default);
}

public interface ISupplierInvoiceService
{
    Task<PagedResult<SupplierInvoiceDto>> GetInvoicesAsync(int page, int pageSize, Guid? supplierId, string? status, CancellationToken ct = default);
    Task<SupplierInvoiceDto?> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default);
    Task<SupplierInvoiceDto> CreateInvoiceAsync(CreateSupplierInvoiceRequest req, string username, CancellationToken ct = default);
}

public interface IThreeWayMatchingEngine
{
    Task<ThreeWayMatchResultDto> EvaluateMatchAsync(Guid invoiceId, string username, decimal tolerancePercent = 1.0m, CancellationToken ct = default);
    Task<ThreeWayMatchResultDto?> GetMatchResultByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<List<ThreeWayMatchResultDto>> GetExceptionQueueAsync(CancellationToken ct = default);
    Task<ThreeWayMatchResultDto> ResolveMatchAsync(Guid matchResultId, ResolveMatchingRequest req, string username, CancellationToken ct = default);
}

public interface ISepidarFinancialAdapter
{
    string AdapterName { get; }
    Task<List<SepidarCandidateInvoiceDto>> GetCandidateInvoicesAsync(DateTime? sinceDateUtc, CancellationToken ct = default);
}

public interface ISepidarReconciliationService
{
    Task<List<SepidarCandidateInvoiceDto>> GetCandidatesForInvoiceAsync(Guid supplierInvoiceId, CancellationToken ct = default);
    Task<SepidarFinancialReconciliationDto> LinkInvoiceAsync(LinkSepidarInvoiceRequest req, string username, CancellationToken ct = default);
    Task<SepidarFinancialReconciliationDto> UnlinkInvoiceAsync(Guid reconciliationId, string auditNotes, string username, CancellationToken ct = default);
    Task<List<SepidarFinancialReconciliationDto>> GetReconciliationHistoryAsync(CancellationToken ct = default);
}

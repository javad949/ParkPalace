using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/finance/invoices")]
public class SupplierInvoicesController : ControllerBase
{
    private readonly ISupplierInvoiceService _invoiceService;
    private readonly ICurrentUserService _currentUserService;

    public SupplierInvoicesController(ISupplierInvoiceService invoiceService, ICurrentUserService currentUserService)
    {
        _invoiceService = invoiceService;
        _currentUserService = currentUserService;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierInvoiceDto>>> GetInvoices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.InvoicesView) && !HasPermission(Permissions.FinanceView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز مشاهده فاکتورهای تأمین‌کنندگان را ندارید.",
                Status = 403
            });
        }

        var result = await _invoiceService.GetInvoicesAsync(page, pageSize, supplierId, status, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierInvoiceDto>> GetInvoiceById(Guid id, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.InvoicesView) && !HasPermission(Permissions.FinanceView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var invoice = await _invoiceService.GetInvoiceByIdAsync(id, ct);
        if (invoice == null)
        {
            return NotFound(new ProblemDetails { Title = "فاکتور تأمین‌کننده یافت نشد", Status = 404 });
        }

        return Ok(invoice);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierInvoiceDto>> CreateInvoice([FromBody] CreateSupplierInvoiceRequest req, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.InvoicesManage) && !HasPermission(Permissions.FinanceReconcile))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز ثبت و ویرایش فاکتورهای تأمین‌کنندگان را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "FinanceUser";
            var created = await _invoiceService.CreateInvoiceAsync(req, username, ct);
            return CreatedAtAction(nameof(GetInvoiceById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطای اعتبارسنجی فاکتور", Detail = ex.Message, Status = 400 });
        }
    }
}

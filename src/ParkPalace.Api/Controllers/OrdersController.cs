using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/procurement/orders")]
public class OrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _orderService;
    private readonly ICurrentUserService _currentUserService;

    public OrdersController(IPurchaseOrderService orderService, ICurrentUserService currentUserService)
    {
        _orderService = orderService;
        _currentUserService = currentUserService;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PurchaseOrderDto>>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.OrdersView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز مشاهده سفارشات خرید را ندارید.",
                Status = 403
            });
        }

        var result = await _orderService.GetOrdersAsync(page, pageSize, status, search, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetOrderById(Guid id, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.OrdersView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var po = await _orderService.GetOrderByIdAsync(id, ct);
        if (po == null)
        {
            return NotFound(new ProblemDetails { Title = "سفارش خرید یافت نشد", Status = 404 });
        }

        return Ok(po);
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDto>> CreateOrder([FromBody] CreatePurchaseOrderRequest req, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.OrdersCreate))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز ایجاد سفارش خرید را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "User";
            var created = await _orderService.CreateOrderAsync(req, username, ct);
            return CreatedAtAction(nameof(GetOrderById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطای اعتبارسنجی سفارش خرید", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<PurchaseOrderDto>> IssueOrder(
        Guid id,
        [FromBody] IssueOrderPayload payload,
        CancellationToken ct = default)
    {
        // Direct PO issuance by Procurement without manager approval (SRS FR-05)
        if (!HasPermission(Permissions.OrdersIssue))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز صدور مستقیم سفارش خرید را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "User";
            var issued = await _orderService.IssueOrderAsync(id, username, payload.ConcurrencyToken, payload.ManualReviewConfirmed, ct);
            return Ok(issued);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            return Conflict(new ProblemDetails { Title = "تداخل همزمانی", Detail = ex.Message, Status = 409 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در صدور سفارش", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PurchaseOrderDto>> CancelOrder(
        Guid id,
        [FromBody] CancelPurchaseOrderRequest req,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.OrdersCancel) && !HasPermission(Permissions.OrdersCreate))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز ابطال سفارش خرید را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "User";
            var cancelled = await _orderService.CancelOrderAsync(id, req, username, ct);
            return Ok(cancelled);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            return Conflict(new ProblemDetails { Title = "تداخل همزمانی", Detail = ex.Message, Status = 409 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در ابطال سفارش", Detail = ex.Message, Status = 400 });
        }
    }
}

public class IssueOrderPayload
{
    public string ConcurrencyToken { get; set; } = string.Empty;
    public bool ManualReviewConfirmed { get; set; } = false;
}

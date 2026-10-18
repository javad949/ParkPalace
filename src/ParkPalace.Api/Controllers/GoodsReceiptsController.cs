using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/warehouse/receipts")]
public class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _receiptService;
    private readonly ICurrentUserService _currentUserService;

    public GoodsReceiptsController(IGoodsReceiptService receiptService, ICurrentUserService currentUserService)
    {
        _receiptService = receiptService;
        _currentUserService = currentUserService;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GoodsReceiptDto>>> GetReceipts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? poId = null,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.ReceiptsView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز مشاهده رسیدهای انبار را ندارید.",
                Status = 403
            });
        }

        var result = await _receiptService.GetReceiptsAsync(page, pageSize, poId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GoodsReceiptDto>> GetReceiptById(Guid id, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.ReceiptsView) && !HasPermission(Permissions.WarehouseView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var receipt = await _receiptService.GetReceiptByIdAsync(id, ct);
        if (receipt == null)
        {
            return NotFound(new ProblemDetails { Title = "رسید انبار یافت نشد", Status = 404 });
        }

        return Ok(receipt);
    }

    [HttpPost]
    public async Task<ActionResult<GoodsReceiptDto>> CreateReceipt([FromBody] CreateGoodsReceiptRequest req, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.WarehouseReceive) && !HasPermission(Permissions.ReceiptsManage))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز ثبت رسید انبار و تحویل کالا را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "WarehouseUser";
            var created = await _receiptService.CreateReceiptAsync(req, username, ct);
            return CreatedAtAction(nameof(GetReceiptById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطای اعتبارسنجی رسید انبار", Detail = ex.Message, Status = 400 });
        }
    }
}

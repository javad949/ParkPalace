using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/finance/sepidar-reconciliation")]
public class SepidarReconciliationController : ControllerBase
{
    private readonly ISepidarReconciliationService _reconciliationService;
    private readonly ICurrentUserService _currentUserService;

    public SepidarReconciliationController(ISepidarReconciliationService reconciliationService, ICurrentUserService currentUserService)
    {
        _reconciliationService = reconciliationService;
        _currentUserService = currentUserService;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpGet("candidates/{supplierInvoiceId:guid}")]
    public async Task<ActionResult<List<SepidarCandidateInvoiceDto>>> GetCandidates(Guid supplierInvoiceId, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.FinanceView) && !HasPermission(Permissions.FinanceReconcile))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز بررسی اسناد رسمی سپیدار را ندارید.",
                Status = 403
            });
        }

        try
        {
            var candidates = await _reconciliationService.GetCandidatesForInvoiceAsync(supplierInvoiceId, ct);
            return Ok(candidates);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در یافتن اسناد متناظر سپیدار", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPost("link")]
    public async Task<ActionResult<SepidarFinancialReconciliationDto>> LinkInvoice([FromBody] LinkSepidarInvoiceRequest req, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.FinanceReconcile))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز تطبیق و اتصال قطعی سند سپیدار را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "FinanceUser";
            var result = await _reconciliationService.LinkInvoiceAsync(req, username, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در تطبیق سند سپیدار", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPost("unlink/{reconciliationId:guid}")]
    public async Task<ActionResult<SepidarFinancialReconciliationDto>> UnlinkInvoice(
        Guid reconciliationId,
        [FromBody] UnlinkPayload payload,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.FinanceReconcile))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز لغو اتصال سند سپیدار را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "FinanceUser";
            var result = await _reconciliationService.UnlinkInvoiceAsync(reconciliationId, payload.AuditNotes ?? "لغو اتصال توسط کاربر", username, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در لغو اتصال سند سپیدار", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<SepidarFinancialReconciliationDto>>> GetHistory(CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.FinanceView) && !HasPermission(Permissions.FinanceReconcile))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var history = await _reconciliationService.GetReconciliationHistoryAsync(ct);
        return Ok(history);
    }
}

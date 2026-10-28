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
[Route("api/v1/finance/matching")]
public class ThreeWayMatchingController : ControllerBase
{
    private readonly IThreeWayMatchingEngine _matchingEngine;
    private readonly ICurrentUserService _currentUserService;

    public ThreeWayMatchingController(IThreeWayMatchingEngine matchingEngine, ICurrentUserService currentUserService)
    {
        _matchingEngine = matchingEngine;
        _currentUserService = currentUserService;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) || 
               _currentUserService.Permissions.Contains(permission);
    }

    [HttpPost("evaluate/{invoiceId:guid}")]
    public async Task<ActionResult<ThreeWayMatchResultDto>> EvaluateMatching(
        Guid invoiceId,
        [FromQuery] decimal tolerance = 1.0m,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.MatchingView) && !HasPermission(Permissions.FinanceView))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز ارزیابی تطبیق سه‌طرفه را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "FinanceUser";
            var result = await _matchingEngine.EvaluateMatchAsync(invoiceId, username, tolerance, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در ارزیابی تطبیق سه‌طرفه", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpGet("invoice/{invoiceId:guid}")]
    public async Task<ActionResult<ThreeWayMatchResultDto>> GetMatchResult(Guid invoiceId, CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.MatchingView) && !HasPermission(Permissions.FinanceView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var result = await _matchingEngine.GetMatchResultByInvoiceIdAsync(invoiceId, ct);
        if (result == null)
        {
            return NotFound(new ProblemDetails { Title = "نتیجه تطبیق برای این فاکتور یافت نشد", Status = 404 });
        }

        return Ok(result);
    }

    [HttpGet("exceptions")]
    public async Task<ActionResult<List<ThreeWayMatchResultDto>>> GetExceptionQueue(CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.MatchingView) && !HasPermission(Permissions.FinanceView))
        {
            return StatusCode(403, new ProblemDetails { Title = "دسترسی غیرمجاز", Status = 403 });
        }

        var list = await _matchingEngine.GetExceptionQueueAsync(ct);
        return Ok(list);
    }

    [HttpPost("resolve/{matchResultId:guid}")]
    public async Task<ActionResult<ThreeWayMatchResultDto>> ResolveMatching(
        Guid matchResultId,
        [FromBody] ResolveMatchingRequest req,
        CancellationToken ct = default)
    {
        if (!HasPermission(Permissions.MatchingResolve) && !HasPermission(Permissions.FinanceReconcile))
        {
            return StatusCode(403, new ProblemDetails
            {
                Title = "دسترسی غیرمجاز",
                Detail = "شما مجوز رفع و تایید مغایرت‌های تطبیق سه‌طرفه را ندارید.",
                Status = 403
            });
        }

        try
        {
            var username = _currentUserService.Username ?? "FinanceUser";
            var result = await _matchingEngine.ResolveMatchAsync(matchResultId, req, username, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "خطا در ثبت تصمیم رفع مغایرت", Detail = ex.Message, Status = 400 });
        }
    }
}

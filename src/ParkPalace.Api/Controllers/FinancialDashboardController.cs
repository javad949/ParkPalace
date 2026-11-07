using Microsoft.AspNetCore.Mvc;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Financial;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/finance/dashboard")]
[Route("v1/finance/dashboard")]
public class FinancialDashboardController : ControllerBase
{
    private readonly IFinancialDashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;

    public FinancialDashboardController(IFinancialDashboardService dashboard, ICurrentUserService currentUser)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<FinancialDashboardDto>> Get(
        [FromQuery] int? fiscalYear,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] PartyCategory? partyCategory,
        [FromQuery] string? search,
        [FromQuery] string? followUpOwner,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized(Denied(401, "عدم احراز هویت", "دسترسی به داشبورد مالی نیازمند ورود است."));
        if (!FinancialAccess.CanViewTotals(_currentUser))
            return StatusCode(403, Denied(403, "عدم دسترسی", "مانده/جمع طرف‌حساب فقط با Finance.PartyLedgerView باز است؛ Admin، Finance.View یا Finance.Reconcile به‌تنهایی کافی نیست."));

        var result = await _dashboard.GetDashboardAsync(new FinancialDashboardQuery
        {
            FiscalYear = fiscalYear,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            PartyCategory = partyCategory,
            Search = search,
            FollowUpOwner = followUpOwner
        }, ct);
        return Ok(result);
    }

    [HttpGet("parties/{partyId:guid}/ledger")]
    public async Task<ActionResult<PartyLedgerDto>> Ledger(
        Guid partyId,
        [FromQuery] bool includeDocuments = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            return Unauthorized(Denied(401, "عدم احراز هویت", "دسترسی به پرونده مالی نیازمند ورود است."));
        if (!FinancialAccess.CanViewTotals(_currentUser))
            return StatusCode(403, Denied(403, "عدم دسترسی", "مشاهده پرونده مالی طرف‌حساب مجاز نیست."));

        var documents = includeDocuments && FinancialAccess.CanViewDocumentLines(_currentUser);
        var ledger = await _dashboard.GetPartyLedgerAsync(partyId, documents, page, pageSize, ct);
        if (ledger == null)
            return NotFound(Denied(404, "طرف‌حساب یافت نشد", "این شناسه در CRM نیست."));

        if (includeDocuments && !FinancialAccess.CanViewDocumentLines(_currentUser))
        {
            ledger.DocumentsAuthorized = false;
            ledger.LinesStatus = FinancialFigureStatus.Blocked;
            ledger.LinesReason = "ریز اسناد فقط با مجوز Finance.PartyLedgerView باز می‌شود. Finance.View یا Finance.Reconcile به‌تنهایی کافی نیست.";
            ledger.Lines.Clear();
            ledger.TotalLineCount = null;
        }

        return Ok(ledger);
    }

    private static ProblemDetails Denied(int status, string title, string detail) => new()
    {
        Title = title,
        Detail = detail,
        Status = status
    };
}

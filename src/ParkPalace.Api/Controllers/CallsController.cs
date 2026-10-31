using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/calls")]
[Route("v1/calls")]
public class CallsController : ControllerBase
{
    private readonly ICallManagerService _callManagerService;
    private readonly IRecordingService _recordingService;
    private readonly ITelephonyAdapter _telephonyAdapter;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICrmDbContext _dbContext;
    private readonly ITelephonyAccessControlService _accessControlService;
    private readonly ILogger<CallsController> _logger;

    public CallsController(
        ICallManagerService callManagerService,
        IRecordingService recordingService,
        ITelephonyAdapter telephonyAdapter,
        ICurrentUserService currentUserService,
        ICrmDbContext dbContext,
        ITelephonyAccessControlService accessControlService,
        ILogger<CallsController> logger)
    {
        _callManagerService = callManagerService;
        _recordingService = recordingService;
        _telephonyAdapter = telephonyAdapter;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
        _accessControlService = accessControlService;
        _logger = logger;
    }

    private async Task<TelephonyAccessScope> GetCurrentScopeAsync()
    {
        return await _accessControlService.GetUserAccessScopeAsync(_currentUserService.UserId, _currentUserService.Username);
    }

    private bool HasPermission(string permission)
    {
        if (string.Equals(_currentUserService.ActiveRole, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            return true;

        if (_currentUserService.Roles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase))
            return true;

        if (_currentUserService.AvailableRoles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase))
            return true;

        return _currentUserService.HasPermission(permission);
    }

    private ObjectResult Forbidden(string detail = "شما مجوز دسترسی به این بخش را ندارید.")
    {
        return StatusCode(403, new ProblemDetails
        {
            Title = "دسترسی غیرمجاز",
            Detail = detail,
            Status = 403
        });
    }

    [HttpGet]
    public async Task<ActionResult<List<CallRecordDto>>> GetCalls(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? extension = null,
        [FromQuery] CallDirection? direction = null,
        [FromQuery] CallStatus? status = null,
        [FromQuery] bool? unknownOnly = null)
    {
        if (!HasPermission(Permissions.CallsView))
        {
            return Forbidden("شما مجوز مشاهده تاریخچه تماس‌ها را ندارید.");
        }

        var scope = await GetCurrentScopeAsync();
        
        string? filterExt = extension;
        if (!scope.HasGlobalAccess)
        {
            if (scope.AllowedExtensions.Count == 0)
            {
                return Ok(new List<CallRecordDto>());
            }

            if (!string.IsNullOrWhiteSpace(filterExt))
            {
                if (!scope.AllowedExtensions.Contains(filterExt.Trim()))
                {
                    return Forbidden("شما تنها به تماس‌های داخلی خود و اعضای گروه تحت نظارت دسترسی دارید.");
                }
            }
        }

        var calls = await _callManagerService.GetCallsAsync(
            skip, 
            take, 
            filterExt, 
            direction, 
            status, 
            unknownOnly, 
            scope.HasGlobalAccess ? null : scope.AllowedExtensions.ToList());

        return Ok(calls);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CallRecordDto>> GetCallById(Guid id)
    {
        if (!HasPermission(Permissions.CallsView))
        {
            return Forbidden();
        }

        var call = await _callManagerService.GetCallByIdAsync(id);
        if (call == null)
        {
            return NotFound(new { message = $"Call with ID {id} not found." });
        }

        var scope = await GetCurrentScopeAsync();
        if (!_accessControlService.CanAccessCall(scope, call.Extension, call.CallerNumber, call.DestinationNumber))
        {
            return Forbidden("شما تنها به تماس‌های داخلی خود و اعضای گروه تحت نظارت دسترسی دارید.");
        }

        return Ok(call);
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<CallNoteDto>> AddNote(Guid id, [FromBody] CreateCallNoteRequest request)
    {
        if (!HasPermission(Permissions.CallsManage))
        {
            return Forbidden("شما مجوز ثبت یادداشت و پیگیری تماس را ندارید.");
        }

        if (string.IsNullOrWhiteSpace(request.NoteText))
        {
            return BadRequest(new { message = "NoteText cannot be empty." });
        }

        var username = !string.IsNullOrWhiteSpace(_currentUserService.Username) 
            ? _currentUserService.Username 
            : (User?.Identity?.Name ?? "CRM\\Administrator");

        var note = await _callManagerService.AddCallNoteAsync(id, request, username, _currentUserService.UserId);
        return Ok(note);
    }

    [HttpPost("{id:guid}/classify")]
    public async Task<ActionResult<CallRecordDto>> ClassifyCall(Guid id, [FromBody] ClassifyCallRequest request)
    {
        if (!HasPermission(Permissions.SuppliersManage) && !HasPermission(Permissions.CallsManage))
        {
            return Forbidden("شما مجوز انتساب شماره ناشناس به تأمین‌کننده را ندارید.");
        }

        try
        {
            var username = !string.IsNullOrWhiteSpace(_currentUserService.Username) 
                ? _currentUserService.Username 
                : (User?.Identity?.Name ?? "CRM\\Administrator");
            var updated = await _callManagerService.ClassifyUnknownCallAsync(id, request, username);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            var inner = dbEx.InnerException?.Message ?? dbEx.Message;
            return StatusCode(500, new { message = "خطای پایگاه داده در ثبت اطلاعات", details = inner });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, details = ex.InnerException?.Message });
        }
    }

    [HttpGet("match")]
    public async Task<ActionResult<List<PartyMatchDto>>> MatchParties([FromQuery] string phone)
    {
        if (!HasPermission(Permissions.CallsView) && !HasPermission(Permissions.SuppliersView))
        {
            return Forbidden();
        }

        var matches = await _callManagerService.MatchPartiesByNumberAsync(phone);
        return Ok(matches);
    }

    [HttpGet("unknown")]
    public async Task<ActionResult<List<CallRecordDto>>> GetUnknownCalls()
    {
        if (!HasPermission(Permissions.CallsView))
        {
            return Forbidden();
        }

        var unknownCalls = await _callManagerService.GetUnknownCallsAsync();
        return Ok(unknownCalls);
    }

    [HttpGet("follow-ups")]
    public async Task<ActionResult<List<CallNoteDto>>> GetPendingFollowUps()
    {
        if (!HasPermission(Permissions.CallsView))
        {
            return Forbidden();
        }

        var followUps = await _callManagerService.GetPendingFollowUpsAsync();
        return Ok(followUps);
    }

    [HttpPut("follow-ups/{noteId:guid}/status")]
    public async Task<ActionResult<CallNoteDto>> UpdateFollowUpStatus(Guid noteId, [FromQuery] FollowUpStatus status)
    {
        if (!HasPermission(Permissions.CallsManage))
        {
            return Forbidden();
        }

        var username = !string.IsNullOrWhiteSpace(_currentUserService.Username) 
            ? _currentUserService.Username 
            : (User?.Identity?.Name ?? "CRM\\Administrator");

        var note = await _callManagerService.UpdateFollowUpStatusAsync(noteId, status, username);
        return Ok(note);
    }

    [HttpGet("{id:guid}/recording")]
    public async Task<IActionResult> StreamRecording(Guid id)
    {
        if (!HasPermission(Permissions.CallsRecordingsListen) && !HasPermission(Permissions.CallsView))
        {
            _logger.LogWarning("Unauthorized recording playback attempt by {Username} for Call {CallId}", _currentUserService.Username, id);
            return Forbidden("شما مجوز شنود مکالمات ضبط‌شده را ندارید.");
        }

        var call = await _callManagerService.GetCallByIdAsync(id);
        if (call == null)
        {
            return NotFound(new { message = "Call not found." });
        }

        var scope = await GetCurrentScopeAsync();
        if (!_accessControlService.CanAccessCall(scope, call.Extension, call.CallerNumber, call.DestinationNumber))
        {
            _logger.LogWarning("Forbidden recording playback attempt by {Username} for Call {CallId} (Ext: {Ext}, Caller: {Caller}, Dest: {Dest})",
                _currentUserService.Username, id, call.Extension, call.CallerNumber, call.DestinationNumber);
            return Forbidden("شما تنها به فایل‌های ضبط‌شده داخلی خود و اعضای گروه تحت نظارت دسترسی دارید.");
        }

        var result = await _recordingService.GetCallAudioStreamAsync(id);
        if (result == null)
        {
            return NotFound(new { message = "Recording not found or unavailable." });
        }

        // Audit recording access
        try
        {
            var username = !string.IsNullOrWhiteSpace(_currentUserService.Username) 
                ? _currentUserService.Username 
                : (User?.Identity?.Name ?? "Unknown");

            _dbContext.CallAudits.Add(new CallAudit
            {
                CallRecordId = id,
                Action = "RecordingAccessed",
                PerformedBy = username,
                TimestampUtc = DateTime.UtcNow,
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { call.Extension, call.CallerNumber, call.DestinationNumber })
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit log for recording playback on call {CallId}", id);
        }

        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true);
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> SimulateCall([FromBody] SimulateCallRequest request)
    {
        await _telephonyAdapter.SimulateCallAsync(request);
        return Ok(new { message = "Call simulation triggered successfully.", request.CallType, request.CallerNumber });
    }

    [HttpPost("reconcile-cdr")]
    public async Task<ActionResult<int>> ReconcileCdr([FromBody] List<CdrReconciliationDto> cdrBatch)
    {
        if (!HasPermission(Permissions.CallsSupervise) && !HasPermission(Permissions.CallsConfig))
        {
            return Forbidden("شما مجوز تطبیق اسناد CDR تلفنی را ندارید.");
        }

        var username = !string.IsNullOrWhiteSpace(_currentUserService.Username) 
            ? _currentUserService.Username 
            : (User?.Identity?.Name ?? "System");

        var reconciled = await _callManagerService.ReconcileCdrRecordsAsync(cdrBatch, username);
        return Ok(new { reconciledCount = reconciled });
    }

    [HttpGet("extensions")]
    public async Task<ActionResult<List<UserExtensionDto>>> GetExtensions()
    {
        if (!HasPermission(Permissions.CallsView))
        {
            return Forbidden();
        }

        var list = await _callManagerService.GetUserExtensionsAsync();
        return Ok(list);
    }

    [HttpPost("extensions")]
    public async Task<ActionResult<UserExtensionDto>> SaveExtension([FromBody] UserExtensionDto dto)
    {
        if (!HasPermission(Permissions.CallsConfig) && !_currentUserService.IsInRole(Roles.Admin))
        {
            return Forbidden();
        }

        var result = await _callManagerService.SaveUserExtensionAsync(dto);
        await _accessControlService.SyncActiveConnectionsAsync();
        return Ok(result);
    }

    [HttpGet("active")]
    public async Task<ActionResult<CallerPopupDto?>> GetActiveCall([FromQuery] string? extension = null)
    {
        var scope = await GetCurrentScopeAsync();

        var targetExt = extension?.Trim();
        if (string.IsNullOrWhiteSpace(targetExt))
        {
            targetExt = scope.VerifiedUserExtension;
        }
        else
        {
            if (!scope.HasGlobalAccess && !scope.AllowedExtensions.Contains(targetExt))
            {
                return Forbidden("شما به تماس فعال این داخلی دسترسی ندارید.");
            }
        }

        if (string.IsNullOrWhiteSpace(targetExt))
        {
            return Ok(null);
        }

        var active = await _callManagerService.GetActiveCallForExtensionAsync(targetExt);
        return Ok(active);
    }
}

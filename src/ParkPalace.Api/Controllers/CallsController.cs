using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Api.Controllers;

[ApiController]
[Route("api/v1/calls")]
public class CallsController : ControllerBase
{
    private readonly ICallManagerService _callManagerService;
    private readonly IRecordingService _recordingService;
    private readonly ITelephonyAdapter _telephonyAdapter;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CallsController> _logger;

    public CallsController(
        ICallManagerService callManagerService,
        IRecordingService recordingService,
        ITelephonyAdapter telephonyAdapter,
        ICurrentUserService currentUserService,
        ILogger<CallsController> logger)
    {
        _callManagerService = callManagerService;
        _recordingService = recordingService;
        _telephonyAdapter = telephonyAdapter;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private bool HasPermission(string permission)
    {
        return _currentUserService.Roles.Contains(Roles.Admin) ||
               _currentUserService.Permissions.Contains(permission);
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

        var isSupervisor = HasPermission(Permissions.CallsSupervise) || 
                           _currentUserService.IsInRole(Roles.Admin) || 
                           _currentUserService.IsInRole(Roles.Manager);

        var filterExt = extension;
        if (!isSupervisor && string.IsNullOrWhiteSpace(filterExt) && !_currentUserService.IsInRole(Roles.Procurement))
        {
            var userExtensions = await _callManagerService.GetUserExtensionsAsync();
            var myExt = userExtensions.Find(e => e.Username.Equals(_currentUserService.Username, StringComparison.OrdinalIgnoreCase))?.Extension;
            if (!string.IsNullOrWhiteSpace(myExt))
            {
                filterExt = myExt;
            }
        }

        var calls = await _callManagerService.GetCallsAsync(skip, take, filterExt, direction, status, unknownOnly);
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

        var note = await _callManagerService.AddCallNoteAsync(id, request, _currentUserService.Username, _currentUserService.UserId);
        return Ok(note);
    }

    [HttpPost("{id:guid}/classify")]
    public async Task<ActionResult<CallRecordDto>> ClassifyCall(Guid id, [FromBody] ClassifyCallRequest request)
    {
        if (!HasPermission(Permissions.SuppliersManage) && !HasPermission(Permissions.CallsManage))
        {
            return Forbidden("شما مجوز انتساب شماره ناشناس به تأمین‌کننده را ندارید.");
        }

        var updated = await _callManagerService.ClassifyUnknownCallAsync(id, request, _currentUserService.Username);
        return Ok(updated);
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

        var note = await _callManagerService.UpdateFollowUpStatusAsync(noteId, status, _currentUserService.Username);
        return Ok(note);
    }

    [HttpGet("{id:guid}/recording")]
    public async Task<IActionResult> StreamRecording(Guid id)
    {
        if (!HasPermission(Permissions.CallsRecordingsListen))
        {
            _logger.LogWarning("Unauthorized recording playback attempt by {Username} for Call {CallId}", _currentUserService.Username, id);
            return Forbidden("شما مجوز شنود مکالمات ضبط‌شده را ندارید.");
        }

        var result = await _recordingService.GetCallAudioStreamAsync(id);
        if (result == null)
        {
            return NotFound(new { message = "Recording not found or unavailable." });
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

        var reconciled = await _callManagerService.ReconcileCdrRecordsAsync(cdrBatch, _currentUserService.Username);
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
        return Ok(result);
    }
}

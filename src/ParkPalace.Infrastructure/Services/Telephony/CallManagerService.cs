using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.Common;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Telephony;

public class CallManagerService : ICallManagerService
{
    private readonly ICrmDbContext _dbContext;
    private readonly ITelephonyNotificationService _notificationService;
    private readonly IRecordingService _recordingService;
    private readonly ILogger<CallManagerService> _logger;

    public CallManagerService(
        ICrmDbContext dbContext,
        ITelephonyNotificationService notificationService,
        IRecordingService recordingService,
        ILogger<CallManagerService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _recordingService = recordingService;
        _logger = logger;
    }

    public async Task<CallRecordDto> IngestCallEventAsync(CallEventPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.UniqueId))
        {
            throw new ArgumentException("UniqueId cannot be empty.", nameof(payload));
        }

        var normalizedCaller = PhoneNormalizer.Normalize(payload.CallerNumber);
        var normalizedDest = PhoneNormalizer.Normalize(payload.DestinationNumber);

        // Check if call already exists
        var existingCall = await _dbContext.CallRecords
            .Include(c => c.Party)
            .Include(c => c.Contact)
            .Include(c => c.Notes)
            .Include(c => c.Audits)
            .FirstOrDefaultAsync(c => c.UniqueId == payload.UniqueId);

        if (existingCall == null)
        {
            // Initial call creation (e.g. Ringing or direct Ingestion)
            var newCall = new CallRecord
            {
                UniqueId = payload.UniqueId,
                LinkedId = string.IsNullOrWhiteSpace(payload.LinkedId) ? payload.UniqueId : payload.LinkedId,
                Direction = payload.Direction,
                Status = payload.Status,
                CallerNumber = payload.CallerNumber,
                NormalizedCallerNumber = normalizedCaller,
                CallerName = payload.CallerName,
                DestinationNumber = payload.DestinationNumber,
                Extension = payload.Extension,
                Channel = payload.Channel,
                DestinationChannel = payload.DestinationChannel,
                StartTimeUtc = payload.TimestampUtc,
                DurationSeconds = payload.DurationSeconds,
                BillableSeconds = payload.BillableSeconds,
                DisconnectionCause = payload.DisconnectionCause,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Party & Supplier Identification
            var searchNumber = payload.Direction == CallDirection.Outgoing ? normalizedDest : normalizedCaller;
            var matchedParties = await MatchPartiesByNumberAsync(searchNumber);

            if (matchedParties.Count == 1)
            {
                var match = matchedParties[0];
                newCall.PartyId = match.PartyId;
                newCall.ContactId = match.ContactId;
                newCall.MatchConfidence = "Exact";
                newCall.IsUnknownCaller = false;
                newCall.CallerName = !string.IsNullOrWhiteSpace(match.ContactName)
                    ? $"{match.PartyName} ({match.ContactName})"
                    : match.PartyName;
            }
            else if (matchedParties.Count > 1)
            {
                newCall.MatchConfidence = "Multiple";
                newCall.IsUnknownCaller = false;
                newCall.CallerName = $"چند تأمین‌کننده منطبق ({matchedParties.Count} مورد)";
            }
            else
            {
                newCall.MatchConfidence = "None";
                newCall.IsUnknownCaller = true;
                if (string.IsNullOrWhiteSpace(newCall.CallerName))
                {
                    newCall.CallerName = "ناشناس";
                }
            }

            _dbContext.CallRecords.Add(newCall);

            var audit = new CallAudit
            {
                CallRecordId = newCall.Id,
                Action = payload.Status.ToString(),
                PerformedBy = "AMI_Worker",
                TimestampUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    payload.Direction,
                    payload.CallerNumber,
                    payload.DestinationNumber,
                    payload.Extension,
                    payload.Channel
                })
            };
            _dbContext.CallAudits.Add(audit);

            await _dbContext.SaveChangesAsync();

            // Prepare and broadcast live caller popup
            var recentCalls = await GetRecentCallSummariesAsync(searchNumber, newCall.Id);
            var popup = new CallerPopupDto
            {
                CallId = newCall.Id,
                UniqueId = newCall.UniqueId,
                LinkedId = newCall.LinkedId,
                Direction = newCall.Direction,
                Status = newCall.Status,
                CallerNumber = newCall.CallerNumber,
                NormalizedCallerNumber = newCall.NormalizedCallerNumber,
                CallerName = newCall.CallerName,
                DestinationNumber = newCall.DestinationNumber,
                Extension = newCall.Extension,
                TimestampUtc = newCall.StartTimeUtc,
                MatchStatus = newCall.MatchConfidence,
                MatchedParties = matchedParties,
                RecentCalls = recentCalls
            };

            await _notificationService.BroadcastCallPopupAsync(newCall.Extension, popup);

            return MapToDto(newCall);
        }
        else
        {
            // Idempotent State Transition / Leg Update
            var previousStatus = existingCall.Status;
            existingCall.Status = payload.Status;

            if (payload.Status == CallStatus.Answered && existingCall.AnswerTimeUtc == null)
            {
                existingCall.AnswerTimeUtc = payload.TimestampUtc;
            }

            if (payload.Status is CallStatus.Completed or CallStatus.Missed or CallStatus.Busy or CallStatus.Failed or CallStatus.Transferred)
            {
                existingCall.EndTimeUtc = payload.TimestampUtc;
                if (payload.DurationSeconds.HasValue)
                {
                    existingCall.DurationSeconds = payload.DurationSeconds;
                }
                if (payload.BillableSeconds.HasValue)
                {
                    existingCall.BillableSeconds = payload.BillableSeconds;
                }
                if (!string.IsNullOrWhiteSpace(payload.DisconnectionCause))
                {
                    existingCall.DisconnectionCause = payload.DisconnectionCause;
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.RecordingFileName))
            {
                existingCall.RecordingFileName = payload.RecordingFileName;
                existingCall.HasRecording = true;
            }

            var audit = new CallAudit
            {
                CallRecordId = existingCall.Id,
                Action = payload.Status.ToString(),
                PerformedBy = "AMI_Worker",
                TimestampUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    FromStatus = previousStatus.ToString(),
                    ToStatus = payload.Status.ToString(),
                    payload.DurationSeconds,
                    payload.DisconnectionCause
                })
            };
            _dbContext.CallAudits.Add(audit);

            await _dbContext.SaveChangesAsync();

            var dto = MapToDto(existingCall);
            await _notificationService.BroadcastCallStatusAsync(dto);

            if (existingCall.Status is CallStatus.Completed or CallStatus.Missed or CallStatus.Busy or CallStatus.Failed)
            {
                await _notificationService.BroadcastCallEndedAsync(existingCall.UniqueId);
            }

            return dto;
        }
    }

    public async Task<List<CallRecordDto>> GetCallsAsync(int skip = 0, int take = 50, string? extension = null, CallDirection? direction = null, CallStatus? status = null, bool? unknownOnly = null)
    {
        var query = _dbContext.CallRecords
            .Include(c => c.Party)
            .Include(c => c.Contact)
            .Include(c => c.Notes)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(extension))
        {
            query = query.Where(c => c.Extension == extension.Trim());
        }

        if (direction.HasValue)
        {
            query = query.Where(c => c.Direction == direction.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (unknownOnly == true)
        {
            query = query.Where(c => c.IsUnknownCaller);
        }

        var calls = await query
            .OrderByDescending(c => c.StartTimeUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return calls.Select(MapToDto).ToList();
    }

    public async Task<CallRecordDto?> GetCallByIdAsync(Guid id)
    {
        var call = await _dbContext.CallRecords
            .Include(c => c.Party)
            .Include(c => c.Contact)
            .Include(c => c.Notes)
            .Include(c => c.Audits)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        return call == null ? null : MapToDto(call);
    }

    public async Task<CallNoteDto> AddCallNoteAsync(Guid callId, CreateCallNoteRequest request, string username, Guid? userId)
    {
        var call = await _dbContext.CallRecords.FirstOrDefaultAsync(c => c.Id == callId);
        if (call == null)
        {
            throw new KeyNotFoundException($"Call with ID {callId} not found.");
        }

        var note = new CallNote
        {
            CallRecordId = callId,
            UserId = userId,
            Username = username,
            NoteText = request.NoteText,
            Outcome = string.IsNullOrWhiteSpace(request.Outcome) ? "General" : request.Outcome,
            FollowUpRequired = request.FollowUpRequired,
            FollowUpDueDateUtc = request.FollowUpDueDateUtc,
            FollowUpAssignedTo = request.FollowUpAssignedTo,
            FollowUpStatus = request.FollowUpRequired ? FollowUpStatus.Pending : FollowUpStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CallNotes.Add(note);

        var audit = new CallAudit
        {
            CallRecordId = call.Id,
            Action = "NoteAdded",
            PerformedBy = username,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                request.Outcome,
                request.FollowUpRequired,
                request.FollowUpAssignedTo
            })
        };
        _dbContext.CallAudits.Add(audit);

        await _dbContext.SaveChangesAsync();

        return new CallNoteDto
        {
            Id = note.Id,
            CallRecordId = note.CallRecordId,
            UserId = note.UserId,
            Username = note.Username,
            NoteText = note.NoteText,
            Outcome = note.Outcome,
            FollowUpRequired = note.FollowUpRequired,
            FollowUpDueDateUtc = note.FollowUpDueDateUtc,
            FollowUpAssignedTo = note.FollowUpAssignedTo,
            FollowUpStatus = note.FollowUpStatus,
            CreatedAtUtc = note.CreatedAtUtc
        };
    }

    public async Task<CallRecordDto> ClassifyUnknownCallAsync(Guid callId, ClassifyCallRequest request, string username)
    {
        var call = await _dbContext.CallRecords
            .Include(c => c.Notes)
            .Include(c => c.Audits)
            .FirstOrDefaultAsync(c => c.Id == callId);

        if (call == null)
        {
            throw new KeyNotFoundException($"Call with ID {callId} not found.");
        }

        var party = await _dbContext.Parties.FirstOrDefaultAsync(p => p.Id == request.PartyId);
        if (party == null)
        {
            throw new KeyNotFoundException($"Party with ID {request.PartyId} not found.");
        }

        PartyContact? contact = null;
        if (request.ContactId.HasValue)
        {
            contact = await _dbContext.PartyContacts.FirstOrDefaultAsync(c => c.Id == request.ContactId.Value && c.PartyId == party.Id);
        }

        call.PartyId = party.Id;
        call.ContactId = contact?.Id;
        call.IsUnknownCaller = false;
        call.MatchConfidence = "Manual";
        call.CallerName = contact != null ? $"{party.DisplayName} ({contact.PersonName})" : party.DisplayName;

        var audit = new CallAudit
        {
            CallRecordId = call.Id,
            Action = "SupplierLinked",
            PerformedBy = username,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                PartyId = party.Id,
                PartyName = party.DisplayName,
                ContactId = contact?.Id,
                ContactName = contact?.PersonName
            })
        };
        _dbContext.CallAudits.Add(audit);

        // Also update past unknown calls with the exact same normalized number to preserve call history!
        var pastUnknownCalls = await _dbContext.CallRecords
            .Where(c => c.NormalizedCallerNumber == call.NormalizedCallerNumber && c.IsUnknownCaller && c.Id != call.Id)
            .ToListAsync();

        foreach (var pastCall in pastUnknownCalls)
        {
            pastCall.PartyId = party.Id;
            pastCall.ContactId = contact?.Id;
            pastCall.IsUnknownCaller = false;
            pastCall.MatchConfidence = "Manual";
            pastCall.CallerName = call.CallerName;

            var retroAudit = new CallAudit
            {
                CallRecordId = pastCall.Id,
                Action = "SupplierLinkedRetrospective",
                PerformedBy = username,
                TimestampUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new { SourceCallId = call.Id, PartyId = party.Id })
            };
            _dbContext.CallAudits.Add(retroAudit);
        }

        await _dbContext.SaveChangesAsync();

        return MapToDto(call);
    }

    public async Task<List<PartyMatchDto>> MatchPartiesByNumberAsync(string rawPhoneNumber)
    {
        var normalized = PhoneNormalizer.Normalize(rawPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new List<PartyMatchDto>();
        }

        var matchingPhones = await _dbContext.PartyPhones
            .Include(p => p.Party)
            .Include(p => p.Contact)
            .Where(p => p.NormalizedValue == normalized)
            .AsNoTracking()
            .ToListAsync();

        var matches = new List<PartyMatchDto>();
        foreach (var phone in matchingPhones)
        {
            if (phone.Party != null)
            {
                matches.Add(new PartyMatchDto
                {
                    PartyId = phone.PartyId,
                    PartyName = phone.Party.DisplayName,
                    PartyCode = phone.Party.Id.ToString()[..8].ToUpper(),
                    ContactId = phone.ContactId,
                    ContactName = phone.Contact?.PersonName,
                    ContactRole = phone.Contact?.Position,
                    PhoneNumber = phone.OriginalValue,
                    MatchType = phone.ContactId.HasValue ? "Contact" : "Company"
                });
            }
        }

        return matches;
    }

    public async Task<List<CallRecordDto>> GetUnknownCallsAsync()
    {
        var calls = await _dbContext.CallRecords
            .Include(c => c.Notes)
            .Where(c => c.IsUnknownCaller)
            .OrderByDescending(c => c.StartTimeUtc)
            .Take(100)
            .AsNoTracking()
            .ToListAsync();

        return calls.Select(MapToDto).ToList();
    }

    public async Task<List<CallNoteDto>> GetPendingFollowUpsAsync()
    {
        var notes = await _dbContext.CallNotes
            .Where(n => n.FollowUpRequired && n.FollowUpStatus == FollowUpStatus.Pending)
            .OrderBy(n => n.FollowUpDueDateUtc)
            .AsNoTracking()
            .ToListAsync();

        return notes.Select(n => new CallNoteDto
        {
            Id = n.Id,
            CallRecordId = n.CallRecordId,
            UserId = n.UserId,
            Username = n.Username,
            NoteText = n.NoteText,
            Outcome = n.Outcome,
            FollowUpRequired = n.FollowUpRequired,
            FollowUpDueDateUtc = n.FollowUpDueDateUtc,
            FollowUpAssignedTo = n.FollowUpAssignedTo,
            FollowUpStatus = n.FollowUpStatus,
            CreatedAtUtc = n.CreatedAtUtc
        }).ToList();
    }

    public async Task<CallNoteDto> UpdateFollowUpStatusAsync(Guid noteId, FollowUpStatus status, string username)
    {
        var note = await _dbContext.CallNotes.FirstOrDefaultAsync(n => n.Id == noteId);
        if (note == null)
        {
            throw new KeyNotFoundException($"Note with ID {noteId} not found.");
        }

        note.FollowUpStatus = status;

        await _dbContext.SaveChangesAsync();

        return new CallNoteDto
        {
            Id = note.Id,
            CallRecordId = note.CallRecordId,
            UserId = note.UserId,
            Username = note.Username,
            NoteText = note.NoteText,
            Outcome = note.Outcome,
            FollowUpRequired = note.FollowUpRequired,
            FollowUpDueDateUtc = note.FollowUpDueDateUtc,
            FollowUpAssignedTo = note.FollowUpAssignedTo,
            FollowUpStatus = note.FollowUpStatus,
            CreatedAtUtc = note.CreatedAtUtc
        };
    }

    public async Task<int> ReconcileCdrRecordsAsync(IEnumerable<CdrReconciliationDto> cdrRecords, string reconciledBy)
    {
        int reconciledCount = 0;

        foreach (var cdr in cdrRecords)
        {
            if (string.IsNullOrWhiteSpace(cdr.UniqueId))
                continue;

            var call = await _dbContext.CallRecords.FirstOrDefaultAsync(c => c.UniqueId == cdr.UniqueId);
            if (call != null)
            {
                call.CdrId = cdr.UniqueId;
                call.DurationSeconds = cdr.DurationSeconds;
                call.BillableSeconds = cdr.BillableSeconds;
                call.DisconnectionCause = cdr.Disposition;
                call.ReconciliationStatus = CallReconciliationStatus.Reconciled;

                if (!string.IsNullOrWhiteSpace(cdr.RecordingFile))
                {
                    await _recordingService.LinkRecordingToCallAsync(call.UniqueId, cdr.RecordingFile);
                }

                var audit = new CallAudit
                {
                    CallRecordId = call.Id,
                    Action = "CdrReconciled",
                    PerformedBy = reconciledBy,
                    TimestampUtc = DateTime.UtcNow,
                    DetailsJson = JsonSerializer.Serialize(new
                    {
                        cdr.UniqueId,
                        cdr.DurationSeconds,
                        cdr.BillableSeconds,
                        cdr.Disposition
                    })
                };
                _dbContext.CallAudits.Add(audit);

                reconciledCount++;
            }
            else
            {
                // Uncaptured call: create a new reconciled call record from CDR
                var normCaller = PhoneNormalizer.Normalize(cdr.CallerNumber);
                var matches = await MatchPartiesByNumberAsync(normCaller);

                var newCall = new CallRecord
                {
                    UniqueId = cdr.UniqueId,
                    LinkedId = string.IsNullOrWhiteSpace(cdr.LinkedId) ? cdr.UniqueId : cdr.LinkedId,
                    Direction = CallDirection.Incoming,
                    Status = cdr.Disposition == "ANSWERED" ? CallStatus.Completed : CallStatus.Missed,
                    CallerNumber = cdr.CallerNumber,
                    NormalizedCallerNumber = normCaller,
                    DestinationNumber = cdr.DestinationNumber,
                    Extension = cdr.DestinationNumber,
                    StartTimeUtc = cdr.StartTimeUtc,
                    AnswerTimeUtc = cdr.AnswerTimeUtc,
                    EndTimeUtc = cdr.EndTimeUtc,
                    DurationSeconds = cdr.DurationSeconds,
                    BillableSeconds = cdr.BillableSeconds,
                    DisconnectionCause = cdr.Disposition,
                    CdrId = cdr.UniqueId,
                    ReconciliationStatus = CallReconciliationStatus.Reconciled,
                    CreatedAtUtc = DateTime.UtcNow
                };

                if (matches.Count == 1)
                {
                    newCall.PartyId = matches[0].PartyId;
                    newCall.ContactId = matches[0].ContactId;
                    newCall.CallerName = matches[0].PartyName;
                    newCall.MatchConfidence = "Exact";
                    newCall.IsUnknownCaller = false;
                }
                else
                {
                    newCall.IsUnknownCaller = true;
                    newCall.MatchConfidence = matches.Count > 1 ? "Multiple" : "None";
                }

                if (!string.IsNullOrWhiteSpace(cdr.RecordingFile))
                {
                    newCall.RecordingFileName = cdr.RecordingFile;
                    newCall.HasRecording = true;
                }

                _dbContext.CallRecords.Add(newCall);

                var audit = new CallAudit
                {
                    CallRecordId = newCall.Id,
                    Action = "CdrIngestedAndReconciled",
                    PerformedBy = reconciledBy,
                    TimestampUtc = DateTime.UtcNow
                };
                _dbContext.CallAudits.Add(audit);

                reconciledCount++;
            }
        }

        await _dbContext.SaveChangesAsync();
        return reconciledCount;
    }

    public async Task<List<UserExtensionDto>> GetUserExtensionsAsync()
    {
        var exts = await _dbContext.UserExtensions.AsNoTracking().ToListAsync();
        return exts.Select(e => new UserExtensionDto
        {
            Id = e.Id,
            UserId = e.UserId,
            Username = e.Username,
            Extension = e.Extension,
            DeviceType = e.DeviceType,
            IsActive = e.IsActive,
            Description = e.Description
        }).ToList();
    }

    public async Task<UserExtensionDto> SaveUserExtensionAsync(UserExtensionDto dto)
    {
        UserExtension? entity = null;
        if (dto.Id != Guid.Empty)
        {
            entity = await _dbContext.UserExtensions.FirstOrDefaultAsync(e => e.Id == dto.Id);
        }

        if (entity == null)
        {
            entity = new UserExtension
            {
                Extension = dto.Extension.Trim(),
                Username = dto.Username.Trim(),
                UserId = dto.UserId,
                DeviceType = string.IsNullOrWhiteSpace(dto.DeviceType) ? "SIP" : dto.DeviceType,
                IsActive = dto.IsActive,
                Description = dto.Description
            };
            _dbContext.UserExtensions.Add(entity);
        }
        else
        {
            entity.Extension = dto.Extension.Trim();
            entity.Username = dto.Username.Trim();
            entity.UserId = dto.UserId;
            entity.DeviceType = string.IsNullOrWhiteSpace(dto.DeviceType) ? "SIP" : dto.DeviceType;
            entity.IsActive = dto.IsActive;
            entity.Description = dto.Description;
        }

        await _dbContext.SaveChangesAsync();
        dto.Id = entity.Id;
        return dto;
    }

    private async Task<List<RecentCallSummaryDto>> GetRecentCallSummariesAsync(string normalizedPhone, Guid currentCallId)
    {
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            return new List<RecentCallSummaryDto>();

        var recentCalls = await _dbContext.CallRecords
            .Include(c => c.Notes)
            .Where(c => c.NormalizedCallerNumber == normalizedPhone && c.Id != currentCallId)
            .OrderByDescending(c => c.StartTimeUtc)
            .Take(5)
            .AsNoTracking()
            .ToListAsync();

        return recentCalls.Select(c => new RecentCallSummaryDto
        {
            Id = c.Id,
            Direction = c.Direction,
            Status = c.Status,
            StartTimeUtc = c.StartTimeUtc,
            DurationSeconds = c.DurationSeconds,
            Extension = c.Extension,
            HasRecording = c.HasRecording,
            NoteSummary = c.Notes.OrderByDescending(n => n.CreatedAtUtc).FirstOrDefault()?.NoteText
        }).ToList();
    }

    private static CallRecordDto MapToDto(CallRecord c)
    {
        return new CallRecordDto
        {
            Id = c.Id,
            UniqueId = c.UniqueId,
            LinkedId = c.LinkedId,
            Direction = c.Direction,
            Status = c.Status,
            CallerNumber = c.CallerNumber,
            NormalizedCallerNumber = c.NormalizedCallerNumber,
            CallerName = c.CallerName,
            DestinationNumber = c.DestinationNumber,
            Extension = c.Extension,
            Channel = c.Channel,
            DestinationChannel = c.DestinationChannel,
            StartTimeUtc = c.StartTimeUtc,
            AnswerTimeUtc = c.AnswerTimeUtc,
            EndTimeUtc = c.EndTimeUtc,
            DurationSeconds = c.DurationSeconds,
            BillableSeconds = c.BillableSeconds,
            PartyId = c.PartyId,
            PartyName = c.Party?.DisplayName,
            ContactId = c.ContactId,
            ContactName = c.Contact?.PersonName,
            ContactPosition = c.Contact?.Position,
            IsUnknownCaller = c.IsUnknownCaller,
            MatchConfidence = c.MatchConfidence,
            RecordingFileName = c.RecordingFileName,
            HasRecording = c.HasRecording,
            RecordingDurationSeconds = c.RecordingDurationSeconds,
            RecordingFileSize = c.RecordingFileSize,
            CdrId = c.CdrId,
            ReconciliationStatus = c.ReconciliationStatus,
            DisconnectionCause = c.DisconnectionCause,
            NotesCount = c.Notes.Count,
            Notes = c.Notes.Select(n => new CallNoteDto
            {
                Id = n.Id,
                CallRecordId = n.CallRecordId,
                UserId = n.UserId,
                Username = n.Username,
                NoteText = n.NoteText,
                Outcome = n.Outcome,
                FollowUpRequired = n.FollowUpRequired,
                FollowUpDueDateUtc = n.FollowUpDueDateUtc,
                FollowUpAssignedTo = n.FollowUpAssignedTo,
                FollowUpStatus = n.FollowUpStatus,
                CreatedAtUtc = n.CreatedAtUtc
            }).ToList(),
            Audits = c.Audits.Select(a => new CallAuditDto
            {
                Id = a.Id,
                CallRecordId = a.CallRecordId,
                Action = a.Action,
                PerformedBy = a.PerformedBy,
                TimestampUtc = a.TimestampUtc,
                DetailsJson = a.DetailsJson
            }).ToList()
        };
    }
}

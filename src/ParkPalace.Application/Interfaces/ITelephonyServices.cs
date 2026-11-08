using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.Interfaces;

public class CallEventPayload
{
    public string UniqueId { get; set; } = string.Empty;
    public string LinkedId { get; set; } = string.Empty;
    public CallDirection Direction { get; set; } = CallDirection.Incoming;
    public CallStatus Status { get; set; } = CallStatus.Ringing;
    public string CallerNumber { get; set; } = string.Empty;
    public string? CallerName { get; set; }
    public string DestinationNumber { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? Channel { get; set; }
    public string? DestinationChannel { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int? DurationSeconds { get; set; }
    public int? BillableSeconds { get; set; }
    public string? RecordingFileName { get; set; }
    public string? DisconnectionCause { get; set; }
}

public interface ITelephonyAdapter
{
    bool IsConnected { get; }
    string AdapterType { get; } // "Simulated" or "IssabelAMI"
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    event Func<CallEventPayload, Task>? OnCallEventReceived;
    Task SimulateCallAsync(SimulateCallRequest request);
    Task<List<AvailableExtensionDto>> GetAvailableExtensionsAsync(CancellationToken cancellationToken = default);
}

public interface ICallManagerService
{
    Task<CallRecordDto> IngestCallEventAsync(CallEventPayload payload);
    Task<List<CallRecordDto>> GetCallsAsync(int skip = 0, int take = 50, string? extension = null, CallDirection? direction = null, CallStatus? status = null, bool? unknownOnly = null);
    Task<CallRecordDto?> GetCallByIdAsync(Guid id);
    Task<CallNoteDto> AddCallNoteAsync(Guid callId, CreateCallNoteRequest request, string username, Guid? userId);
    Task<CallRecordDto> ClassifyUnknownCallAsync(Guid callId, ClassifyCallRequest request, string username);
    Task<List<PartyMatchDto>> MatchPartiesByNumberAsync(string rawPhoneNumber);
    Task<List<CallRecordDto>> GetUnknownCallsAsync();
    Task<List<CallNoteDto>> GetPendingFollowUpsAsync();
    Task<CallNoteDto> UpdateFollowUpStatusAsync(Guid noteId, FollowUpStatus status, string username);
    Task<int> ReconcileCdrRecordsAsync(IEnumerable<CdrReconciliationDto> cdrRecords, string reconciledBy);
    Task<List<UserExtensionDto>> GetUserExtensionsAsync();
    Task<UserExtensionDto> SaveUserExtensionAsync(UserExtensionDto dto);
}

public interface IRecordingService
{
    Task<(Stream Stream, string ContentType, string FileName)?> GetCallAudioStreamAsync(Guid callId);
    bool ValidateRecordingPath(string filePath, out string sanitizedPath);
    Task<bool> LinkRecordingToCallAsync(string uniqueId, string recordingFileName, long? fileSize = null, int? durationSeconds = null);
    string GetRecordingsDirectory();
}

public interface ITelephonyNotificationService
{
    Task BroadcastCallPopupAsync(string? extension, CallerPopupDto popup);
    Task BroadcastCallStatusAsync(CallRecordDto call);
    Task BroadcastCallEndedAsync(string uniqueId);
}


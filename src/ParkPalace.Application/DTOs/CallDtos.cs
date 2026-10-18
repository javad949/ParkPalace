using System;
using System.Collections.Generic;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.DTOs;

public class CallRecordDto
{
    public Guid Id { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string LinkedId { get; set; } = string.Empty;
    public CallDirection Direction { get; set; }
    public CallStatus Status { get; set; }
    public string CallerNumber { get; set; } = string.Empty;
    public string NormalizedCallerNumber { get; set; } = string.Empty;
    public string? CallerName { get; set; }
    public string DestinationNumber { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? Channel { get; set; }
    public string? DestinationChannel { get; set; }

    public DateTime StartTimeUtc { get; set; }
    public DateTime? AnswerTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public int? BillableSeconds { get; set; }

    public Guid? PartyId { get; set; }
    public string? PartyName { get; set; }
    public string? PartyCode { get; set; }
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPosition { get; set; }

    public bool IsUnknownCaller { get; set; }
    public string MatchConfidence { get; set; } = "None";

    public string? RecordingFileName { get; set; }
    public bool HasRecording { get; set; }
    public int? RecordingDurationSeconds { get; set; }
    public long? RecordingFileSize { get; set; }

    public string? CdrId { get; set; }
    public CallReconciliationStatus ReconciliationStatus { get; set; }
    public string? DisconnectionCause { get; set; }

    public int NotesCount { get; set; }
    public List<CallNoteDto> Notes { get; set; } = new();
    public List<CallAuditDto> Audits { get; set; } = new();
}

public class CallNoteDto
{
    public Guid Id { get; set; }
    public Guid CallRecordId { get; set; }
    public Guid? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string Outcome { get; set; } = "General";
    public bool FollowUpRequired { get; set; }
    public DateTime? FollowUpDueDateUtc { get; set; }
    public string? FollowUpAssignedTo { get; set; }
    public FollowUpStatus FollowUpStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CallAuditDto
{
    public Guid Id { get; set; }
    public Guid CallRecordId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? DetailsJson { get; set; }
}

public class UserExtensionDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "SIP";
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}

public class PartyMatchDto
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string PartyCode { get; set; } = string.Empty;
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactRole { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string MatchType { get; set; } = "Company"; // Company, Contact
}

public class RecentCallSummaryDto
{
    public Guid Id { get; set; }
    public CallDirection Direction { get; set; }
    public CallStatus Status { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Extension { get; set; }
    public bool HasRecording { get; set; }
    public string? NoteSummary { get; set; }
}

public class CallerPopupDto
{
    public Guid CallId { get; set; }
    public string UniqueId { get; set; } = string.Empty;
    public string LinkedId { get; set; } = string.Empty;
    public CallDirection Direction { get; set; }
    public CallStatus Status { get; set; }
    public string CallerNumber { get; set; } = string.Empty;
    public string NormalizedCallerNumber { get; set; } = string.Empty;
    public string? CallerName { get; set; }
    public string DestinationNumber { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public DateTime TimestampUtc { get; set; }

    public string MatchStatus { get; set; } = "None"; // None, Single, Multiple
    public List<PartyMatchDto> MatchedParties { get; set; } = new();
    public List<RecentCallSummaryDto> RecentCalls { get; set; } = new();
}

public class CreateCallNoteRequest
{
    public string NoteText { get; set; } = string.Empty;
    public string Outcome { get; set; } = "General";
    public bool FollowUpRequired { get; set; }
    public DateTime? FollowUpDueDateUtc { get; set; }
    public string? FollowUpAssignedTo { get; set; }
}

public class ClassifyCallRequest
{
    public Guid PartyId { get; set; }
    public Guid? ContactId { get; set; }
}

public class SimulateCallRequest
{
    public string CallerNumber { get; set; } = string.Empty;
    public string DestinationNumber { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string? CallerName { get; set; }
    public string CallType { get; set; } = "incoming"; // incoming, outgoing, missed, busy, answered, transfer
    public int DurationSeconds { get; set; } = 30;
}

public class CdrReconciliationDto
{
    public string UniqueId { get; set; } = string.Empty;
    public string? LinkedId { get; set; }
    public string CallerNumber { get; set; } = string.Empty;
    public string DestinationNumber { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime? AnswerTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public int DurationSeconds { get; set; }
    public int BillableSeconds { get; set; }
    public string Disposition { get; set; } = "ANSWERED"; // ANSWERED, NO ANSWER, BUSY, FAILED
    public string? RecordingFile { get; set; }
}

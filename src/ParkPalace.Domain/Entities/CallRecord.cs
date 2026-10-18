using System;
using System.Collections.Generic;
using ParkPalace.Domain.Common;

namespace ParkPalace.Domain.Entities;

public enum CallDirection
{
    Incoming = 0,
    Outgoing = 1,
    Internal = 2
}

public enum CallStatus
{
    Ringing = 0,
    Answered = 1,
    Missed = 2,
    Busy = 3,
    Failed = 4,
    Transferred = 5,
    Completed = 6
}

public enum CallReconciliationStatus
{
    Pending = 0,
    Reconciled = 1,
    Uncorrelated = 2,
    Ambiguous = 3,
    ManualReview = 4
}

public enum FollowUpStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2
}

public class CallRecord : BaseEntity
{
    public string UniqueId { get; set; } = string.Empty; // Asterisk AMI Uniqueid, e.g. 1729234567.123
    public string LinkedId { get; set; } = string.Empty; // Asterisk Linkedid for multi-leg / transfer tracking

    public CallDirection Direction { get; set; } = CallDirection.Incoming;
    public CallStatus Status { get; set; } = CallStatus.Ringing;

    public string CallerNumber { get; set; } = string.Empty;
    public string NormalizedCallerNumber { get; set; } = string.Empty;
    public string? CallerName { get; set; }
    public string DestinationNumber { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? Channel { get; set; }
    public string? DestinationChannel { get; set; }

    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AnswerTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public int? BillableSeconds { get; set; }

    // Supplier & Contact Identification
    public Guid? PartyId { get; set; }
    public Party? Party { get; set; }

    public Guid? ContactId { get; set; }
    public PartyContact? Contact { get; set; }

    public bool IsUnknownCaller { get; set; } = true;
    public string MatchConfidence { get; set; } = "None"; // None, Exact, Multiple

    // Recording Metadata
    public string? RecordingFileName { get; set; }
    public string? RecordingFilePath { get; set; }
    public int? RecordingDurationSeconds { get; set; }
    public long? RecordingFileSize { get; set; }
    public bool HasRecording { get; set; } = false;

    // CDR / CEL Correlation
    public string? CdrId { get; set; }
    public CallReconciliationStatus ReconciliationStatus { get; set; } = CallReconciliationStatus.Pending;
    public string? DisconnectionCause { get; set; }

    // Navigation
    public ICollection<CallNote> Notes { get; set; } = new List<CallNote>();
    public ICollection<CallAudit> Audits { get; set; } = new List<CallAudit>();
}

public class CallNote : BaseEntity
{
    public Guid CallRecordId { get; set; }
    public CallRecord? CallRecord { get; set; }

    public Guid? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;

    public string Outcome { get; set; } = "General"; // PriceInquiry, OrderPlacement, DeliveryFollowup, Complaint, General
    public bool FollowUpRequired { get; set; } = false;
    public DateTime? FollowUpDueDateUtc { get; set; }
    public string? FollowUpAssignedTo { get; set; }
    public FollowUpStatus FollowUpStatus { get; set; } = FollowUpStatus.Pending;
}

public class CallAudit : BaseEntity
{
    public Guid CallRecordId { get; set; }
    public CallRecord? CallRecord { get; set; }

    public string Action { get; set; } = string.Empty; // Received, Answered, Transferred, Hangup, SupplierLinked, NoteAdded, RecordingAccessed, CdrReconciled
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? DetailsJson { get; set; }
}

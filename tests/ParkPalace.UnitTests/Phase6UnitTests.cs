using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Application.Common;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services.Telephony;
using Xunit;

namespace ParkPalace.UnitTests;

public class Phase6UnitTests
{
    private class FakeTelephonyNotificationService : ITelephonyNotificationService
    {
        public List<CallerPopupDto> SentPopups { get; } = new();
        public List<CallRecordDto> StatusUpdates { get; } = new();
        public List<string> EndedCalls { get; } = new();

        public Task BroadcastCallPopupAsync(string? extension, CallerPopupDto popup)
        {
            SentPopups.Add(popup);
            return Task.CompletedTask;
        }

        public Task BroadcastCallStatusAsync(CallRecordDto call)
        {
            StatusUpdates.Add(call);
            return Task.CompletedTask;
        }

        public Task BroadcastCallEndedAsync(string uniqueId, string? extension)
        {
            EndedCalls.Add(uniqueId);
            return Task.CompletedTask;
        }
    }

    private class FakeConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;
        public FakeConfiguration(Dictionary<string, string?> values) => _values = values;
        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value;
        }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private static (CrmDbContext context, CallManagerService callManager, RecordingService recordingService, FakeTelephonyNotificationService notificationService, string tempRecDir) CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CrmDbContext(options);

        var tempRecDir = Path.Combine(Path.GetTempPath(), "pp_test_rec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRecDir);

        var configuration = new FakeConfiguration(new Dictionary<string, string?>
        {
            { "Telephony:RecordingsPath", tempRecDir }
        });

        var recordingService = new RecordingService(context, configuration, NullLogger<RecordingService>.Instance);
        var notificationService = new FakeTelephonyNotificationService();
        var callManager = new CallManagerService(context, notificationService, recordingService, NullLogger<CallManagerService>.Instance);

        return (context, callManager, recordingService, notificationService, tempRecDir);
    }

    [Theory]
    [InlineData("۰۹۱۲۱۱۱۱۱۱۱", "09121111111")]
    [InlineData("٠٩١٢١١١١١١١", "09121111111")]
    [InlineData("+989121111111", "09121111111")]
    [InlineData("00989121111111", "09121111111")]
    [InlineData("9121111111", "09121111111")]
    [InlineData("021-8888-9999", "02188889999")]
    public void PhoneNormalizer_ShouldNormalizeIranianMobileAndLandlines(string raw, string expected)
    {
        var normalized = PhoneNormalizer.Normalize(raw);
        normalized.Should().Be(expected);
    }

    [Fact]
    public async Task IngestCallEvent_IncomingCall_ShouldCreateCallRecord_AndDispatchSignalRPopup()
    {
        var (context, callManager, _, notificationService, tempDir) = CreateTestContext();
        try
        {
            var payload = new CallEventPayload
            {
                UniqueId = "1729230000.100",
                LinkedId = "1729230000.100",
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = "09123456789",
                DestinationNumber = "101",
                Extension = "101",
                TimestampUtc = DateTime.UtcNow
            };

            var result = await callManager.IngestCallEventAsync(payload);

            result.Should().NotBeNull();
            result.UniqueId.Should().Be("1729230000.100");
            result.NormalizedCallerNumber.Should().Be("09123456789");
            result.IsUnknownCaller.Should().BeTrue();
            result.Status.Should().Be(CallStatus.Ringing);

            notificationService.SentPopups.Should().HaveCount(1);
            notificationService.SentPopups[0].UniqueId.Should().Be("1729230000.100");
            notificationService.SentPopups[0].Extension.Should().Be("101");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IngestCallEvent_KnownSupplier_ShouldMatchExactSupplierAndContact()
    {
        var (context, callManager, _, _, tempDir) = CreateTestContext();
        try
        {
            var supplier = new Party
            {
                DisplayName = "شرکت آرد نمونه البرز",
                PartyType = PartyType.LegalEntity
            };
            context.Parties.Add(supplier);

            var contact = new PartyContact
            {
                PartyId = supplier.Id,
                PersonName = "مهندس اکبری",
                Position = "مدیر فروش"
            };
            context.PartyContacts.Add(contact);

            var phone = new PartyPhone
            {
                PartyId = supplier.Id,
                ContactId = contact.Id,
                OriginalValue = "0912-999-8877",
                NormalizedValue = "09129998877",
                PhoneType = "Mobile"
            };
            context.PartyPhones.Add(phone);
            await context.SaveChangesAsync();

            var payload = new CallEventPayload
            {
                UniqueId = "1729230001.200",
                LinkedId = "1729230001.200",
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = "0912-999-8877",
                DestinationNumber = "101",
                Extension = "101"
            };

            var result = await callManager.IngestCallEventAsync(payload);

            result.IsUnknownCaller.Should().BeFalse();
            result.MatchConfidence.Should().Be("Exact");
            result.PartyId.Should().Be(supplier.Id);
            result.ContactId.Should().Be(contact.Id);
            result.CallerName.Should().Contain("شرکت آرد نمونه البرز");
            result.CallerName.Should().Contain("مهندس اکبری");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IngestCallEvent_MultipleSuppliers_ShouldFlagMultiple_WithoutAutoMerging()
    {
        var (context, callManager, _, _, tempDir) = CreateTestContext();
        try
        {
            var supplierA = new Party { DisplayName = "تأمین‌کننده روغن الف" };
            var supplierB = new Party { DisplayName = "تأمین‌کننده روغن ب" };
            context.Parties.AddRange(supplierA, supplierB);

            // Shared phone number (e.g. same holding / group company)
            context.PartyPhones.AddRange(
                new PartyPhone { PartyId = supplierA.Id, NormalizedValue = "02188880000", OriginalValue = "021-88880000" },
                new PartyPhone { PartyId = supplierB.Id, NormalizedValue = "02188880000", OriginalValue = "021-88880000" }
            );
            await context.SaveChangesAsync();

            var payload = new CallEventPayload
            {
                UniqueId = "1729230002.300",
                Direction = CallDirection.Incoming,
                CallerNumber = "02188880000",
                Extension = "101"
            };

            var result = await callManager.IngestCallEventAsync(payload);

            result.MatchConfidence.Should().Be("Multiple");
            result.PartyId.Should().BeNull(); // Do not arbitrarily pick or merge!

            var matches = await callManager.MatchPartiesByNumberAsync("02188880000");
            matches.Should().HaveCount(2);
            matches.Select(m => m.PartyId).Should().Contain(supplierA.Id).And.Contain(supplierB.Id);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IngestCallEvent_DuplicateAmiEvents_ShouldBeIdempotent()
    {
        var (context, callManager, _, notificationService, tempDir) = CreateTestContext();
        try
        {
            var payload = new CallEventPayload
            {
                UniqueId = "1729230003.400",
                LinkedId = "1729230003.400",
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = "09125554433",
                Extension = "101"
            };

            // First ingestion
            var res1 = await callManager.IngestCallEventAsync(payload);
            // Duplicate event (same UniqueId & status)
            var res2 = await callManager.IngestCallEventAsync(payload);

            res1.Id.Should().Be(res2.Id);
            context.CallRecords.Count(c => c.UniqueId == "1729230003.400").Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task IngestCallEvent_CallLifecycleAndTransfers_ShouldMaintainLinkedIdAcrossLegs()
    {
        var (context, callManager, _, _, tempDir) = CreateTestContext();
        try
        {
            var linkedId = "1729230004.100";

            // Leg 1: Ringing on 101
            var leg1Payload = new CallEventPayload
            {
                UniqueId = "1729230004.100",
                LinkedId = linkedId,
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = "09121234567",
                Extension = "101"
            };
            await callManager.IngestCallEventAsync(leg1Payload);

            // Leg 1: Answered then Transferred
            leg1Payload.Status = CallStatus.Answered;
            await callManager.IngestCallEventAsync(leg1Payload);

            leg1Payload.Status = CallStatus.Transferred;
            await callManager.IngestCallEventAsync(leg1Payload);

            // Leg 2: Transferred to Extension 102 (new UniqueId, same LinkedId)
            var leg2Payload = new CallEventPayload
            {
                UniqueId = "1729230004.101",
                LinkedId = linkedId,
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = "09121234567",
                Extension = "102"
            };
            var leg2Res = await callManager.IngestCallEventAsync(leg2Payload);

            leg2Payload.Status = CallStatus.Completed;
            leg2Payload.DurationSeconds = 45;
            await callManager.IngestCallEventAsync(leg2Payload);

            var allLegs = await context.CallRecords.Where(c => c.LinkedId == linkedId).ToListAsync();
            allLegs.Should().HaveCount(2);
            allLegs[0].Status.Should().Be(CallStatus.Transferred);
            allLegs[1].Status.Should().Be(CallStatus.Completed);
            allLegs[1].Extension.Should().Be("102");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ClassifyUnknownCall_ShouldLinkSupplier_AndPreserveCallHistoryRetrospectively()
    {
        var (context, callManager, _, _, tempDir) = CreateTestContext();
        try
        {
            var callerNumber = "09351239876";

            // Call 1 from unknown number
            var call1 = await callManager.IngestCallEventAsync(new CallEventPayload
            {
                UniqueId = "1729230005.100",
                CallerNumber = callerNumber,
                Extension = "101"
            });

            // Call 2 from same unknown number yesterday
            var call2 = await callManager.IngestCallEventAsync(new CallEventPayload
            {
                UniqueId = "1729230005.200",
                CallerNumber = callerNumber,
                Extension = "101"
            });

            call1.IsUnknownCaller.Should().BeTrue();
            call2.IsUnknownCaller.Should().BeTrue();

            // Create supplier
            var party = new Party { DisplayName = "شرکت پخش مخمر تهران" };
            context.Parties.Add(party);
            await context.SaveChangesAsync();

            // User classifies Call 2 to the supplier
            var classified = await callManager.ClassifyUnknownCallAsync(call2.Id, new ClassifyCallRequest
            {
                PartyId = party.Id
            }, "procurement_user");

            classified.IsUnknownCaller.Should().BeFalse();
            classified.PartyId.Should().Be(party.Id);

            // Verify retrospective history preservation on Call 1!
            var call1Updated = await callManager.GetCallByIdAsync(call1.Id);
            call1Updated!.IsUnknownCaller.Should().BeFalse();
            call1Updated.PartyId.Should().Be(party.Id);
            call1Updated.MatchConfidence.Should().Be("Manual");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("../etc/passwd.wav")]
    [InlineData("..\\..\\windows\\system32\\calc.exe")]
    [InlineData("audio/../../secret.wav")]
    [InlineData("recording.exe")]
    [InlineData("recording.sh")]
    [InlineData("test\0bad.wav")]
    public void RecordingService_ValidateRecordingPath_ShouldPreventPathTraversal(string evilPath)
    {
        var (_, _, recordingService, _, tempDir) = CreateTestContext();
        try
        {
            var isValid = recordingService.ValidateRecordingPath(evilPath, out var sanitized);
            isValid.Should().BeFalse("Path traversal input '{0}' must be rejected", evilPath);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RecordingService_ValidateRecordingPath_ShouldAcceptValidFilename()
    {
        var (_, _, recordingService, _, tempDir) = CreateTestContext();
        try
        {
            var isValid = recordingService.ValidateRecordingPath("q-1729230000.100.wav", out var sanitized);
            isValid.Should().BeTrue();
            sanitized.Should().StartWith(tempDir);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ReconcileCdrRecords_ShouldUpdateExistingCall_AndIngestUncapturedRecord()
    {
        var (context, callManager, _, _, tempDir) = CreateTestContext();
        try
        {
            // Existing call from live AMI
            await callManager.IngestCallEventAsync(new CallEventPayload
            {
                UniqueId = "1729230006.100",
                CallerNumber = "09121112233",
                Status = CallStatus.Ringing,
                Extension = "101"
            });

            var cdrBatch = new List<CdrReconciliationDto>
            {
                // Reconciles existing call
                new()
                {
                    UniqueId = "1729230006.100",
                    CallerNumber = "09121112233",
                    DestinationNumber = "101",
                    DurationSeconds = 65,
                    BillableSeconds = 50,
                    Disposition = "ANSWERED",
                    StartTimeUtc = DateTime.UtcNow.AddMinutes(-5),
                    EndTimeUtc = DateTime.UtcNow.AddMinutes(-4)
                },
                // Uncaptured call that was missed during worker restart
                new()
                {
                    UniqueId = "1729230006.200",
                    CallerNumber = "09129990000",
                    DestinationNumber = "102",
                    DurationSeconds = 120,
                    BillableSeconds = 110,
                    Disposition = "ANSWERED",
                    StartTimeUtc = DateTime.UtcNow.AddMinutes(-20),
                    EndTimeUtc = DateTime.UtcNow.AddMinutes(-18)
                }
            };

            var count = await callManager.ReconcileCdrRecordsAsync(cdrBatch, "CDR_Reconciler");
            count.Should().Be(2);

            var reconciled1 = await context.CallRecords.FirstAsync(c => c.UniqueId == "1729230006.100");
            reconciled1.ReconciliationStatus.Should().Be(CallReconciliationStatus.Reconciled);
            reconciled1.DurationSeconds.Should().Be(65);
            reconciled1.BillableSeconds.Should().Be(50);

            var reconciled2 = await context.CallRecords.FirstAsync(c => c.UniqueId == "1729230006.200");
            reconciled2.ReconciliationStatus.Should().Be(CallReconciliationStatus.Reconciled);
            reconciled2.CallerNumber.Should().Be("09129990000");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CallNotes_ShouldAllowNotesAndFollowUps_EvenOnMiscellaneousCalls()
    {
        var (context, callManager, _, _, tempDir) = CreateTestContext();
        try
        {
            var call = await callManager.IngestCallEventAsync(new CallEventPayload
            {
                UniqueId = "1729230007.100",
                CallerNumber = "09120000000",
                Extension = "101"
            });

            var noteRequest = new CreateCallNoteRequest
            {
                NoteText = "تماس در خصوص قیمت آرد ستاره و شرایط تسویه",
                Outcome = "PriceInquiry",
                FollowUpRequired = true,
                FollowUpDueDateUtc = DateTime.UtcNow.AddDays(2),
                FollowUpAssignedTo = "procurement"
            };

            var note = await callManager.AddCallNoteAsync(call.Id, noteRequest, "procurement_user", Guid.NewGuid());

            note.Should().NotBeNull();
            note.Outcome.Should().Be("PriceInquiry");
            note.FollowUpRequired.Should().BeTrue();
            note.FollowUpStatus.Should().Be(FollowUpStatus.Pending);

            var pendingFollowUps = await callManager.GetPendingFollowUpsAsync();
            pendingFollowUps.Should().Contain(n => n.Id == note.Id);

            // Update follow-up status to Completed
            var updated = await callManager.UpdateFollowUpStatusAsync(note.Id, FollowUpStatus.Completed, "procurement_user");
            updated.FollowUpStatus.Should().Be(FollowUpStatus.Completed);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;
using Xunit;

namespace ParkPalace.IntegrationTests;

public class Phase6IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public Phase6IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string uri, string role = "Procurement")
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Dev-Role", role);
        request.Headers.Add("X-Dev-User", $"sahar\\{role.ToLower()}");
        return request;
    }

    [Fact]
    public async Task GetCalls_WithoutCallsViewPermission_ShouldReturn403()
    {
        // Unprivileged or role without CallsView
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/calls");
        req.Headers.Add("X-Dev-Role", "None");
        req.Headers.Add("X-Dev-User", "guest");

        var response = await _client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCalls_WithProcurementRole_ShouldReturn200()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/calls", "Procurement");
        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var calls = await response.Content.ReadFromJsonAsync<List<CallRecordDto>>();
        calls.Should().NotBeNull();
    }

    [Fact]
    public async Task SimulateCall_AndRetrieve_ShouldAppearInCallLog()
    {
        // 1. Simulate an incoming call
        var uniqueNumber = $"0912{Random.Shared.Next(1000000, 9999999)}";
        var simRequest = new SimulateCallRequest
        {
            CallerNumber = uniqueNumber,
            Extension = "101",
            CallType = "incoming",
            DurationSeconds = 20
        };

        using var postReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/calls/simulate", "Admin");
        postReq.Content = JsonContent.Create(simRequest);
        var simResponse = await _client.SendAsync(postReq);
        simResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Fetch call list and verify it is logged
        using var getReq = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/calls?take=10", "Procurement");
        var getResponse = await _client.SendAsync(getReq);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var calls = await getResponse.Content.ReadFromJsonAsync<List<CallRecordDto>>();
        calls.Should().NotBeNull();
        calls.Should().Contain(c => c.CallerNumber == uniqueNumber);
    }

    [Fact]
    public async Task AddCallNote_WithValidData_ShouldSucceed()
    {
        // Seed a call record directly in DB scope
        Guid callId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var call = new CallRecord
            {
                UniqueId = "int-test-" + Guid.NewGuid().ToString("N"),
                LinkedId = "int-test-linked",
                CallerNumber = "09123334455",
                NormalizedCallerNumber = "09123334455",
                DestinationNumber = "101",
                Extension = "101",
                Status = CallStatus.Completed
            };
            db.CallRecords.Add(call);
            await db.SaveChangesAsync();
            callId = call.Id;
        }

        var noteReq = new CreateCallNoteRequest
        {
            NoteText = "تست یادداشت تماس پیگیری سفارش آرد",
            Outcome = "OrderPlacement",
            FollowUpRequired = true,
            FollowUpDueDateUtc = DateTime.UtcNow.AddDays(1),
            FollowUpAssignedTo = "procurement"
        };

        using var postReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/calls/{callId}/notes", "Procurement");
        postReq.Content = JsonContent.Create(noteReq);
        var postRes = await _client.SendAsync(postReq);

        postRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var note = await postRes.Content.ReadFromJsonAsync<CallNoteDto>();
        note.Should().NotBeNull();
        note!.NoteText.Should().Be("تست یادداشت تماس پیگیری سفارش آرد");
        note.Outcome.Should().Be("OrderPlacement");
        note.FollowUpStatus.Should().Be(FollowUpStatus.Pending);
    }

    [Fact]
    public async Task AddCallNote_EmptyNoteText_ShouldReturn400BadRequest()
    {
        using var postReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/calls/{Guid.NewGuid()}/notes", "Procurement");
        postReq.Content = JsonContent.Create(new CreateCallNoteRequest { NoteText = "" });
        var response = await _client.SendAsync(postReq);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ClassifyCall_ShouldLinkSupplierAndReturnUpdatedRecord()
    {
        Guid callId;
        Guid partyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var party = new Party { DisplayName = "تأمین‌کننده روغن تست اینتگریشن" };
            db.Parties.Add(party);

            var call = new CallRecord
            {
                UniqueId = "int-test-classify-" + Guid.NewGuid().ToString("N"),
                CallerNumber = "09120001122",
                NormalizedCallerNumber = "09120001122",
                Extension = "101",
                IsUnknownCaller = true
            };
            db.CallRecords.Add(call);
            await db.SaveChangesAsync();

            callId = call.Id;
            partyId = party.Id;
        }

        var classifyReq = new ClassifyCallRequest { PartyId = partyId };
        using var postReq = CreateAuthenticatedRequest(HttpMethod.Post, $"/api/v1/calls/{callId}/classify", "Procurement");
        postReq.Content = JsonContent.Create(classifyReq);
        var res = await _client.SendAsync(postReq);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await res.Content.ReadFromJsonAsync<CallRecordDto>();
        updated.Should().NotBeNull();
        updated!.IsUnknownCaller.Should().BeFalse();
        updated.PartyId.Should().Be(partyId);
        updated.CallerName.Should().Be("تأمین‌کننده روغن تست اینتگریشن");
    }

    [Fact]
    public async Task StreamRecording_WithoutListenPermission_ShouldReturn403()
    {
        // Warehouse role lacks CallsRecordingsListen permission
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/calls/{Guid.NewGuid()}/recording", "Warehouse");
        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StreamRecording_WhenFileExists_ShouldReturnAudioStream()
    {
        Guid callId;
        string fileName = $"q-int-{Guid.NewGuid():N}.wav";
        using (var scope = _factory.Services.CreateScope())
        {
            var recService = scope.ServiceProvider.GetRequiredService<IRecordingService>();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

            var recDir = recService.GetRecordingsDirectory();
            var fullPath = Path.Combine(recDir, fileName);
            // Write a dummy wav file
            File.WriteAllBytes(fullPath, new byte[64]);

            var call = new CallRecord
            {
                UniqueId = "int-test-rec-" + Guid.NewGuid().ToString("N"),
                CallerNumber = "09121112233",
                NormalizedCallerNumber = "09121112233",
                Extension = "101",
                RecordingFileName = fileName,
                RecordingFilePath = fullPath,
                HasRecording = true
            };
            db.CallRecords.Add(call);
            await db.SaveChangesAsync();
            callId = call.Id;
        }

        using var req = CreateAuthenticatedRequest(HttpMethod.Get, $"/api/v1/calls/{callId}/recording", "Procurement");
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("audio/wav");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().Be(64);
    }

    [Fact]
    public async Task ReconcileCdr_WithManagerRole_ShouldReconcileBatch()
    {
        var cdrBatch = new List<CdrReconciliationDto>
        {
            new()
            {
                UniqueId = "cdr-int-" + Guid.NewGuid().ToString("N"),
                CallerNumber = "09124445566",
                DestinationNumber = "102",
                DurationSeconds = 45,
                BillableSeconds = 40,
                Disposition = "ANSWERED",
                StartTimeUtc = DateTime.UtcNow.AddMinutes(-10),
                EndTimeUtc = DateTime.UtcNow.AddMinutes(-9)
            }
        };

        using var postReq = CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/calls/reconcile-cdr", "Manager");
        postReq.Content = JsonContent.Create(cdrBatch);
        var res = await _client.SendAsync(postReq);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUserExtensions_WithProcurement_ShouldReturnSeededExtensions()
    {
        using var req = CreateAuthenticatedRequest(HttpMethod.Get, "/api/v1/calls/extensions", "Procurement");
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var exts = await res.Content.ReadFromJsonAsync<List<UserExtensionDto>>();
        exts.Should().NotBeNull();
    }
}

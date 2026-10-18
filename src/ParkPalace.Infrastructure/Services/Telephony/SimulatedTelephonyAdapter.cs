using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Telephony;

public class SimulatedTelephonyAdapter : ITelephonyAdapter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SimulatedTelephonyAdapter> _logger;
    private bool _isConnected;
    private readonly string _recordingsPath;

    public event Func<CallEventPayload, Task>? OnCallEventReceived;

    public bool IsConnected => _isConnected;
    public string AdapterType => "Simulated";

    public SimulatedTelephonyAdapter(IConfiguration configuration, ILogger<SimulatedTelephonyAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _recordingsPath = _configuration["Telephony:RecordingsPath"] 
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recordings");

        if (!Directory.Exists(_recordingsPath))
        {
            Directory.CreateDirectory(_recordingsPath);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _isConnected = true;
        _logger.LogInformation("SimulatedTelephonyAdapter started and connected (Deterministic PBX Mock)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _isConnected = false;
        _logger.LogInformation("SimulatedTelephonyAdapter stopped");
        return Task.CompletedTask;
    }

    public async Task SimulateCallAsync(SimulateCallRequest request)
    {
        if (!_isConnected)
        {
            _logger.LogWarning("Cannot simulate call: Telephony adapter is not connected.");
            throw new InvalidOperationException("Telephony adapter is disconnected.");
        }

        var uniqueId = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{Random.Shared.Next(100, 999)}";
        var linkedId = uniqueId;
        var ext = string.IsNullOrWhiteSpace(request.Extension) ? "101" : request.Extension.Trim();
        var callType = request.CallType?.ToLowerInvariant() ?? "incoming";
        var caller = string.IsNullOrWhiteSpace(request.CallerNumber) ? "09121112233" : request.CallerNumber.Trim();
        var dest = string.IsNullOrWhiteSpace(request.DestinationNumber) ? ext : request.DestinationNumber.Trim();
        var callerName = request.CallerName ?? "Unknown";

        _logger.LogInformation("Starting call simulation: Type={CallType}, UniqueId={UniqueId}, Caller={Caller}, Ext={Ext}",
            callType, uniqueId, caller, ext);

        if (callType == "outgoing")
        {
            // Outgoing call flow
            var payload = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = linkedId,
                Direction = CallDirection.Outgoing,
                Status = CallStatus.Ringing,
                CallerNumber = ext,
                CallerName = "ParkPalace Extension " + ext,
                DestinationNumber = caller,
                Extension = ext,
                Channel = $"SIP/{ext}-00000001",
                TimestampUtc = DateTime.UtcNow
            };
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);

            // Answered & completed
            payload.Status = CallStatus.Completed;
            payload.DurationSeconds = request.DurationSeconds > 0 ? request.DurationSeconds : 25;
            payload.BillableSeconds = payload.DurationSeconds;
            payload.DisconnectionCause = "NORMAL_CLEARING";
            var recFile = CreateMockRecordingFile(uniqueId);
            payload.RecordingFileName = recFile;

            if (OnCallEventReceived != null) await OnCallEventReceived(payload);
        }
        else if (callType == "missed")
        {
            // Ringing then Missed
            var payload = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = linkedId,
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = caller,
                CallerName = callerName,
                DestinationNumber = dest,
                Extension = ext,
                Channel = $"SIP/trunk-00000001",
                DestinationChannel = $"SIP/{ext}-00000002",
                TimestampUtc = DateTime.UtcNow
            };
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);

            payload.Status = CallStatus.Missed;
            payload.DurationSeconds = 15;
            payload.BillableSeconds = 0;
            payload.DisconnectionCause = "NO ANSWER";
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);
        }
        else if (callType == "busy")
        {
            // Ringing then Busy
            var payload = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = linkedId,
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = caller,
                CallerName = callerName,
                DestinationNumber = dest,
                Extension = ext,
                Channel = $"SIP/trunk-00000001",
                TimestampUtc = DateTime.UtcNow
            };
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);

            payload.Status = CallStatus.Busy;
            payload.DurationSeconds = 5;
            payload.BillableSeconds = 0;
            payload.DisconnectionCause = "BUSY";
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);
        }
        else if (callType == "transfer")
        {
            // First leg: Ringing -> Answered on ext 101
            var leg1 = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = linkedId,
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = caller,
                CallerName = callerName,
                DestinationNumber = dest,
                Extension = ext,
                Channel = $"SIP/trunk-00000001",
                TimestampUtc = DateTime.UtcNow
            };
            if (OnCallEventReceived != null) await OnCallEventReceived(leg1);

            leg1.Status = CallStatus.Answered;
            if (OnCallEventReceived != null) await OnCallEventReceived(leg1);

            // Transferred: leg1 marked transferred
            leg1.Status = CallStatus.Transferred;
            if (OnCallEventReceived != null) await OnCallEventReceived(leg1);

            // Second leg: new UniqueId, same LinkedId, new Extension (e.g. 102)
            var targetExt = ext == "101" ? "102" : "101";
            var leg2UniqueId = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{Random.Shared.Next(100, 999)}";
            var leg2 = new CallEventPayload
            {
                UniqueId = leg2UniqueId,
                LinkedId = linkedId, // LinkedId preserves the correlation
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = caller,
                CallerName = callerName,
                DestinationNumber = targetExt,
                Extension = targetExt,
                Channel = $"Local/{targetExt}@from-internal",
                TimestampUtc = DateTime.UtcNow
            };
            if (OnCallEventReceived != null) await OnCallEventReceived(leg2);

            leg2.Status = CallStatus.Completed;
            leg2.DurationSeconds = request.DurationSeconds > 0 ? request.DurationSeconds : 40;
            leg2.BillableSeconds = leg2.DurationSeconds;
            leg2.DisconnectionCause = "NORMAL_CLEARING";
            var recFile = CreateMockRecordingFile(leg2UniqueId);
            leg2.RecordingFileName = recFile;
            if (OnCallEventReceived != null) await OnCallEventReceived(leg2);
        }
        else // default: incoming answered & completed
        {
            var payload = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = linkedId,
                Direction = CallDirection.Incoming,
                Status = CallStatus.Ringing,
                CallerNumber = caller,
                CallerName = callerName,
                DestinationNumber = dest,
                Extension = ext,
                Channel = $"SIP/trunk-00000001",
                DestinationChannel = $"SIP/{ext}-00000002",
                TimestampUtc = DateTime.UtcNow
            };
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);

            payload.Status = CallStatus.Answered;
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);

            payload.Status = CallStatus.Completed;
            payload.DurationSeconds = request.DurationSeconds > 0 ? request.DurationSeconds : 35;
            payload.BillableSeconds = payload.DurationSeconds;
            payload.DisconnectionCause = "NORMAL_CLEARING";
            var recFile = CreateMockRecordingFile(uniqueId);
            payload.RecordingFileName = recFile;
            if (OnCallEventReceived != null) await OnCallEventReceived(payload);
        }
    }

    private string CreateMockRecordingFile(string uniqueId)
    {
        try
        {
            var fileName = $"q-{uniqueId}.wav";
            var fullPath = Path.Combine(_recordingsPath, fileName);
            if (!File.Exists(fullPath))
            {
                // Write a minimal valid standard WAV header (44 bytes PCM, 8kHz, 16bit, mono, silence)
                byte[] wavHeader = new byte[44];
                // "RIFF"
                wavHeader[0] = (byte)'R'; wavHeader[1] = (byte)'I'; wavHeader[2] = (byte)'F'; wavHeader[3] = (byte)'F';
                // ChunkSize (36 + data size: 36 + 1600 = 1636)
                int dataSize = 1600; // ~0.1s silence
                int chunkSize = 36 + dataSize;
                BitConverter.GetBytes(chunkSize).CopyTo(wavHeader, 4);
                // "WAVE"
                wavHeader[8] = (byte)'W'; wavHeader[9] = (byte)'A'; wavHeader[10] = (byte)'V'; wavHeader[11] = (byte)'E';
                // "fmt "
                wavHeader[12] = (byte)'f'; wavHeader[13] = (byte)'m'; wavHeader[14] = (byte)'t'; wavHeader[15] = (byte)' ';
                BitConverter.GetBytes(16).CopyTo(wavHeader, 16); // subchunk1 size
                BitConverter.GetBytes((short)1).CopyTo(wavHeader, 20); // audio format PCM
                BitConverter.GetBytes((short)1).CopyTo(wavHeader, 22); // mono
                BitConverter.GetBytes(8000).CopyTo(wavHeader, 24); // sample rate 8000
                BitConverter.GetBytes(16000).CopyTo(wavHeader, 28); // byte rate
                BitConverter.GetBytes((short)2).CopyTo(wavHeader, 32); // block align
                BitConverter.GetBytes((short)16).CopyTo(wavHeader, 34); // bits per sample
                // "data"
                wavHeader[36] = (byte)'d'; wavHeader[37] = (byte)'a'; wavHeader[38] = (byte)'t'; wavHeader[39] = (byte)'a';
                BitConverter.GetBytes(dataSize).CopyTo(wavHeader, 40);

                byte[] data = new byte[dataSize]; // zeros = silence
                using var fs = File.Create(fullPath);
                fs.Write(wavHeader, 0, wavHeader.Length);
                fs.Write(data, 0, data.Length);
            }
            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create mock recording file for {UniqueId}", uniqueId);
            return $"q-{uniqueId}.wav";
        }
    }
}

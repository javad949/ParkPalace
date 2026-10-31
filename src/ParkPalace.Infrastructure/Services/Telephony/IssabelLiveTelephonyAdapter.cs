using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Telephony;

public class IssabelLiveTelephonyAdapter : ITelephonyAdapter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IssabelLiveTelephonyAdapter> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _isConnected;
    private CancellationTokenSource? _cts;

    public event Func<CallEventPayload, Task>? OnCallEventReceived;
    public bool IsConnected => _isConnected;
    public string AdapterType => "IssabelAMI";

    public IssabelLiveTelephonyAdapter(IConfiguration configuration, ILogger<IssabelLiveTelephonyAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var host = _configuration["Telephony:AmiHost"] 
            ?? _configuration["Integrations:Issabel:Host"] 
            ?? "192.168.1.245";
        var port = int.TryParse(_configuration["Telephony:AmiPort"], out var p) ? p : 5038;
        var user = _configuration["Telephony:AmiUser"] ?? "crm_ami";
        var pass = _configuration["Telephony:AmiSecret"] ?? "OR@1405.ami";

        _logger.LogInformation("Connecting to Issabel Asterisk AMI at {Host}:{Port} as user '{User}'...", host, port, user);

        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _tcpClient = new TcpClient();
            
            // Connect with 5-second timeout
            var connectTask = _tcpClient.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(5000, cancellationToken)) != connectTask)
            {
                _tcpClient.Close();
                throw new TimeoutException($"Connection to Asterisk AMI at {host}:{port} timed out.");
            }

            _stream = _tcpClient.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };

            // Read banner e.g. "Asterisk Call Manager/5.0.5"
            var banner = await _reader.ReadLineAsync();
            _logger.LogInformation("Asterisk AMI Banner: {Banner}", banner);

            // Send Login Action
            await _writer.WriteLineAsync("Action: Login");
            await _writer.WriteLineAsync($"Username: {user}");
            await _writer.WriteLineAsync($"Secret: {pass}");
            await _writer.WriteLineAsync("Events: on");
            await _writer.WriteLineAsync();

            // Read login response headers
            var loginSuccess = false;
            var responseLine = await _reader.ReadLineAsync();
            var messageLine = await _reader.ReadLineAsync();
            _logger.LogInformation("AMI Login response: {Resp} | {Msg}", responseLine, messageLine);

            if (responseLine != null && responseLine.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                loginSuccess = true;
            }
            else
            {
                _logger.LogWarning("AMI Login rejected by Asterisk: {Resp} - {Msg}", responseLine, messageLine);
            }

            // Drain remaining login response headers until blank line
            string? drainLine;
            while ((drainLine = await _reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(drainLine)) break;
            }

            _isConnected = loginSuccess;
            if (_isConnected)
            {
                _logger.LogInformation("Issabel AMI connected and authenticated successfully. Starting live event reader...");
                // Start background event read loop
                _ = Task.Run(() => ReadAmiEventsAsync(_cts.Token), _cts.Token);
                // Start background keepalive ping
                _ = Task.Run(() => KeepAliveAsync(_cts.Token), _cts.Token);
            }
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogWarning("Could not establish live AMI connection to {Host}:{Port}: {Message}", host, port, ex.Message);
        }
    }

    private async Task KeepAliveAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isConnected)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                if (_isConnected && _writer != null)
                {
                    await _writer.WriteLineAsync("Action: Ping");
                    await _writer.WriteLineAsync();
                }
            }
            catch
            {
                break;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _isConnected = false;
        try
        {
            _cts?.Cancel();
            _writer?.WriteLine("Action: Logoff\r\n");
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error closing AMI socket");
        }
        _logger.LogInformation("Issabel AMI adapter stopped.");
        return Task.CompletedTask;
    }

    public async Task SimulateCallAsync(SimulateCallRequest request)
    {
        var ext = string.IsNullOrWhiteSpace(request.Extension) ? "222" : request.Extension.Trim();
        var caller = string.IsNullOrWhiteSpace(request.CallerNumber) ? "09121234567" : request.CallerNumber.Trim();
        var callerName = request.CallerName ?? "تأمین‌کننده تجهیزات صنعتی صبا";
        var uniqueId = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{Random.Shared.Next(100, 999)}";

        _logger.LogInformation("Simulating incoming PBX call to extension {Ext} from {Caller} ({Name})...", ext, caller, callerName);

        var payload = new CallEventPayload
        {
            UniqueId = uniqueId,
            LinkedId = uniqueId,
            Direction = CallDirection.Incoming,
            Status = CallStatus.Ringing,
            CallerNumber = caller,
            CallerName = callerName,
            DestinationNumber = ext,
            Extension = ext,
            Channel = $"SIP/{ext}-00000001",
            TimestampUtc = DateTime.UtcNow
        };

        if (OnCallEventReceived != null)
        {
            await OnCallEventReceived(payload);
        }
    }

    public async Task<List<AvailableExtensionDto>> GetAvailableExtensionsAsync(CancellationToken cancellationToken = default)
    {
        var host = _configuration["Telephony:AmiHost"] 
            ?? _configuration["Integrations:Issabel:Host"] 
            ?? "192.168.1.245";
        var port = int.TryParse(_configuration["Telephony:AmiPort"], out var p) ? p : 5038;
        var user = _configuration["Telephony:AmiUser"] ?? "crm_ami";
        var pass = _configuration["Telephony:AmiSecret"] ?? "OR@1405.ami";

        // 1. Direct dynamic query to Issabel Asterisk AMI database
        var directList = await QueryDirectExtensionsFromAmiAsync(host, port, user, pass, cancellationToken);
        if (directList.Count > 0)
        {
            _logger.LogInformation("Successfully retrieved {Count} PBX extensions directly from Issabel PBX.", directList.Count);
            return directList;
        }

        // 2. Fallback to discovered extension catalog
        return GetFallbackExtensions();
    }

    private async Task<List<AvailableExtensionDto>> QueryDirectExtensionsFromAmiAsync(string host, int port, string user, string pass, CancellationToken cancellationToken)
    {
        var list = new List<AvailableExtensionDto>();
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3500, cancellationToken)) != connectTask)
            {
                return list;
            }

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

            await reader.ReadLineAsync(); // Banner

            await writer.WriteLineAsync("Action: Login");
            await writer.WriteLineAsync($"Username: {user}");
            await writer.WriteLineAsync($"Secret: {pass}");
            await writer.WriteLineAsync("Events: off");
            await writer.WriteLineAsync();

            string? l;
            while ((l = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(l)) break;
            }

            await writer.WriteLineAsync("Action: Command");
            await writer.WriteLineAsync("Command: database show AMPUSER");
            await writer.WriteLineAsync();

            while ((l = await reader.ReadLineAsync()) != null)
            {
                if (l.StartsWith("--END COMMAND--", StringComparison.OrdinalIgnoreCase)) break;

                // Format: Output: /AMPUSER/222/cidname                              : Bazargani
                var match = Regex.Match(l, @"/AMPUSER/(\d{3,4})/cidname\s*:\s*(.+)$");
                if (match.Success)
                {
                    var ext = match.Groups[1].Value.Trim();
                    var cidName = match.Groups[2].Value.Trim();
                    var persianDesc = MapCidNameToPersian(ext, cidName);

                    list.Add(new AvailableExtensionDto
                    {
                        Extension = ext,
                        DisplayName = $"داخلی {ext} ({persianDesc})",
                        DeviceType = "SIP",
                        Status = "Available"
                    });
                }
            }

            await writer.WriteLineAsync("Action: Logoff\r\n");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Direct query to Asterisk AMI for extensions failed: {Message}", ex.Message);
        }

        return list.OrderBy(x => x.Extension).ToList();
    }

    private static string MapCidNameToPersian(string ext, string cidName)
    {
        return cidName switch
        {
            "Bazargani" => "مسئول تدارکات / بازرگانی",
            "Anbar" => "سرپرست انبار",
            "Modir" => "مدیر ناظر",
            "Daftar" => "دفتر مرکزی",
            "Cafee" => "کافی‌شاپ",
            "Rahmani" => "رحمانی",
            "Amiri" => "امیری",
            "Bagheri" => "باقری",
            "Tolid" => "واحد تولید",
            "Alipour" => "علی‌پور",
            "Mohammadi" => "محمدی",
            "ABA-Kitchen" => "آشپزخانه ABA",
            "K-Khoshk" => "آشپزخانه خشک",
            "K-Tar" => "آشپزخانه تر",
            "Kitchen" => "آشپزخانه",
            "Sefaresh" => "واحد سفارشات",
            "Ms-Aram" => "خانم آرام",
            _ => string.IsNullOrWhiteSpace(cidName) ? $"داخلی {ext}" : cidName
        };
    }

    private static List<AvailableExtensionDto> GetFallbackExtensions()
    {
        return new List<AvailableExtensionDto>
        {
            new AvailableExtensionDto { Extension = "200", DisplayName = "داخلی ۲۰۰ (دفتر مرکزی)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "201", DisplayName = "داخلی ۲۰۱ (کافی‌شاپ)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "202", DisplayName = "داخلی ۲۰۲ (رحمانی)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "220", DisplayName = "داخلی ۲۲۰ (امیری)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "221", DisplayName = "داخلی ۲۲۱ (باقری)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "222", DisplayName = "داخلی ۲۲۲ (مسئول تدارکات / بازرگانی)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "223", DisplayName = "داخلی ۲۲۳ (واحد تولید)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "224", DisplayName = "داخلی ۲۲۴ (علی‌پور)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "225", DisplayName = "داخلی ۲۲۵ (محمدی)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "226", DisplayName = "داخلی ۲۲۶ (آشپزخانه ABA)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "227", DisplayName = "داخلی ۲۲۷ (آشپزخانه خشک)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "228", DisplayName = "داخلی ۲۲۸ (آشپزخانه تر)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "229", DisplayName = "داخلی ۲۲۹ (آشپزخانه)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "230", DisplayName = "داخلی ۲۳۰ (سرپرست انبار)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "240", DisplayName = "داخلی ۲۴۰ (واحد سفارشات)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "250", DisplayName = "داخلی ۲۵۰ (مدیر ناظر)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "251", DisplayName = "داخلی ۲۵۱ (مدیر ناظر)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "300", DisplayName = "داخلی ۳۰۰ (خانم آرام)", DeviceType = "SIP", Status = "Available" },
            new AvailableExtensionDto { Extension = "333", DisplayName = "داخلی ۳۳۳", DeviceType = "SIP", Status = "Available" }
        };
    }

    private async Task ReadAmiEventsAsync(CancellationToken token)
    {
        try
        {
            var eventHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (!token.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync();
                if (line == null) break;

                line = line.Trim();

                if (string.IsNullOrEmpty(line))
                {
                    // End of event block — process headers
                    if (eventHeaders.TryGetValue("Event", out var eventName))
                    {
                        await ProcessAmiEventAsync(eventName, eventHeaders);
                    }
                    eventHeaders.Clear();
                    continue;
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line.Substring(0, colonIdx).Trim();
                    var val = line.Substring(colonIdx + 1).Trim();
                    eventHeaders[key] = val;
                }
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            _logger.LogWarning("AMI read loop disconnected: {Message}", ex.Message);
        }
        finally
        {
            _isConnected = false;
        }
    }

    private async Task ProcessAmiEventAsync(string eventName, Dictionary<string, string> headers)
    {
        try
        {
            // Only handle telephony call progression events
            if (!eventName.Equals("DialBegin", StringComparison.OrdinalIgnoreCase) &&
                !eventName.Equals("DialState", StringComparison.OrdinalIgnoreCase) &&
                !eventName.Equals("Newchannel", StringComparison.OrdinalIgnoreCase) &&
                !eventName.Equals("Newstate", StringComparison.OrdinalIgnoreCase) &&
                !eventName.Equals("BridgeEnter", StringComparison.OrdinalIgnoreCase) &&
                !eventName.Equals("Hangup", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _logger.LogDebug("[IssabelAMI] Event received: {EventName}", eventName);

            var callerNum = headers.GetValueOrDefault("CallerIDNum") 
                ?? headers.GetValueOrDefault("CallerID") 
                ?? headers.GetValueOrDefault("ConnectedLineNum") 
                ?? string.Empty;

            var callerName = headers.GetValueOrDefault("CallerIDName") ?? headers.GetValueOrDefault("ConnectedLineName");

            var dest = headers.GetValueOrDefault("DestCallerIDNum") 
                ?? headers.GetValueOrDefault("DialString") 
                ?? headers.GetValueOrDefault("Exten") 
                ?? headers.GetValueOrDefault("ConnectedLineNum")
                ?? string.Empty;

            var channel = headers.GetValueOrDefault("Channel") ?? string.Empty;
            var destChannel = headers.GetValueOrDefault("Destination") ?? headers.GetValueOrDefault("DestChannel") ?? string.Empty;
            var uniqueId = headers.GetValueOrDefault("Uniqueid") ?? Guid.NewGuid().ToString("N");
            var linkedId = headers.GetValueOrDefault("Linkedid") ?? uniqueId;

            // Extract extension number from dest or channels
            string? targetExt = null;
            if (Regex.IsMatch(dest, @"^\d{3,4}$"))
            {
                targetExt = dest;
            }
            else
            {
                var m = Regex.Match(destChannel, @"(?:SIP|PJSIP)/(\d{3,4})");
                if (m.Success)
                {
                    targetExt = m.Groups[1].Value;
                }
                else
                {
                    var m2 = Regex.Match(channel, @"(?:SIP|PJSIP)/(\d{3,4})");
                    if (m2.Success)
                    {
                        targetExt = m2.Groups[1].Value;
                    }
                    else
                    {
                        var m3 = Regex.Match(dest, @"(?:SIP|PJSIP|)/?(\d{3,4})");
                        if (m3.Success)
                        {
                            targetExt = m3.Groups[1].Value;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(targetExt))
            {
                return;
            }

            var isOutgoing = callerNum.Length <= 4 && dest.Length > 4;
            var direction = isOutgoing ? CallDirection.Outgoing : CallDirection.Incoming;

            CallStatus status = CallStatus.Ringing;
            if (eventName.Equals("BridgeEnter", StringComparison.OrdinalIgnoreCase) ||
                headers.GetValueOrDefault("ChannelStateDesc")?.Equals("Up", StringComparison.OrdinalIgnoreCase) == true)
            {
                status = CallStatus.Answered;
            }
            else if (eventName.Equals("Hangup", StringComparison.OrdinalIgnoreCase))
            {
                status = CallStatus.Completed;
            }

            var payload = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = linkedId,
                Direction = direction,
                Status = status,
                CallerNumber = callerNum,
                CallerName = callerName,
                DestinationNumber = dest,
                Extension = targetExt,
                Channel = channel,
                DestinationChannel = destChannel,
                TimestampUtc = DateTime.UtcNow,
                DisconnectionCause = headers.GetValueOrDefault("Cause-txt")
            };

            _logger.LogInformation("[IssabelAMI] Dispatching call event: Status={Status}, Caller={Caller}, Ext={Ext}, UniqueId={UniqueId}",
                status, callerNum, targetExt, uniqueId);

            if (OnCallEventReceived != null)
            {
                await OnCallEventReceived(payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[IssabelAMI] Error parsing event {EventName}", eventName);
        }
    }
}

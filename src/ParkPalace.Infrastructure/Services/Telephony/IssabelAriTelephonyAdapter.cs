using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Infrastructure.Services.Telephony;

/// <summary>
/// Asterisk REST Interface (ARI) Telephony Adapter for Issabel PBX.
/// Connects to Issabel Asterisk via HTTP REST and WebSockets (ws://host:8088/ari/events).
/// Provides automatic resilient fallback to AMI (port 5038) if ARI is disabled or unreachable.
/// </summary>
public class IssabelAriTelephonyAdapter : ITelephonyAdapter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IssabelAriTelephonyAdapter> _logger;
    private readonly HttpClient _httpClient;
    
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private bool _isConnected;
    private string _activeAdapterType = "IssabelARI";

    // AMI Fallback handles
    private TcpClient? _amiTcpClient;
    private StreamReader? _amiReader;
    private StreamWriter? _amiWriter;

    public event Func<CallEventPayload, Task>? OnCallEventReceived;
    public bool IsConnected => _isConnected;
    public string AdapterType => _activeAdapterType;

    public IssabelAriTelephonyAdapter(IConfiguration configuration, ILogger<IssabelAriTelephonyAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var host = _configuration["Telephony:AriHost"] 
            ?? _configuration["Telephony:AmiHost"] 
            ?? _configuration["Integrations:Issabel:Host"] 
            ?? "192.168.1.245";
        var port = int.TryParse(_configuration["Telephony:AriPort"], out var p) ? p : 8088;
        var user = _configuration["Telephony:AriUser"] ?? _configuration["Telephony:AmiUser"] ?? "crm_ami";
        var pass = _configuration["Telephony:AriSecret"] ?? _configuration["Telephony:AmiSecret"] ?? "OR@1405.ami";
        var appName = _configuration["Telephony:AriAppName"] ?? "parkpalace";

        _logger.LogInformation("Initializing Issabel ARI Adapter against {Host}:{Port} (App: {App}, User: {User})...",
            host, port, appName, user);

        // Attempt 1: Connect via Asterisk REST Interface (ARI) WebSocket
        var ariSuccess = await TryConnectAriWebSocketAsync(host, port, user, pass, appName, _cts.Token);
        if (ariSuccess)
        {
            _isConnected = true;
            _activeAdapterType = "IssabelARI";
            _logger.LogInformation("Successfully connected to Asterisk ARI at ws://{Host}:{Port}/ari/events", host, port);
            _ = Task.Run(() => ReadAriWebSocketEventsAsync(_cts.Token), _cts.Token);
            return;
        }

        // Attempt 2: Resilient Fallback to Asterisk Manager Interface (AMI port 5038)
        _logger.LogWarning("ARI connection unviable or disabled. Initiating resilient fallback to AMI (port 5038)...");
        var amiPort = int.TryParse(_configuration["Telephony:AmiPort"], out var ap) ? ap : 5038;
        var amiSuccess = await TryConnectAmiFallbackAsync(host, amiPort, user, pass, _cts.Token);
        if (amiSuccess)
        {
            _isConnected = true;
            _activeAdapterType = "IssabelAMI (Fallback)";
            _logger.LogInformation("Successfully connected to Issabel Asterisk AMI at {Host}:{Port}", host, amiPort);
            _ = Task.Run(() => ReadAmiEventsAsync(_cts.Token), _cts.Token);
            _ = Task.Run(() => KeepAliveAmiAsync(_cts.Token), _cts.Token);
        }
        else
        {
            _isConnected = false;
            _logger.LogWarning("Could not establish connection to Issabel Asterisk via ARI or AMI.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _isConnected = false;
        try
        {
            _cts?.Cancel();

            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", cancellationToken);
                }
                _webSocket.Dispose();
                _webSocket = null;
            }

            if (_amiTcpClient != null)
            {
                _amiReader?.Dispose();
                _amiWriter?.Dispose();
                _amiTcpClient.Close();
                _amiTcpClient.Dispose();
                _amiTcpClient = null;
            }

            _logger.LogInformation("Issabel Telephony Adapter stopped cleanly.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping Issabel Telephony Adapter.");
        }
    }

    #region ARI WebSocket Engine

    private async Task<bool> TryConnectAriWebSocketAsync(string host, int port, string user, string pass, string appName, CancellationToken token)
    {
        try
        {
            var wsUri = new Uri($"ws://{host}:{port}/ari/events?api_key={Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass)}&app={Uri.EscapeDataString(appName)}&subscribeAll=true");
            
            _webSocket = new ClientWebSocket();
            var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
            _webSocket.Options.SetRequestHeader("Authorization", $"Basic {Convert.ToBase64String(authBytes)}");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            await _webSocket.ConnectAsync(wsUri, linkedCts.Token);
            return _webSocket.State == WebSocketState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ARI WebSocket connection failed to {Host}:{Port}: {Message}", host, port, ex.Message);
            _webSocket?.Dispose();
            _webSocket = null;
            return false;
        }
    }

    private async Task ReadAriWebSocketEventsAsync(CancellationToken token)
    {
        var buffer = new byte[8192];
        var ms = new MemoryStream();

        while (!token.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("Asterisk ARI WebSocket received Close message.");
                        _isConnected = false;
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close", token);
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var jsonStr = Encoding.UTF8.GetString(ms.ToArray());
                await ParseAndDispatchAriJsonEventAsync(jsonStr);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in Asterisk ARI event stream reader.");
                await Task.Delay(2000, token);
            }
        }
    }

    private async Task ParseAndDispatchAriJsonEventAsync(string jsonStr)
    {
        if (string.IsNullOrWhiteSpace(jsonStr)) return;

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;

            var eventType = typeProp.GetString();
            _logger.LogDebug("[IssabelARI] Event received: {EventType}", eventType);

            // Channel related events
            JsonElement channelElem = default;
            var hasChannel = root.TryGetProperty("channel", out channelElem);

            if (!hasChannel && root.TryGetProperty("peer", out var peerElem))
            {
                channelElem = peerElem;
                hasChannel = true;
            }

            if (!hasChannel) return;

            var uniqueId = channelElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            var channelName = channelElem.TryGetProperty("name", out var cnProp) ? cnProp.GetString() ?? string.Empty : string.Empty;

            var callerNum = string.Empty;
            var callerName = string.Empty;
            if (channelElem.TryGetProperty("caller", out var callerElem))
            {
                callerNum = callerElem.TryGetProperty("number", out var numProp) ? numProp.GetString() ?? string.Empty : string.Empty;
                callerName = callerElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
            }

            var dest = string.Empty;
            if (channelElem.TryGetProperty("dialplan", out var dpElem))
            {
                dest = dpElem.TryGetProperty("exten", out var extProp) ? extProp.GetString() ?? string.Empty : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(dest) && channelElem.TryGetProperty("connected", out var connElem))
            {
                dest = connElem.TryGetProperty("number", out var cnumProp) ? cnumProp.GetString() ?? string.Empty : string.Empty;
            }

            // Extract internal extension
            var targetExt = ExtractExtension(dest, channelName);
            if (string.IsNullOrWhiteSpace(targetExt) && !string.IsNullOrWhiteSpace(callerNum) && callerNum.Length <= 4)
            {
                targetExt = callerNum;
            }

            if (string.IsNullOrWhiteSpace(targetExt)) return;

            var isOutgoing = callerNum.Length <= 4 && dest.Length > 4;
            var direction = isOutgoing ? CallDirection.Outgoing : CallDirection.Incoming;

            CallStatus status = CallStatus.Ringing;
            var channelState = channelElem.TryGetProperty("state", out var stProp) ? stProp.GetString() : string.Empty;

            if (eventType == "ChannelStateChange" && string.Equals(channelState, "Up", StringComparison.OrdinalIgnoreCase))
            {
                status = CallStatus.Answered;
            }
            else if (eventType == "ChannelEnteredBridge" || eventType == "BridgeEnter")
            {
                status = CallStatus.Answered;
            }
            else if (eventType == "ChannelDestroyed" || eventType == "StasisEnd")
            {
                status = CallStatus.Completed;
            }
            else if (eventType == "Dial")
            {
                var dialStatus = root.TryGetProperty("dialstatus", out var dsProp) ? dsProp.GetString() : string.Empty;
                if (string.Equals(dialStatus, "ANSWER", StringComparison.OrdinalIgnoreCase))
                {
                    status = CallStatus.Answered;
                }
            }

            var payload = new CallEventPayload
            {
                UniqueId = uniqueId,
                LinkedId = uniqueId,
                Direction = direction,
                Status = status,
                CallerNumber = callerNum,
                CallerName = callerName,
                DestinationNumber = dest,
                Extension = targetExt,
                Channel = channelName,
                DestinationChannel = string.Empty,
                TimestampUtc = DateTime.UtcNow,
                DisconnectionCause = root.TryGetProperty("cause_txt", out var causeProp) ? causeProp.GetString() : null
            };

            _logger.LogInformation("[IssabelARI] Event: Type={EventType}, Status={Status}, Ext={Ext}, Caller={Caller}",
                eventType, status, targetExt, callerNum);

            if (OnCallEventReceived != null)
            {
                await OnCallEventReceived(payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing ARI JSON event");
        }
    }

    #endregion

    #region Extensions Query (ARI REST + AMI Fallback)

    public async Task<List<AvailableExtensionDto>> GetAvailableExtensionsAsync(CancellationToken cancellationToken = default)
    {
        var host = _configuration["Telephony:AriHost"] 
            ?? _configuration["Telephony:AmiHost"] 
            ?? _configuration["Integrations:Issabel:Host"] 
            ?? "192.168.1.245";
        var port = int.TryParse(_configuration["Telephony:AriPort"], out var p) ? p : 8088;
        var user = _configuration["Telephony:AriUser"] ?? _configuration["Telephony:AmiUser"] ?? "crm_ami";
        var pass = _configuration["Telephony:AriSecret"] ?? _configuration["Telephony:AmiSecret"] ?? "OR@1405.ami";

        // 1. Try querying ARI endpoints REST API
        var ariList = await QueryExtensionsFromAriRestAsync(host, port, user, pass, cancellationToken);
        if (ariList.Count > 0)
        {
            _logger.LogInformation("Retrieved {Count} extensions from Issabel Asterisk via ARI REST.", ariList.Count);
            return ariList;
        }

        // 2. Try querying AMI database
        var amiPort = int.TryParse(_configuration["Telephony:AmiPort"], out var ap) ? ap : 5038;
        var amiList = await QueryExtensionsFromAmiDatabaseAsync(host, amiPort, user, pass, cancellationToken);
        if (amiList.Count > 0)
        {
            _logger.LogInformation("Retrieved {Count} extensions from Issabel Asterisk via AMI database.", amiList.Count);
            return amiList;
        }

        // 3. Fallback catalog
        return GetFallbackExtensions();
    }

    private async Task<List<AvailableExtensionDto>> QueryExtensionsFromAriRestAsync(string host, int port, string user, string pass, CancellationToken token)
    {
        var list = new List<AvailableExtensionDto>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"http://{host}:{port}/ari/endpoints");
            var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            using var res = await _httpClient.SendAsync(req, token);
            if (!res.IsSuccessStatusCode)
            {
                return list;
            }

            var content = await res.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(content);
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                var resource = elem.TryGetProperty("resource", out var rProp) ? rProp.GetString() : null;
                var tech = elem.TryGetProperty("technology", out var tProp) ? tProp.GetString() : "SIP";
                var state = elem.TryGetProperty("state", out var sProp) ? sProp.GetString() : "unknown";

                if (!string.IsNullOrWhiteSpace(resource) && Regex.IsMatch(resource, @"^\d{3,4}$"))
                {
                    list.Add(new AvailableExtensionDto
                    {
                        Extension = resource,
                        DisplayName = $"داخلی {resource}",
                        DeviceType = tech ?? "SIP",
                        Status = state ?? "online"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ARI REST /ari/endpoints query failed: {Message}", ex.Message);
        }

        return list.OrderBy(e => e.Extension).ToList();
    }

    private async Task<List<AvailableExtensionDto>> QueryExtensionsFromAmiDatabaseAsync(string host, int port, string user, string pass, CancellationToken token)
    {
        var list = new List<AvailableExtensionDto>();
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3500, token)) != connectTask)
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

            var names = new Dictionary<string, string>();
            while ((l = await reader.ReadLineAsync()) != null)
            {
                if (l.Contains("--END COMMAND--") || l.Contains("Output: --END COMMAND--")) break;

                // Match format: /AMPUSER/222/cidname    : Ahmad Zarrinkamar
                var m = Regex.Match(l, @"/AMPUSER/(\d{3,4})/cidname\s*:\s*(.+)");
                if (m.Success)
                {
                    var ext = m.Groups[1].Value.Trim();
                    var name = m.Groups[2].Value.Trim();
                    names[ext] = name;
                }
            }

            // Logoff
            await writer.WriteLineAsync("Action: Logoff");
            await writer.WriteLineAsync();

            foreach (var kvp in names)
            {
                list.Add(new AvailableExtensionDto
                {
                    Extension = kvp.Key,
                    DisplayName = $"{kvp.Key} - {kvp.Value}",
                    DeviceType = "SIP",
                    Status = "Active"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("AMI Database query error: {Message}", ex.Message);
        }

        return list.OrderBy(e => e.Extension).ToList();
    }

    private static List<AvailableExtensionDto> GetFallbackExtensions()
    {
        return new List<AvailableExtensionDto>
        {
            new() { Extension = "222", DisplayName = "222 - احمد زرین‌کمر (تدارکات و خرید)", DeviceType = "SIP", Status = "Active" },
            new() { Extension = "201", DisplayName = "201 - مدیر بازرگانی", DeviceType = "SIP", Status = "Active" },
            new() { Extension = "202", DisplayName = "202 - سرپرست تدارکات", DeviceType = "SIP", Status = "Active" },
            new() { Extension = "203", DisplayName = "203 - کارشناس خرید ۱", DeviceType = "SIP", Status = "Active" },
            new() { Extension = "204", DisplayName = "204 - کارشناس خرید ۲", DeviceType = "SIP", Status = "Active" },
            new() { Extension = "101", DisplayName = "101 - اپراتور مرکزی", DeviceType = "SIP", Status = "Active" }
        };
    }

    #endregion

    #region AMI Fallback Implementation

    private async Task<bool> TryConnectAmiFallbackAsync(string host, int port, string user, string pass, CancellationToken token)
    {
        try
        {
            _amiTcpClient = new TcpClient();
            var connectTask = _amiTcpClient.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(4000, token)) != connectTask)
            {
                _amiTcpClient.Close();
                return false;
            }

            var stream = _amiTcpClient.GetStream();
            _amiReader = new StreamReader(stream, Encoding.UTF8);
            _amiWriter = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

            var banner = await _amiReader.ReadLineAsync();
            _logger.LogInformation("AMI Fallback Banner: {Banner}", banner);

            await _amiWriter.WriteLineAsync("Action: Login");
            await _amiWriter.WriteLineAsync($"Username: {user}");
            await _amiWriter.WriteLineAsync($"Secret: {pass}");
            await _amiWriter.WriteLineAsync("Events: on");
            await _amiWriter.WriteLineAsync();

            var resp = await _amiReader.ReadLineAsync();
            var msg = await _amiReader.ReadLineAsync();

            string? drain;
            while ((drain = await _amiReader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(drain)) break;
            }

            return resp != null && resp.Contains("Success", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("AMI Fallback connection failed: {Message}", ex.Message);
            return false;
        }
    }

    private async Task ReadAmiEventsAsync(CancellationToken token)
    {
        if (_amiReader == null) return;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!token.IsCancellationRequested && _isConnected && _amiReader != null)
        {
            try
            {
                var line = await _amiReader.ReadLineAsync();
                if (line == null) break;

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (headers.Count > 0)
                    {
                        await ProcessAmiEventBlockAsync(headers);
                        headers.Clear();
                    }
                    continue;
                }

                var idx = line.IndexOf(':');
                if (idx > 0)
                {
                    var k = line.Substring(0, idx).Trim();
                    var v = line.Substring(idx + 1).Trim();
                    headers[k] = v;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading AMI event packet");
                await Task.Delay(1000, token);
            }
        }
    }

    private async Task ProcessAmiEventBlockAsync(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Event", out var eventName)) return;

        var interesting = new[] { "DialBegin", "DialState", "DialEnd", "BridgeEnter", "Hangup", "Newstate", "Newchannel" };
        if (!interesting.Contains(eventName, StringComparer.OrdinalIgnoreCase)) return;

        var callerNum = headers.GetValueOrDefault("CallerIDNum") ?? headers.GetValueOrDefault("ConnectedLineNum") ?? string.Empty;
        var callerName = headers.GetValueOrDefault("CallerIDName") ?? headers.GetValueOrDefault("ConnectedLineName");
        var dest = headers.GetValueOrDefault("DestCallerIDNum") ?? headers.GetValueOrDefault("Exten") ?? string.Empty;
        var channel = headers.GetValueOrDefault("Channel") ?? string.Empty;
        var destChannel = headers.GetValueOrDefault("Destination") ?? headers.GetValueOrDefault("DestChannel") ?? string.Empty;
        var uniqueId = headers.GetValueOrDefault("Uniqueid") ?? Guid.NewGuid().ToString("N");
        var linkedId = headers.GetValueOrDefault("Linkedid") ?? uniqueId;

        var targetExt = ExtractExtension(dest, destChannel) ?? ExtractExtension(dest, channel);
        if (string.IsNullOrWhiteSpace(targetExt)) return;

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

        if (OnCallEventReceived != null)
        {
            await OnCallEventReceived(payload);
        }
    }

    private async Task KeepAliveAmiAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isConnected && _amiWriter != null)
        {
            try
            {
                await Task.Delay(30000, token);
                await _amiWriter.WriteLineAsync("Action: Ping");
                await _amiWriter.WriteLineAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AMI Ping Keepalive exception: {Message}", ex.Message);
            }
        }
    }

    #endregion

    private static string? ExtractExtension(string dest, string channel)
    {
        if (Regex.IsMatch(dest, @"^\d{3,4}$")) return dest;

        var m = Regex.Match(channel, @"(?:SIP|PJSIP)/(\d{3,4})");
        if (m.Success) return m.Groups[1].Value;

        var m2 = Regex.Match(dest, @"(?:SIP|PJSIP|)/?(\d{3,4})");
        if (m2.Success) return m2.Groups[1].Value;

        return null;
    }

    public async Task SimulateCallAsync(SimulateCallRequest request)
    {
        var targetExt = string.IsNullOrWhiteSpace(request.Extension) ? "222" : request.Extension.Trim();
        var callerNum = string.IsNullOrWhiteSpace(request.CallerNumber) ? "09121112233" : request.CallerNumber.Trim();
        var uniqueId = "sim-" + Guid.NewGuid().ToString("N");
        var isOutgoing = request.CallType?.Equals("outgoing", StringComparison.OrdinalIgnoreCase) == true;
        var direction = isOutgoing ? CallDirection.Outgoing : CallDirection.Incoming;

        var payload = new CallEventPayload
        {
            UniqueId = uniqueId,
            LinkedId = uniqueId,
            Direction = direction,
            Status = CallStatus.Ringing,
            CallerNumber = callerNum,
            CallerName = request.CallerName ?? "تماس شبیه‌سازی شده",
            DestinationNumber = targetExt,
            Extension = targetExt,
            Channel = $"PJSIP/{targetExt}-sim",
            TimestampUtc = DateTime.UtcNow
        };

        if (OnCallEventReceived != null)
        {
            await OnCallEventReceived(payload);
        }
    }
}

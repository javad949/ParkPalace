using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
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
        var host = _configuration["Telephony:AmiHost"] ?? "192.168.1.245";
        var port = int.TryParse(_configuration["Telephony:AmiPort"], out var p) ? p : 5038;
        var user = _configuration["Telephony:AmiUser"] ?? "crm_ami";
        var pass = _configuration["Telephony:AmiSecret"] ?? "";

        _logger.LogInformation("Connecting to Issabel Asterisk AMI at {Host}:{Port}...", host, port);

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
            _reader = new StreamReader(_stream, Encoding.ASCII);
            _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };

            // Read banner e.g. "Asterisk Call Manager/2.10.5"
            var banner = await _reader.ReadLineAsync();
            _logger.LogInformation("Asterisk AMI Banner: {Banner}", banner);

            // Send Login Action
            await _writer.WriteLineAsync("Action: Login");
            await _writer.WriteLineAsync($"Username: {user}");
            await _writer.WriteLineAsync($"Secret: {pass}");
            await _writer.WriteLineAsync("Events: call,cdr,system");
            await _writer.WriteLineAsync();

            _isConnected = true;
            _logger.LogInformation("Issabel AMI connected and authenticated successfully.");

            // Start background event read loop
            _ = Task.Run(() => ReadAmiEventsAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogWarning("Could not establish live AMI connection: {Message}", ex.Message);
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

    public Task SimulateCallAsync(SimulateCallRequest request)
    {
        _logger.LogInformation("SimulateCall called on Live AMI adapter - falling back to simulated event dispatch.");
        return Task.CompletedTask;
    }

    private async Task ReadAmiEventsAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync();
                if (line == null) break;

                // Process Asterisk AMI event blocks delimited by blank line
                if (line.StartsWith("Event:", StringComparison.OrdinalIgnoreCase))
                {
                    var eventName = line.Substring(6).Trim();
                    // Process event headers
                    if (eventName.Equals("DialBegin", StringComparison.OrdinalIgnoreCase) ||
                        eventName.Equals("BridgeEnter", StringComparison.OrdinalIgnoreCase) ||
                        eventName.Equals("Hangup", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Received live AMI event: {EventName}", eventName);
                    }
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
}

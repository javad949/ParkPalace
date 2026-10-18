using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Api.Services;

public class TelephonyBackgroundService : BackgroundService
{
    private readonly ITelephonyAdapter _telephonyAdapter;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelephonyBackgroundService> _logger;

    public TelephonyBackgroundService(
        ITelephonyAdapter telephonyAdapter,
        IServiceProvider serviceProvider,
        ILogger<TelephonyBackgroundService> logger)
    {
        _telephonyAdapter = telephonyAdapter;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelephonyBackgroundService starting ({AdapterType})...", _telephonyAdapter.AdapterType);

        _telephonyAdapter.OnCallEventReceived += async payload =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var callManager = scope.ServiceProvider.GetRequiredService<ICallManagerService>();
                await callManager.IngestCallEventAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting telephony event: UniqueId={UniqueId}", payload.UniqueId);
            }
        };

        await _telephonyAdapter.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_telephonyAdapter.IsConnected)
            {
                _logger.LogWarning("Telephony adapter disconnected! Attempting reconnection...");
                try
                {
                    await _telephonyAdapter.StartAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect telephony adapter.");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        await _telephonyAdapter.StopAsync(CancellationToken.None);
        _logger.LogInformation("TelephonyBackgroundService stopped.");
    }
}

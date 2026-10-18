using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkPalace.Api.Hubs;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Api.Services;

public class SignalRTelephonyNotificationService : ITelephonyNotificationService
{
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly ILogger<SignalRTelephonyNotificationService> _logger;

    public SignalRTelephonyNotificationService(
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        ILogger<SignalRTelephonyNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastCallPopupAsync(string? extension, CallerPopupDto popup)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(extension))
            {
                var groupName = $"ext_{extension.Trim()}";
                _logger.LogInformation("Broadcasting call popup for UniqueId {UniqueId} to {GroupName}", popup.UniqueId, groupName);
                await _hubContext.Clients.Group(groupName).ReceiveCallPopup(popup);
            }

            // Always notify supervisors as well
            await _hubContext.Clients.Group("supervisors").ReceiveCallPopup(popup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast call popup for UniqueId {UniqueId}", popup.UniqueId);
        }
    }

    public async Task BroadcastCallStatusAsync(CallRecordDto call)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(call.Extension))
            {
                var groupName = $"ext_{call.Extension.Trim()}";
                await _hubContext.Clients.Group(groupName).CallStatusChanged(call);
            }

            await _hubContext.Clients.Group("supervisors").CallStatusChanged(call);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast call status changed for UniqueId {UniqueId}", call.UniqueId);
        }
    }

    public async Task BroadcastCallEndedAsync(string uniqueId)
    {
        try
        {
            await _hubContext.Clients.All.CallEnded(uniqueId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast call ended for UniqueId {UniqueId}", uniqueId);
        }
    }
}

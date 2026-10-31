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
    private readonly ITelephonyAccessControlService _accessControlService;
    private readonly ILogger<SignalRTelephonyNotificationService> _logger;

    public SignalRTelephonyNotificationService(
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        ITelephonyAccessControlService accessControlService,
        ILogger<SignalRTelephonyNotificationService> logger)
    {
        _hubContext = hubContext;
        _accessControlService = accessControlService;
        _logger = logger;
    }

    public async Task BroadcastCallPopupAsync(string? extension, CallerPopupDto popup)
    {
        try
        {
            var targetGroups = await _accessControlService.GetNotificationTargetGroupsForExtensionAsync(extension);
            if (targetGroups.Count > 0)
            {
                _logger.LogInformation("Broadcasting call popup for UniqueId {UniqueId} (Ext: {Ext}) to groups: {Groups}", 
                    popup.UniqueId, extension, string.Join(", ", targetGroups));
                await _hubContext.Clients.Groups(targetGroups).ReceiveCallPopup(popup);
            }
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
            var targetGroups = await _accessControlService.GetNotificationTargetGroupsForExtensionAsync(call.Extension);
            if (targetGroups.Count > 0)
            {
                _logger.LogInformation("Broadcasting call status changed for UniqueId {UniqueId} (Ext: {Ext}, Status: {Status}) to groups: {Groups}", 
                    call.UniqueId, call.Extension, call.Status, string.Join(", ", targetGroups));
                await _hubContext.Clients.Groups(targetGroups).CallStatusChanged(call);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast call status changed for UniqueId {UniqueId}", call.UniqueId);
        }
    }

    public async Task BroadcastCallEndedAsync(string uniqueId, string? extension)
    {
        try
        {
            var targetGroups = await _accessControlService.GetNotificationTargetGroupsForExtensionAsync(extension);
            if (targetGroups.Count > 0)
            {
                _logger.LogInformation("Broadcasting call ended for UniqueId {UniqueId} (Ext: {Ext}) to groups: {Groups}", 
                    uniqueId, extension, string.Join(", ", targetGroups));
                await _hubContext.Clients.Groups(targetGroups).CallEnded(uniqueId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast call ended for UniqueId {UniqueId}", uniqueId);
        }
    }
}

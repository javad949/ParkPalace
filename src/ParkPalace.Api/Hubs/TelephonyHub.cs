using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Constants;

namespace ParkPalace.Api.Hubs;

public interface ITelephonyClient
{
    Task ReceiveCallPopup(CallerPopupDto popup);
    Task CallStatusChanged(CallRecordDto call);
    Task CallEnded(string uniqueId);
    Task ReceiveNotification(string message);
}

public class TelephonyHub : Hub<ITelephonyClient>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TelephonyHub> _logger;

    public TelephonyHub(ICurrentUserService currentUserService, ILogger<TelephonyHub> logger)
    {
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task RegisterExtension(string extension)
    {
        var username = _currentUserService.Username;
        var hasSupervise = _currentUserService.HasPermission(Permissions.CallsSupervise);

        if (!_currentUserService.HasPermission(Permissions.CallsView))
        {
            _logger.LogWarning("Unauthorized attempt to register extension {Extension} by {Username}", extension, username);
            throw new HubException("Access denied: missing CallsView permission.");
        }

        if (!string.IsNullOrWhiteSpace(extension))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ext_{extension.Trim()}");
            _logger.LogInformation("User {Username} registered connection {ConnectionId} for extension {Extension}", username, Context.ConnectionId, extension);
        }

        if (hasSupervise)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "supervisors");
            _logger.LogInformation("User {Username} registered to supervisory group", username);
        }
    }

    public async Task UnregisterExtension(string extension)
    {
        if (!string.IsNullOrWhiteSpace(extension))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ext_{extension.Trim()}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        var username = _currentUserService.Username;
        if (_currentUserService.HasPermission(Permissions.CallsSupervise))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "supervisors");
        }
        await base.OnConnectedAsync();
    }
}

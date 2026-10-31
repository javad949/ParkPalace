using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ParkPalace.Api.Hubs;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Api.Services;

public class SignalRTelephonyHubGroupManager : ITelephonyHubGroupManager
{
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;

    public SignalRTelephonyHubGroupManager(IHubContext<TelephonyHub, ITelephonyClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        return _hubContext.Groups.AddToGroupAsync(connectionId, groupName, ct);
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default)
    {
        return _hubContext.Groups.RemoveFromGroupAsync(connectionId, groupName, ct);
    }
}

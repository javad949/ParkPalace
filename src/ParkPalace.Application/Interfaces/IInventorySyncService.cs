using ParkPalace.Application.DTOs;
using ParkPalace.Domain.Entities;

namespace ParkPalace.Application.Interfaces;

public interface IInventorySyncService
{
    Task<SyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default);
    Task<SyncJobDto> TriggerSyncAsync(SyncJobType jobType, string triggeredBy, CancellationToken cancellationToken = default);
}

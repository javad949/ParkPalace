using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IExecutiveDashboardService
{
    Task<ExecutiveDashboardDto> GetAsync(ExecutiveDashboardQuery query, bool includeFinance, CancellationToken ct = default);
}

using ParkPalace.Application.DTOs;

namespace ParkPalace.Application.Interfaces;

public interface IFinancialDashboardService
{
    Task<FinancialDashboardDto> GetDashboardAsync(FinancialDashboardQuery query, CancellationToken ct = default);

    Task<PartyLedgerDto?> GetPartyLedgerAsync(Guid partyId, bool includeDocuments, int page, int pageSize, CancellationToken ct = default);
}

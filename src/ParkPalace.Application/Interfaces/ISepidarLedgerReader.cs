using ParkPalace.Application.Financial;

namespace ParkPalace.Application.Interfaces;

public interface ISepidarLedgerReader
{
    Task<SepidarLedgerSnapshot> ReadAsync(int? fiscalYear, DateTime? from, DateTime? to, CancellationToken ct = default);

    Task<SepidarPartyLedgerResult> ReadPartyAsync(
        string sepidarPartyRef,
        string? sepidarCode,
        bool includeLines,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

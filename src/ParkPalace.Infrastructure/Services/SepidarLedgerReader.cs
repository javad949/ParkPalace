using ParkPalace.Application.Financial;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Infrastructure.Services;

/// <summary>
/// Local development reader. Sepidar is not attached, so ledger reads stay disconnected.
/// </summary>
public class SepidarLedgerReader : ISepidarLedgerReader
{
    public Task<SepidarLedgerSnapshot> ReadAsync(int? fiscalYear, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        return Task.FromResult(new SepidarLedgerSnapshot
        {
            Connected = false,
            Failure = "اتصال سپیدار در محیط توسعه محلی پیکربندی نشده است.",
            ReadAtUtc = DateTime.UtcNow
        });
    }

    public Task<SepidarPartyLedgerResult> ReadPartyAsync(
        string sepidarPartyRef,
        string? sepidarCode,
        bool includeLines,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return Task.FromResult(new SepidarPartyLedgerResult
        {
            Connected = false,
            Found = false,
            Failure = "اتصال سپیدار در محیط توسعه محلی پیکربندی نشده است."
        });
    }
}

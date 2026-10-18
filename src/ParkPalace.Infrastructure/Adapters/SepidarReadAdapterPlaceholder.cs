using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Infrastructure.Adapters;

public class SepidarReadAdapterPlaceholder : ISepidarReadAdapter
{
    private readonly ILogger<SepidarReadAdapterPlaceholder> _logger;

    public SepidarReadAdapterPlaceholder(ILogger<SepidarReadAdapterPlaceholder> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured => false;

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SepidarReadAdapter is currently disabled by design in Phase 2.");
        return Task.FromResult(false);
    }
}

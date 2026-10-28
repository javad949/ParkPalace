using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.Interfaces;

namespace ParkPalace.Infrastructure.Adapters;

public class SepidarLiveReadAdapter : ISepidarReadAdapter
{
    private readonly string? _connectionString;
    private readonly ILogger<SepidarLiveReadAdapter> _logger;

    public SepidarLiveReadAdapter(IConfiguration configuration, ILogger<SepidarLiveReadAdapter> logger)
    {
        _connectionString = configuration.GetConnectionString("SepidarDatabase");
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return false;

        try
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT 1", conn) { CommandTimeout = 5 };
            await conn.OpenAsync(cancellationToken);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            return res != null && Convert.ToInt32(res) == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Sepidar live health check failed: {Message}", ex.Message);
            return false;
        }
    }
}

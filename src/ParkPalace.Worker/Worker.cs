using System;
using System.Data;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkPalace.Application.DTOs;
using ParkPalace.Application.Interfaces;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Data;

namespace ParkPalace.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=================================================");
        _logger.LogInformation("ParkPalace Background Worker started.");
        _logger.LogInformation("Sepidar & Issabel Integrations active.");
        _logger.LogInformation("=================================================");

        // Initial delay to let host services initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Execute Sepidar Sync Task (Isolated)
            await ExecuteSepidarSyncSafelyAsync(stoppingToken);

            // 2. Execute Issabel Telephony Health Check (Isolated)
            await ExecuteIssabelHealthSafelyAsync(stoppingToken);

            // Interval delay (default 15 minutes, configurable)
            var intervalMin = int.TryParse(_configuration["Worker:SyncIntervalMinutes"], out var m) ? m : 15;
            _logger.LogInformation("Worker cycle completed. Next sync in {Minutes} minutes.", intervalMin);
            await Task.Delay(TimeSpan.FromMinutes(intervalMin), stoppingToken);
        }
    }

    private async Task ExecuteSepidarSyncSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IInventorySyncService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var sepidarConnStr = config.GetConnectionString("SepidarDatabase");
            if (string.IsNullOrWhiteSpace(sepidarConnStr))
            {
                _logger.LogWarning("[SepidarSync] SepidarDatabase connection string is not set. Skipping live sync.");
                return;
            }

            _logger.LogInformation("[SepidarSync] Initiating inventory sync job from Sepidar...");
            var job = await syncService.TriggerSyncAsync(SyncJobType.Incremental, "BackgroundWorker", stoppingToken);
            _logger.LogInformation("[SepidarSync] Inventory sync completed successfully. Job ID: {JobId}, Status: {Status}, Items Processed: {Count}",
                job.Id, job.Status, job.ItemsProcessed);

            // Synchronize Parties/Vendors incrementally without duplication
            await SyncSepidarPartiesAsync(dbContext, sepidarConnStr, stoppingToken);
        }
        catch (Exception ex)
        {
            // Sepidar failure is completely isolated and must never stop CRM or other integrations
            _logger.LogError(ex, "[SepidarSyncError] Sepidar synchronization failed: {Message}", ex.Message);
        }
    }

    private async Task SyncSepidarPartiesAsync(CrmDbContext dbContext, string sepidarConnStr, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("[SepidarParties] Starting incremental party/vendor sync...");

            const string partyQuery = @"
                SELECT TOP 100
                    CAST(p.PartyId AS NVARCHAR(50)) AS PartyRef,
                    p.Name,
                    p.LastName,
                    ISNULL(p.EconomicCode, '') AS EconomicCode,
                    ISNULL(p.IdentificationCode, '') AS IdentificationCode,
                    p.Type,
                    p.IsVendor,
                    p.IsCustomer,
                    CAST(ISNULL(p.CustomerRemaining, 0) AS DECIMAL(18,2)) AS Balance
                FROM GNR.Party p
                WHERE (p.IsVendor = 1 OR p.IsCustomer = 1)
                ORDER BY p.LastModificationDate DESC, p.PartyId DESC;";

            using var conn = new SqlConnection(sepidarConnStr);
            using var cmd = new SqlCommand(partyQuery, conn) { CommandTimeout = 30 };
            await conn.OpenAsync(stoppingToken);
            using var reader = await cmd.ExecuteReaderAsync(stoppingToken);

            int syncedCount = 0;
            while (await reader.ReadAsync(stoppingToken))
            {
                var partyRef = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var firstName = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var lastName = reader.IsDBNull(2) ? null : reader.GetString(2);
                var economicCode = reader.IsDBNull(3) ? "" : reader.GetString(3);
                var nationalId = reader.IsDBNull(4) ? "" : reader.GetString(4);
                int typeInt = reader.IsDBNull(5) ? 1 : Convert.ToInt32(reader.GetValue(5));
                bool isVendor = !reader.IsDBNull(6) && Convert.ToBoolean(reader.GetValue(6));
                bool isCustomer = !reader.IsDBNull(7) && Convert.ToBoolean(reader.GetValue(7));
                decimal balance = reader.IsDBNull(8) ? 0m : Convert.ToDecimal(reader.GetValue(8));

                var displayName = string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName}";
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = $"Party-{partyRef}";
                }
                var partyRefTag = $"[Sepidar:PartyRef={partyRef}]";

                // Check for existing party by Sepidar tag or National Code to prevent duplication
                var existingParty = await dbContext.Parties
                    .FirstOrDefaultAsync(p => 
                        (p.Notes != null && p.Notes.Contains(partyRefTag)) ||
                        (!string.IsNullOrWhiteSpace(nationalId) && (p.NationalIdentifier == nationalId || p.NationalId == nationalId)) ||
                        p.DisplayName == displayName, stoppingToken);

                if (existingParty == null)
                {
                    var newParty = new Party
                    {
                        DisplayName = displayName,
                        FirstName = firstName,
                        LastName = lastName,
                        PartyType = typeInt == 1 ? PartyType.Individual : PartyType.LegalEntity,
                        NationalId = typeInt == 1 ? nationalId : null,
                        NationalIdentifier = typeInt == 2 ? nationalId : null,
                        EconomicCode = economicCode,
                        IsActive = true,
                        Notes = $"{partyRefTag} [Balance={balance:N0} IRR] [Vendor={isVendor}, Customer={isCustomer}]"
                    };
                    dbContext.Parties.Add(newParty);
                    syncedCount++;
                }
                else
                {
                    // Incremental update (preserving notes, updating balance tag)
                    if (existingParty.Notes == null || !existingParty.Notes.Contains(partyRefTag))
                    {
                        existingParty.Notes = $"{existingParty.Notes} {partyRefTag} [Balance={balance:N0} IRR]".Trim();
                    }
                    if (string.IsNullOrWhiteSpace(existingParty.EconomicCode) && !string.IsNullOrWhiteSpace(economicCode))
                    {
                        existingParty.EconomicCode = economicCode;
                    }
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("[SepidarParties] Completed party sync. New parties added: {Count}", syncedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SepidarPartiesError] Party sync error: {Message}", ex.Message);
        }
    }

    private async Task ExecuteIssabelHealthSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            var host = _configuration["Telephony:AmiHost"] ?? "192.168.1.245";
            var port = int.TryParse(_configuration["Telephony:AmiPort"], out var p) ? p : 5038;

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connectTask, Task.Delay(3000, stoppingToken)) == connectTask && client.Connected)
            {
                using var stream = client.GetStream();
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.ASCII);
                var banner = await reader.ReadLineAsync();
                _logger.LogInformation("[IssabelTelephony] PBX Asterisk AMI is ONLINE at {Host}:{Port}. Banner: {Banner}", host, port, banner);
            }
            else
            {
                _logger.LogWarning("[IssabelTelephony] Could not establish socket to Issabel Asterisk AMI at {Host}:{Port} (Timeout/Refused).", host, port);
            }
        }
        catch (Exception ex)
        {
            // Telephony failure is completely isolated and must never stop CRM or other integrations
            _logger.LogWarning(ex, "[IssabelTelephonyWarning] Telephony check warning: {Message}", ex.Message);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ParkPalace.Domain.Entities;
using ParkPalace.Infrastructure.Adapters;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using Xunit;

namespace ParkPalace.UnitTests;

public class LivePilotEntryGuardTests
{
    [Fact]
    public async Task GenericLiveSync_RejectsBeforeConnectingOrTrackingAnyWrites()
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new CrmDbContext(options);
        // No connection string: reaching the adapter would throw a different error.
        var adapter = new SepidarLiveInventoryAdapter(
            new ConfigurationBuilder().Build(), NullLogger<SepidarLiveInventoryAdapter>.Instance);
        var service = new InventorySyncService(db, adapter, null!, null!,
            NullLogger<InventorySyncService>.Instance);

        // A second attempt also fails at the guard, not at a leaked sync lock.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.TriggerSyncAsync(SyncJobType.Full, "local-guard-test"));
            Assert.StartsWith("LIVE_PILOT_SCOPE_REQUIRED:", error.Message);
            Assert.Empty(db.ChangeTracker.Entries());
            Assert.Empty(await db.InventorySyncJobs.ToListAsync());
        }
    }
}
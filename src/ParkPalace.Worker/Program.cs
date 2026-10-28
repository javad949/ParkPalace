using Microsoft.EntityFrameworkCore;
using ParkPalace.Application.Interfaces;
using ParkPalace.Infrastructure.Adapters;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Services;
using ParkPalace.Worker;

if (args.Contains("--pilot-preview") || args.Contains("--preview-pilot"))
{
    var exitCode = await InventoryPilotCliRunner.RunAsync(args);
    Environment.Exit(exitCode);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService();

// CRM DbContext - Explicit connection configuration required, no silent local default
var crmConnStr = builder.Configuration.GetConnectionString("CrmDatabase") 
    ?? Environment.GetEnvironmentVariable("PARKPALACE_CRM_CONN");

if (string.IsNullOrWhiteSpace(crmConnStr))
{
    throw new InvalidOperationException("CRITICAL: CRM database connection string is not configured. Configure 'ConnectionStrings:CrmDatabase' or environment variable 'PARKPALACE_CRM_CONN'. Default local connection fallback has been removed for security and consistency.");
}

builder.Services.AddDbContext<CrmDbContext>(options =>
{
    options.UseSqlServer(crmConnStr);
});
builder.Services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

// Sepidar Integration (Independent from Issabel)
var sepidarConnStr = builder.Configuration.GetConnectionString("SepidarDatabase");
if (!string.IsNullOrWhiteSpace(sepidarConnStr))
{
    builder.Services.AddScoped<ISepidarInventoryAdapter, SepidarLiveInventoryAdapter>();
    builder.Services.AddScoped<ISepidarFinancialAdapter, SepidarLiveFinancialAdapter>();
    builder.Services.AddScoped<ISepidarReadAdapter, SepidarLiveReadAdapter>();
}
else
{
    builder.Services.AddScoped<ISepidarInventoryAdapter, SepidarFixtureInventoryAdapter>();
    builder.Services.AddScoped<ISepidarFinancialAdapter, SepidarFixtureFinancialAdapter>();
    builder.Services.AddScoped<ISepidarReadAdapter, SepidarReadAdapterPlaceholder>();
}

builder.Services.AddScoped<IConsumptionCalculator, ConsumptionCalculator>();
builder.Services.AddScoped<IPurchaseRecommendationEngine, PurchaseRecommendationEngine>();
builder.Services.AddScoped<ICatalogStandardizationEngine, CatalogStandardizationEngine>();
builder.Services.AddScoped<IInventorySyncService, InventorySyncService>();
builder.Services.AddScoped<ISepidarReconciliationService, SepidarReconciliationService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

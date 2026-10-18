using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using ParkPalace.Api.Middleware;
using ParkPalace.Application.Interfaces;
using ParkPalace.Infrastructure.Adapters;
using ParkPalace.Infrastructure.Data;
using ParkPalace.Infrastructure.Identity;
using ParkPalace.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Database Context (Isolated CRM Database only - Never Sepidar)
var connectionString = builder.Configuration.GetConnectionString("CrmDatabase") 
    ?? "Server=.;Database=ParkPalaceCRM_DB;Integrated Security=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<CrmDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName);
    });
});

builder.Services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());

// 2. Identity & Authentication configuration
var isWindowsAuth = !builder.Environment.IsDevelopment() || 
    (builder.Configuration.GetValue<string>("Auth:Provider") ?? "Dev").Equals("Windows", StringComparison.OrdinalIgnoreCase);

if (isWindowsAuth)
{
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
    builder.Services.AddScoped<ICurrentUserService, WindowsCurrentUserService>();
}
else
{
    // Strictly development-only mock identity provider
    builder.Services.AddAuthentication();
    builder.Services.AddScoped<ICurrentUserService, DevCurrentUserService>();
}

builder.Services.AddHttpContextAccessor();

// 3. Register Sepidar Adapter & Phase 4 Services
var sepidarConnStr = builder.Configuration.GetConnectionString("SepidarDatabase");
if (!string.IsNullOrWhiteSpace(sepidarConnStr))
{
    builder.Services.AddScoped<ISepidarInventoryAdapter, SepidarLiveInventoryAdapter>();
}
else
{
    builder.Services.AddScoped<ISepidarInventoryAdapter, SepidarFixtureInventoryAdapter>();
}

builder.Services.AddScoped<IConsumptionCalculator, ConsumptionCalculator>();
builder.Services.AddScoped<IPurchaseRecommendationEngine, PurchaseRecommendationEngine>();
builder.Services.AddScoped<IInventorySyncService, InventorySyncService>();
builder.Services.AddScoped<ISepidarReadAdapter, SepidarReadAdapterPlaceholder>();

// Phase 5 Services
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
builder.Services.AddScoped<ISupplierInvoiceService, SupplierInvoiceService>();
builder.Services.AddScoped<IThreeWayMatchingEngine, ThreeWayMatchingEngine>();

if (!string.IsNullOrWhiteSpace(sepidarConnStr))
{
    builder.Services.AddScoped<ISepidarFinancialAdapter, SepidarLiveFinancialAdapter>();
}
else
{
    builder.Services.AddScoped<ISepidarFinancialAdapter, SepidarFixtureFinancialAdapter>();
}
builder.Services.AddScoped<ISepidarReconciliationService, SepidarReconciliationService>();

// Phase 6 Services: Telephony, Call Management, Audio Recordings & SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITelephonyAdapter, ParkPalace.Infrastructure.Services.Telephony.SimulatedTelephonyAdapter>();
builder.Services.AddScoped<ICallManagerService, ParkPalace.Infrastructure.Services.Telephony.CallManagerService>();
builder.Services.AddScoped<IRecordingService, ParkPalace.Infrastructure.Services.Telephony.RecordingService>();
builder.Services.AddSingleton<ITelephonyNotificationService, ParkPalace.Api.Services.SignalRTelephonyNotificationService>();
builder.Services.AddHostedService<ParkPalace.Api.Services.TelephonyBackgroundService>();

// Phase 7 Services: Reporting, Analytics & Exports
builder.Services.AddScoped<IReportingService, ParkPalace.Infrastructure.Services.Reporting.ReportingService>();
builder.Services.AddScoped<IExportService, ParkPalace.Infrastructure.Services.Reporting.ExportService>();

// 4. Controllers & JSON serialization
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// 5. CORS for React Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8888", "http://crm.sahar.loc:8888")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 6. OpenAPI / Swagger for Development
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ParkPalace Procurement CRM API",
        Version = "v1",
        Description = "Internal procurement CRM API for ParkPalace Bakery. Built with ASP.NET Core 8 & EF Core."
    });
});

var app = builder.Build();

// 7. Configure Pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ParkPalace CRM API v1");
    });

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    try
    {
        await CrmDbSeeder.SeedAsync(dbContext);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Development seeding warning: {Message}", ex.Message);
    }
}

app.UseCors("AllowLocalFrontend");

if (isWindowsAuth)
{
    app.UseAuthentication();
}

app.UseAuthorization();

app.MapControllers();
app.MapHub<ParkPalace.Api.Hubs.TelephonyHub>("/hubs/telephony");

app.Run();

// For IntegrationTests WebApplicationFactory
public partial class Program { }

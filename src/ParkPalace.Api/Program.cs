using Microsoft.AspNetCore.Authentication;
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
builder.Services.AddScoped<ICatalogStandardizationEngine, CatalogStandardizationEngine>();
builder.Services.AddScoped<IInventorySyncService, InventorySyncService>();
if (!string.IsNullOrWhiteSpace(sepidarConnStr))
{
    builder.Services.AddScoped<ISepidarReadAdapter, SepidarLiveReadAdapter>();
}
else
{
    builder.Services.AddScoped<ISepidarReadAdapter, SepidarReadAdapterPlaceholder>();
}

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
builder.Services.AddScoped<ISepidarPartyLookup, SepidarPartyLookup>();
builder.Services.AddScoped<ICrmDirectoryService, CrmDirectoryService>();
builder.Services.AddScoped<IEmployeeDirectoryService, ParkPalace.Infrastructure.Services.EmployeeDirectoryService>();
builder.Services.AddScoped<ISepidarLedgerReader, SepidarLedgerReader>();
builder.Services.AddScoped<IFinancialDashboardService, FinancialDashboardService>();

// Phase 6 Services: Telephony, Call Management, Audio Recordings & SignalR
builder.Services.AddSignalR();
var telephonyProvider = builder.Configuration["Telephony:Provider"] ?? "Simulated";
if (telephonyProvider.Equals("Issabel", StringComparison.OrdinalIgnoreCase) ||
    telephonyProvider.Equals("IssabelARI", StringComparison.OrdinalIgnoreCase) ||
    telephonyProvider.Equals("ARI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<ITelephonyAdapter, ParkPalace.Infrastructure.Services.Telephony.IssabelAriTelephonyAdapter>();
}
else
{
    builder.Services.AddSingleton<ITelephonyAdapter, ParkPalace.Infrastructure.Services.Telephony.SimulatedTelephonyAdapter>();
}

builder.Services.AddSingleton<ITrunkCallDetectionService, ParkPalace.Infrastructure.Services.Telephony.TrunkCallDetectionService>();
builder.Services.AddScoped<ICallManagerService, ParkPalace.Infrastructure.Services.Telephony.CallManagerService>();
builder.Services.AddScoped<IRecordingService, ParkPalace.Infrastructure.Services.Telephony.RecordingService>();
builder.Services.AddSingleton<ISftpClientRunner, ParkPalace.Infrastructure.Services.Telephony.ProcessSftpClientRunner>();
builder.Services.AddScoped<ISftpRecordingFetchService, ParkPalace.Infrastructure.Services.Telephony.SftpRecordingFetchService>();
builder.Services.AddSingleton<ITelephonyConnectionTracker, ParkPalace.Infrastructure.Services.Telephony.TelephonyConnectionTracker>();
builder.Services.AddSingleton<ITelephonyHubGroupManager, ParkPalace.Api.Services.SignalRTelephonyHubGroupManager>();
builder.Services.AddSingleton<ITelephonyAccessControlService, ParkPalace.Infrastructure.Services.Telephony.TelephonyAccessControlService>();
builder.Services.AddSingleton<ITelephonyNotificationService, ParkPalace.Api.Services.SignalRTelephonyNotificationService>();
builder.Services.AddHostedService<ParkPalace.Api.Services.TelephonyBackgroundService>();

// Phase 7 Services: Reporting, Analytics & Exports
builder.Services.AddScoped<IReportingService, ParkPalace.Infrastructure.Services.Reporting.ReportingService>();
builder.Services.AddScoped<IExecutiveDashboardService, ParkPalace.Infrastructure.Services.ExecutiveDashboardService>();
builder.Services.AddScoped<IExportService, ParkPalace.Infrastructure.Services.Reporting.ExportService>();

// Active Directory & User Management
builder.Services.AddScoped<IActiveDirectoryService, ParkPalace.Infrastructure.Services.ActiveDirectory.ActiveDirectoryService>();

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
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8888", "http://crm.sahar.loc:8888", "https://crm.sahar.loc", "http://crm.sahar.loc")
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
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ParkPalace CRM API v1");
    });
}

// Database seeding and DDL creation is strictly restricted to Development and Staging.
// In Production, automated startup DDL/seeding is disabled to protect operational data and enforce read-only/no-migration invariants.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    try
    {
        await CrmDbSeeder.SeedAsync(dbContext, isDevelopmentOrTest: true);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database seeding warning: {Message}", ex.Message);
    }
}
else
{
    app.Logger.LogInformation("Production environment active: automated startup database seeding/DDL is disabled.");
}


app.UseCors("AllowLocalFrontend");

if (isWindowsAuth)
{
    app.UseAuthentication();
    app.Use(async (context, next) =>
    {
        // Windows authentication identifies the account; CRM IsEnabled grants app access.
        // Keep health checks and /api/me reachable so the client can report denial.
        var path = context.Request.Path;
        var protectedPath = path.StartsWithSegments("/api") || path.StartsWithSegments("/v1") ||
                            path.StartsWithSegments("/hubs") || path.StartsWithSegments("/roles") ||
                            path.StartsWithSegments("/reports");
        var publicPath = path.StartsWithSegments("/api/health") ||
                         path.StartsWithSegments("/api/me") || path.StartsWithSegments("/me");
        if (protectedPath && !publicPath && !HttpMethods.IsOptions(context.Request.Method))
        {
            // A bare 403 gives the browser no chance to send Negotiate credentials.
            // Parallel calls then stay anonymous. Challenge first; 403 only after Windows
            // has authenticated an account that CRM does not allow.
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync(NegotiateDefaults.AuthenticationScheme);
                return;
            }

            var currentUser = context.RequestServices.GetRequiredService<ICurrentUserService>();
            if (!currentUser.IsAuthenticated)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json; charset=utf-8";
                await context.Response.WriteAsync(
                    "{\"status\":403,\"title\":\"عدم دسترسی\",\"detail\":\"این حساب ویندوز در CRM فعال نیست.\"}");
                return;
            }
        }
        await next();
    });
}

app.UseAuthorization();

app.MapControllers();
app.MapHub<ParkPalace.Api.Hubs.TelephonyHub>("/api/hubs/telephony");
app.MapHub<ParkPalace.Api.Hubs.TelephonyHub>("/hubs/telephony");

app.Run();

// For IntegrationTests WebApplicationFactory
public partial class Program { }

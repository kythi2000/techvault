using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TechVault.Api.Endpoints;
using TechVault.Api.Authentication;
using TechVault.Api.Middleware;
using TechVault.Api.Operations;
using TechVault.Application;
using TechVault.Application.Common.Abstractions;
using TechVault.Infrastructure;
using TechVault.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);
builder.AddOperations();

builder.Services.AddOpenApi();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAdminAuthentication(builder.Configuration);

var app = builder.Build();

if (args.Contains("--seed-catalog", StringComparer.Ordinal))
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        await CatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ITechVaultDbContext>(),
            app.Lifetime.ApplicationStopping);
    }
    app.Logger.LogInformation("Catalog seed completed; existing editorial content was preserved.");
    await app.DisposeAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseRouting();
app.UseMiddleware<RequestLogMiddleware>();
app.UseMiddleware<ApiErrorHandlingMiddleware>();
app.UseMiddleware<RequestSafetyMiddleware>();
app.UseRateLimiter();
app.UseCors("Catalog");
app.UseAuthentication();
app.UseAuthorization();
app.MapPublicCatalog();
app.MapAdminCatalog();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Lifetime.ApplicationStarted.Register(() => app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("TechVault.Operations").LogInformation("API started; startup configuration validated. Migrations and seeding remain explicit."));

app.Run();

public partial class Program { }

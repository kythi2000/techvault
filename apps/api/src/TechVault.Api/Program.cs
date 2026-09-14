using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using TechVault.Application.Common.Abstractions;
using TechVault.Infrastructure;
using TechVault.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (args.Contains("--seed-catalog", StringComparer.Ordinal))
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        await Nokia3310Seed.SeedAsync(scope.ServiceProvider.GetRequiredService<ITechVaultDbContext>(),
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

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program { }

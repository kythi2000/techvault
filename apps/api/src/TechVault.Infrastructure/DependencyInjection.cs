using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechVault.Application.Common.Abstractions;
using TechVault.Infrastructure.Persistence;

namespace TechVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<TechVaultDbContext>(options =>
        {
            var connectionString = configuration["DATABASE_URL"];

            Console.WriteLine("Configuring PostgreSQL DbContext.");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set DATABASE_URL to an Npgsql connection string. See README.md.");
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<ITechVaultDbContext>(provider => provider.GetRequiredService<TechVaultDbContext>());

        services.AddHealthChecks()
            .AddDbContextCheck<TechVaultDbContext>("postgres", tags: ["ready"]);

        return services;
    }
}

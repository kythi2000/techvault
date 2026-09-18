using TechVault.Application.Common.Abstractions;

namespace TechVault.Infrastructure.Persistence.Seed;

public static class CatalogSeed
{
    public static async Task SeedAsync(ITechVaultDbContext db, CancellationToken cancellationToken = default)
    {
        // Each device checks its own slug and saves once. Existing editorial records are never repaired or updated.
        await Nokia3310Seed.SeedAsync(db, cancellationToken);
        await Nokia3210Seed.SeedAsync(db, cancellationToken);
        await Macintosh128KSeed.SeedAsync(db, cancellationToken);
        await ImacG3Seed.SeedAsync(db, cancellationToken);
    }
}

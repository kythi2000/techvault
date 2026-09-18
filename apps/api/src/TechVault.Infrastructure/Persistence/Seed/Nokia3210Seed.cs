using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Seed;

internal static class Nokia3210Seed
{
    public static async Task SeedAsync(ITechVaultDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Devices.AnyAsync(x => x.Slug == "nokia-3210", cancellationToken)) return;

        var references = new CatalogSeedReferences(db, cancellationToken);
        var nokia = await references.BrandAsync("Nokia", "nokia", "Finnish telecommunications company and mobile phone manufacturer.");
        var phones = await references.CategoryAsync("Phones", "phones", 10, "Mobile phones.");
        var featurePhones = await references.CategoryAsync("Feature Phones", "feature-phones", 10,
            "Phones centered on calls, messaging, and built-in applications.", phones);
        var device = new Device("Nokia 3210", "nokia-3210", nokia, featurePhones);
        // Original 1999 handset, not the 2024 reissue. Sources and precision decisions: docs/catalog/SEED_DATA.md.
        device.UpdateContent(
            "A 1999 dual-band GSM phone with an internal antenna, interchangeable covers, and predictive text.",
            "The original Nokia 3210 combines GSM 900/1800 calling with SMS, picture messages, and a built-in ringtone composer. Its front and back covers can be replaced, while its games include Snake, Memory, and Rotation.",
            "Nokia introduced the 3210 at CeBIT on 18 March 1999, with volume availability planned for the second quarter. The launch emphasized personalisation through covers and messaging, bringing expressive features to an everyday mobile phone.",
            "Nokia 3210 (1999) specifications and history | TechVault",
            "Explore the original Nokia 3210: its 1999 introduction, internal antenna, replaceable covers, predictive text, and GSM messaging features.");
        device.SetRelease(1999); // Announcement is known; exact retail date and physical measurements remain unset.

        var general = await references.GroupAsync("General", "general", 10);
        var design = await references.GroupAsync("Design", "design", 20);
        var battery = await references.GroupAsync("Battery", "battery", 30);
        var network = await references.GroupAsync("Network", "network", 40);
        var software = await references.GroupAsync("Software", "software", 50);
        await references.SpecificationAsync(device, general, "Announcement date", "announcement_date", 10,
            SpecificationValue.Date(new DateOnly(1999, 3, 18)));
        await references.SpecificationAsync(device, design, "Replaceable front and back covers", "replaceable_covers", 10,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, design, "Antenna", "antenna", 20, SpecificationValue.Text("Internal"));
        await references.SpecificationAsync(device, battery, "Standard battery chemistry", "battery_chemistry", 10,
            SpecificationValue.Text("NiMH"));
        await references.SpecificationAsync(device, network, "Supported networks", "network_bands", 10,
            SpecificationValue.Text("GSM 900/1800"));
        await references.SpecificationAsync(device, software, "Predictive text input", "predictive_text", 30,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, software, "Picture messages", "picture_messages", 40,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, software, "Ringtone composer", "ringtone_composer", 50,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, software, "Maximum SMS text length", "sms_character_limit", 60,
            SpecificationValue.Number(160), "characters");
        await references.SpecificationAsync(device, software, "Built-in games", "built_in_games", 70,
            SpecificationValue.Text("Snake; Memory; Rotation"));

        device.Publish();
        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Seed;

public static class Nokia3310Seed
{
    public static async Task SeedAsync(ITechVaultDbContext db, CancellationToken cancellationToken = default)
    {
        // An existing device, including a draft or an edited/partial record, is never touched.
        if (await db.Devices.AnyAsync(x => x.Slug == "nokia-3310", cancellationToken))
            return;

        var nokia = await db.Brands.SingleOrDefaultAsync(x => x.Slug == "nokia", cancellationToken);
        if (nokia is null)
        {
            nokia = new Brand("Nokia", "nokia", "Finnish telecommunications company and mobile phone manufacturer.");
            db.Brands.Add(nokia);
        }

        var phones = await db.Categories.SingleOrDefaultAsync(x => x.Slug == "phones", cancellationToken);
        if (phones is null)
        {
            phones = new Category("Phones", "phones", 10, description: "Mobile phones.");
            db.Categories.Add(phones);
        }

        var featurePhones = await db.Categories.SingleOrDefaultAsync(x => x.Slug == "feature-phones", cancellationToken);
        if (featurePhones is null)
        {
            featurePhones = new Category("Feature Phones", "feature-phones", 10, phones,
                "Phones centered on calls, messaging, and built-in applications.");
            db.Categories.Add(featurePhones);
        }
        else if (featurePhones.ParentCategoryId != phones.Id)
        {
            throw new InvalidOperationException("The existing feature-phones category is not under phones. Review it manually; seed did not overwrite it.");
        }

        var device = new Device("Nokia 3310", "nokia-3310", nokia, featurePhones);
        device.UpdateContent(
            "A 2000 GSM phone built around calls, text messaging, and personalisation.",
            "The original Nokia 3310 supports GSM 900/1800 networks, SMS chat, and longer messages assembled from up to three SMS messages. Replaceable covers and downloadable profiles let owners personalise the phone.",
            "Nokia announced the 3310 on 1 September 2000, with availability planned for the fourth quarter. Its launch highlighted mobile messaging as a social activity, alongside built-in games such as Space Impact and Bantumi.",
            "Nokia 3310 specifications and history | TechVault",
            "Explore the original Nokia 3310: its 2000 launch, messaging features, network support, and documented specifications.");
        device.SetRelease(2000); // The announcement date is not an exact retail release date.
        device.SetPhysicalDetails(null, null, null, 133);

        var general = await GetGroupAsync("General", "general", 10);
        var design = await GetGroupAsync("Design", "design", 20);
        var battery = await GetGroupAsync("Battery", "battery", 30);
        var network = await GetGroupAsync("Network", "network", 40);
        var software = await GetGroupAsync("Software", "software", 50);

        await AddSpecificationAsync(general, "Announcement date", "announcement_date", 10,
            SpecificationValue.Date(new DateOnly(2000, 9, 1)));
        await AddSpecificationAsync(design, "Replaceable front and back covers", "replaceable_covers", 10,
            SpecificationValue.Boolean(true));
        await AddSpecificationAsync(design, "Antenna", "antenna", 20, SpecificationValue.Text("Internal"));
        await AddSpecificationAsync(battery, "Standard battery chemistry", "battery_chemistry", 10,
            SpecificationValue.Text("NiMH"));
        await AddSpecificationAsync(battery, "Advertised maximum talk time", "talk_time_max", 20,
            SpecificationValue.Number(4.5m), "h");
        await AddSpecificationAsync(battery, "Advertised maximum standby time", "standby_time_max", 30,
            SpecificationValue.Number(260), "h");
        await AddSpecificationAsync(network, "Supported networks", "network_bands", 10,
            SpecificationValue.Text("GSM 900/1800; EGSM 900"));
        await AddSpecificationAsync(software, "SMS chat", "sms_chat", 10, SpecificationValue.Boolean(true));
        await AddSpecificationAsync(software, "Maximum concatenated SMS segments", "sms_segments_max", 20,
            SpecificationValue.Number(3));

        device.Publish();
        db.Devices.Add(device);
        // One SaveChanges makes the missing references, device, and specifications atomic.
        await db.SaveChangesAsync(cancellationToken);

        async Task<SpecificationGroup> GetGroupAsync(string name, string key, int order)
        {
            var group = await db.SpecificationGroups.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (group is not null)
                return group;
            group = new SpecificationGroup(name, key, order);
            db.SpecificationGroups.Add(group);
            return group;
        }

        async Task AddSpecificationAsync(SpecificationGroup group, string name, string key, int order,
            SpecificationValue value, string? unit = null)
        {
            var definition = await db.SpecificationDefinitions.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (definition is null)
            {
                definition = new SpecificationDefinition(name, key, group, value.DataType, order, unit);
                db.SpecificationDefinitions.Add(definition);
            }
            else if (definition.DataType != value.DataType || definition.Unit != unit)
            {
                throw new InvalidOperationException($"Existing specification '{key}' has an incompatible type or unit. Review it manually; seed did not overwrite it.");
            }
            device.SetSpecification(definition, value);
        }
    }
}

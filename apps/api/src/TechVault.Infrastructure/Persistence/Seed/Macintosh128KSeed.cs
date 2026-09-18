using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Seed;

internal static class Macintosh128KSeed
{
    public static async Task SeedAsync(ITechVaultDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Devices.AnyAsync(x => x.Slug == "macintosh-128k", cancellationToken)) return;

        var references = new CatalogSeedReferences(db, cancellationToken);
        var apple = await references.BrandAsync("Apple", "apple", "Personal computer maker behind the Macintosh and iMac.");
        var computers = await references.CategoryAsync("Computers", "computers", 20, "Personal computers.");
        var allInOne = await references.CategoryAsync("All-in-One Computers", "all-in-one-computers", 10,
            "Computers with the display and main system integrated into one enclosure.", computers);
        var device = new Device("Macintosh 128K", "macintosh-128k", apple, allInOne);
        // Factory configuration, without third-party memory upgrades. See docs/catalog/SEED_DATA.md.
        device.UpdateContent(
            "Apple's 1984 compact Macintosh, combining a built-in monochrome CRT with a 68000 processor and 128 KiB of RAM.",
            "The Macintosh 128K integrates its display and 400K floppy drive in one enclosure. Its factory memory is fixed at 128 KiB, with no RAM slots or internal hard disk. The built-in screen uses a 512 by 342 pixel monochrome image.",
            "Introduced on 24 January 1984, the Macintosh 128K represents the early compact Macintosh design. Its small memory and floppy-based storage shaped how software was used on the machine. Apple records no built-in memory expansion, distinguishing the original configuration from later third-party upgrades.",
            "Macintosh 128K specifications and history | TechVault",
            "Explore the 1984 Macintosh 128K: its 68000 processor, factory memory, monochrome display, floppy storage, and compact all-in-one design.");
        device.SetRelease(1984, new DateOnly(1984, 1, 24), new DateOnly(1985, 10, 1));
        // Apple's inch/pound measurements converted and rounded to whole mm / 100 g, not added precision.
        device.SetPhysicalDetails(345, 244, 277, 7500);

        var processor = await references.GroupAsync("Processor", "processor", 21);
        var memory = await references.GroupAsync("Memory", "memory", 22);
        var storage = await references.GroupAsync("Storage", "storage", 23);
        var display = await references.GroupAsync("Display", "display", 24);
        var network = await references.GroupAsync("Network", "network", 40);
        await references.SpecificationAsync(device, processor, "Processor model", "cpu_model", 10,
            SpecificationValue.Text("Motorola 68000"));
        await references.SpecificationAsync(device, processor, "Processor clock", "cpu_clock", 20,
            SpecificationValue.Number(8), "MHz");
        await references.SpecificationAsync(device, memory, "Factory RAM", "ram_capacity", 10,
            SpecificationValue.Number(0.125m), "MiB");
        await references.SpecificationAsync(device, memory, "RAM slots", "ram_slots", 20,
            SpecificationValue.Number(0));
        await references.SpecificationAsync(device, storage, "Built-in floppy drive", "floppy_drive", 10,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, storage, "Floppy format", "floppy_format", 20,
            SpecificationValue.Text("400K"));
        await references.SpecificationAsync(device, storage, "Built-in hard disk", "internal_hard_disk", 30,
            SpecificationValue.Boolean(false));
        await references.SpecificationAsync(device, display, "Maximum built-in display resolution", "display_resolution", 10,
            SpecificationValue.Text("512 x 342"));
        await references.SpecificationAsync(device, display, "Color display", "display_color", 20,
            SpecificationValue.Boolean(false));
        await references.SpecificationAsync(device, network, "Built-in Ethernet", "ethernet", 20,
            SpecificationValue.Boolean(false));

        device.Publish();
        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
    }
}

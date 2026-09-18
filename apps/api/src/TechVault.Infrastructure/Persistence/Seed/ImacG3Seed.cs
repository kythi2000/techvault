using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Infrastructure.Persistence.Seed;

internal static class ImacG3Seed
{
    public static async Task SeedAsync(ITechVaultDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Devices.AnyAsync(x => x.Slug == "imac-g3", cancellationToken)) return;

        var references = new CatalogSeedReferences(db, cancellationToken);
        var apple = await references.BrandAsync("Apple", "apple", "Personal computer maker behind the Macintosh and iMac.");
        var computers = await references.CategoryAsync("Computers", "computers", 20, "Personal computers.");
        var allInOne = await references.CategoryAsync("All-in-One Computers", "all-in-one-computers", 10,
            "Computers with the display and main system integrated into one enclosure.", computers);
        var device = new Device("iMac G3", "imac-g3", apple, allInOne);
        device.SetComparisonGroup(await references.ComparisonGroupAsync("All-in-One Computers", "all_in_one"));
        // The original 233 MHz 1998 configuration, not an aggregate of the entire G3 range.
        // Sources and omitted revision-dependent fields: docs/catalog/SEED_DATA.md.
        device.UpdateContent(
            "The original 1998 iMac: an all-in-one PowerPC G3 computer with USB, Ethernet, and a built-in color CRT.",
            "This record covers the first 233 MHz iMac configuration, with 32 MiB of factory RAM, a 4 GB ATA hard disk, and a 24x CD-ROM drive. It provides two USB ports and 10/100 Ethernet, while omitting a built-in floppy drive.",
            "Apple announced iMac on 6 May 1998 as a consumer computer focused on Internet access and ease of use. The original model reached the market on 15 August. Its integrated design paired a color display with USB peripherals and Ethernet, contrasting with the floppy-centered compact Macintosh era.",
            "iMac G3 (1998, 233 MHz) specifications and history | TechVault",
            "Explore the original 1998 iMac G3: its 233 MHz processor, factory RAM, hard disk, USB ports, Ethernet, and all-in-one design.");
        device.SetRelease(1998, new DateOnly(1998, 8, 15));
        device.SetPhysicalDetails(401, 386, 447, 18100);

        var general = await references.GroupAsync("General", "general", 10);
        var processor = await references.GroupAsync("Processor", "processor", 21);
        var memory = await references.GroupAsync("Memory", "memory", 22);
        var storage = await references.GroupAsync("Storage", "storage", 23);
        var display = await references.GroupAsync("Display", "display", 24);
        var network = await references.GroupAsync("Network", "network", 40);
        var ports = await references.GroupAsync("Ports", "ports", 45);
        var software = await references.GroupAsync("Software", "software", 50);
        await references.SpecificationAsync(device, general, "Announcement date", "announcement_date", 10,
            SpecificationValue.Date(new DateOnly(1998, 5, 6)), isComparable: false);
        await references.SpecificationAsync(device, processor, "Processor model", "cpu_model", 10,
            SpecificationValue.Text("PowerPC G3"));
        await references.SpecificationAsync(device, processor, "Processor clock", "cpu_clock", 20,
            SpecificationValue.Number(233), "MHz");
        await references.SpecificationAsync(device, memory, "Factory RAM", "ram_capacity", 10,
            SpecificationValue.Number(32), "MiB");
        await references.SpecificationAsync(device, memory, "RAM slots", "ram_slots", 20,
            SpecificationValue.Number(2));
        await references.SpecificationAsync(device, storage, "Built-in floppy drive", "floppy_drive", 10,
            SpecificationValue.Boolean(false));
        await references.SpecificationAsync(device, storage, "Built-in hard disk", "internal_hard_disk", 30,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, storage, "Factory hard disk capacity", "storage_capacity", 40,
            SpecificationValue.Number(4), "GB");
        await references.SpecificationAsync(device, storage, "Optical drive", "optical_drive", 50,
            SpecificationValue.Text("24x CD-ROM"));
        await references.SpecificationAsync(device, display, "Maximum built-in display resolution", "display_resolution", 10,
            SpecificationValue.Text("1024 x 768"));
        await references.SpecificationAsync(device, display, "Color display", "display_color", 20,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, network, "Built-in Ethernet", "ethernet", 20,
            SpecificationValue.Boolean(true));
        await references.SpecificationAsync(device, network, "Ethernet link", "ethernet_link", 30,
            SpecificationValue.Text("10/100BASE-TX"));
        await references.SpecificationAsync(device, ports, "USB ports", "usb_ports", 10,
            SpecificationValue.Number(2));
        await references.SpecificationAsync(device, software, "Original operating system", "operating_system", 80,
            SpecificationValue.Text("Mac OS 8.1"));

        device.Publish();
        db.Devices.Add(device);
        await db.SaveChangesAsync(cancellationToken);
    }
}

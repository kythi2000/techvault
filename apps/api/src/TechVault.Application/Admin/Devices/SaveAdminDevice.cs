using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Domain.Devices;

namespace TechVault.Application.Admin.Devices;

public sealed class SaveAdminDeviceHandler(ITechVaultDbContext db, IValidator<DeviceInput> validator)
{
    public async Task<Result<AdminDeviceState>> HandleAsync(Guid? id, DeviceInput input, CancellationToken ct)
    {
        var error = await AdminValidation.ValidateAsync(validator, input, ct);
        if (error is not null) return Result<AdminDeviceState>.Failure(error);
        var device = id.HasValue ? await db.Devices.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && device is null) return Result<AdminDeviceState>.Failure(AdminValidation.Missing());
        if (device?.Status == DeviceStatus.Archived)
            return Result<AdminDeviceState>.Failure(AdminValidation.Conflict("Archived devices are read-only."));
        if (await db.Devices.AnyAsync(x => x.Slug == input.Slug && (!id.HasValue || x.Id != id), ct))
            return Result<AdminDeviceState>.Failure(AdminValidation.Duplicate());
        var brand = await db.Brands.SingleOrDefaultAsync(x => x.Id == input.BrandId, ct);
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == input.CategoryId, ct);
        var comparison = input.ComparisonGroupId.HasValue
            ? await db.ComparisonGroups.SingleOrDefaultAsync(x => x.Id == input.ComparisonGroupId, ct) : null;
        if (brand is null || category is null || (input.ComparisonGroupId.HasValue && comparison is null))
            return Result<AdminDeviceState>.Failure(Error.Validation("brandId, categoryId, and any comparisonGroupId must reference existing records."));
        device ??= new Device(input.Name, input.Slug, brand, category);
        error = AdminValidation.Domain(() =>
        {
            device.UpdateIdentity(input.Name, input.Slug, brand, category);
            if (device.Status == DeviceStatus.Draft)
                device.UpdateDraftContent(input.ShortDescription, input.Description, input.History, input.SeoTitle, input.SeoDescription);
            else device.UpdateContent(input.ShortDescription, input.Description, input.History, input.SeoTitle, input.SeoDescription);
            device.SetSearchMetadata(input.ModelNumber, input.Aliases);
            device.SetRelease(input.ReleaseYear, input.ReleaseDate, input.DiscontinuedDate);
            device.SetPhysicalDetails(input.HeightMm, input.WidthMm, input.DepthMm, input.WeightGrams);
            device.SetComparisonGroup(comparison);
        });
        if (error is not null) return Result<AdminDeviceState>.Failure(error);
        if (!id.HasValue) db.Devices.Add(device);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeviceState>.Success(AdminDeviceState.From(device));
    }
}

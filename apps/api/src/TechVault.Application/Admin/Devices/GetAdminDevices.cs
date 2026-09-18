using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Devices;

namespace TechVault.Application.Admin.Devices;

public sealed class GetAdminDevicesHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<AdminDeviceSummary>>> ListAsync(AdminDevicesQuery query, CancellationToken ct)
    {
        var error = PaginationRules.Validate(query.Page, query.PageSize);
        if (error is not null) return Result<PagedResult<AdminDeviceSummary>>.Failure(error);
        if (query.Status is not null and not "draft" and not "published" and not "archived")
            return Result<PagedResult<AdminDeviceSummary>>.Failure(Error.Validation("status must be draft, published, or archived."));
        var devices = db.Devices.AsNoTracking();
        if (query.Status is not null)
        {
            var status = Enum.Parse<DeviceStatus>(query.Status, true);
            devices = devices.Where(x => x.Status == status);
        }
        var total = await devices.CountAsync(ct);
        var rows = await devices.OrderBy(x => x.Slug).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.Id, x.Name, x.Slug, x.Status, x.BrandId, x.CategoryId, x.ComparisonGroupId, x.UpdatedAt }).ToListAsync(ct);
        var items = rows.Select(x => new AdminDeviceSummary(x.Id, x.Name, x.Slug, x.Status.ToString().ToLowerInvariant(),
            x.BrandId, x.CategoryId, x.ComparisonGroupId, x.UpdatedAt)).ToArray();
        return Result<PagedResult<AdminDeviceSummary>>.Success(PagedResult<AdminDeviceSummary>.Create(items, query.Page, query.PageSize, total));
    }

    public async Task<Result<AdminDeviceDetail>> GetAsync(Guid id, CancellationToken ct)
    {
        var device = await db.Devices.AsNoTracking().Include(x => x.Specifications).ThenInclude(x => x.Definition)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (device is null) return Result<AdminDeviceDetail>.Failure(AdminValidation.Missing());
        var content = new DeviceInput(device.Name, device.Slug, device.BrandId, device.CategoryId, device.ComparisonGroupId,
            device.ShortDescription, device.Description, device.History, device.SeoTitle, device.SeoDescription, device.ModelNumber,
            device.ReleaseYear, device.ReleaseDate, device.DiscontinuedDate, device.HeightMm, device.WidthMm, device.DepthMm, device.WeightGrams)
            { Aliases = device.Aliases.ToArray() };
        var specs = device.Specifications.OrderBy(x => x.Definition.Key).Select(x => new AdminSpecificationResponse(x.DefinitionId,
            x.Definition.Key, x.DataType.ToString().ToLowerInvariant(), x.Definition.Unit, x.ValueText, x.ValueNumber, x.ValueBoolean, x.ValueDate)).ToArray();
        return Result<AdminDeviceDetail>.Success(new(device.Id, device.Status.ToString().ToLowerInvariant(), content,
            device.CreatedAt, device.UpdatedAt, device.PublishedAt, specs));
    }
}

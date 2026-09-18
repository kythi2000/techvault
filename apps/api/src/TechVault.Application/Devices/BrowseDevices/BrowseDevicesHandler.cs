using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;

namespace TechVault.Application.Devices.BrowseDevices;

public sealed class BrowseDevicesHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<DeviceCardResponse>>> HandleAsync(BrowseDevicesQuery query,
        CancellationToken cancellationToken, string? requiredType = null)
    {
        var error = BrowseDevicesValidator.Validate(query, requiredType);
        if (error is not null) return Result<PagedResult<DeviceCardResponse>>.Failure(error);

        var devices = await PublishedDeviceQueries.FilterAsync(db, query, cancellationToken, requiredType);

        var total = await devices.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "name-asc" => devices.OrderBy(x => x.Name).ThenBy(x => x.Slug),
            "name-desc" => devices.OrderByDescending(x => x.Name).ThenBy(x => x.Slug),
            "release-asc" => devices.OrderBy(x => x.ReleaseYear == null).ThenBy(x => x.ReleaseYear).ThenBy(x => x.Slug),
            _ => devices.OrderBy(x => x.ReleaseYear == null).ThenByDescending(x => x.ReleaseYear).ThenBy(x => x.Slug)
        };
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(PublishedDeviceQueries.Card).ToListAsync(cancellationToken);
        return Result<PagedResult<DeviceCardResponse>>.Success(PagedResult<DeviceCardResponse>.Create(items, query.Page, query.PageSize, total));
    }
}

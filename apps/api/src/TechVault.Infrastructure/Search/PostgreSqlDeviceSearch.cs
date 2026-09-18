using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Devices;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Search;
using TechVault.Domain.Devices;
using TechVault.Infrastructure.Persistence;

namespace TechVault.Infrastructure.Search;

internal sealed class PostgreSqlDeviceSearch(TechVaultDbContext db) : IDeviceSearch
{
    public async Task<PagedResult<DeviceCardResponse>> SearchAsync(string text, int page, int pageSize, CancellationToken cancellationToken)
    {
        var devices = db.Devices.AsNoTracking().Where(x => x.Status == DeviceStatus.Published &&
            EF.Property<NpgsqlTsVector>(x, "SearchVector").Matches(EF.Functions.PlainToTsQuery("simple", text)));
        var total = await devices.CountAsync(cancellationToken);
        var items = await devices.OrderByDescending(x => EF.Property<NpgsqlTsVector>(x, "SearchVector")
                .Rank(EF.Functions.PlainToTsQuery("simple", text))).ThenBy(x => x.Slug)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(PublishedDeviceQueries.Card).ToListAsync(cancellationToken);
        return PagedResult<DeviceCardResponse>.Create(items, page, pageSize, total);
    }
}

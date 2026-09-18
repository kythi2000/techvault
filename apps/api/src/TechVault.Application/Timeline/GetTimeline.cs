using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Application.Devices;
using TechVault.Application.Devices.BrowseDevices;

namespace TechVault.Application.Timeline;

public sealed record GetTimelineQuery(
    string? Brand = null, string? Category = null, string? Type = null,
    int? Year = null, int? FromYear = null, int? ToYear = null, string? Era = null,
    int Page = 1, int PageSize = PaginationRules.DefaultPageSize);

public static class GetTimelineValidator
{
    public static Error? Validate(GetTimelineQuery query)
    {
        if (query.Era is not null &&
            (query.Era.Length != 5 || query.Era[4] != 's' || query.Era.Take(4).Any(x => x is < '0' or > '9') ||
             !int.TryParse(query.Era.AsSpan(0, 4), out var decade) || decade is < 10 or > 9990 || decade % 10 != 0))
            return Error.Validation("era must be a decade such as 1990s (0010s through 9990s).");
        return BrowseDevicesValidator.Validate(ToFilters(query));
    }

    internal static BrowseDevicesQuery ToFilters(GetTimelineQuery query) => new(
        query.Brand, query.Category, query.Type, query.Year, query.FromYear, query.ToYear,
        query.Era is null ? null : int.Parse(query.Era.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture),
        "release-asc", query.Page, query.PageSize);
}

public sealed class GetTimelineHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<DeviceCardResponse>>> HandleAsync(GetTimelineQuery query, CancellationToken cancellationToken)
    {
        var error = GetTimelineValidator.Validate(query);
        if (error is not null) return Result<PagedResult<DeviceCardResponse>>.Failure(error);

        var devices = (await PublishedDeviceQueries.FilterAsync(db, GetTimelineValidator.ToFilters(query), cancellationToken))
            .Where(x => x.ReleaseYear != null);
        var total = await devices.CountAsync(cancellationToken);
        // PostgreSQL ascending dates put NULL last: year-only records follow dated records in the same year.
        var items = await devices.OrderBy(x => x.ReleaseYear).ThenBy(x => x.ReleaseDate).ThenBy(x => x.Slug)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(PublishedDeviceQueries.Card).ToListAsync(cancellationToken);
        return Result<PagedResult<DeviceCardResponse>>.Success(PagedResult<DeviceCardResponse>.Create(items, query.Page, query.PageSize, total));
    }
}

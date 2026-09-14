using TechVault.Application.Common.Pagination;

namespace TechVault.Application.Devices.BrowseDevices;

public sealed record BrowseDevicesQuery(
    string? Brand = null, string? Category = null, string? Type = null,
    int? Year = null, int? FromYear = null, int? ToYear = null, int? Decade = null,
    string Sort = "release-desc", int Page = 1, int PageSize = PaginationRules.DefaultPageSize);

public sealed record DeviceCardResponse(Guid Id, string Name, string Slug, string ShortDescription,
    BrandReference Brand, CategoryReference Category, int? ReleaseYear, DateOnly? ReleaseDate);

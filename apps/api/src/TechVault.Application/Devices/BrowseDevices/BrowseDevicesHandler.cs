using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Devices;

namespace TechVault.Application.Devices.BrowseDevices;

public sealed class BrowseDevicesHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<DeviceCardResponse>>> HandleAsync(BrowseDevicesQuery query,
        CancellationToken cancellationToken, string? requiredType = null)
    {
        var error = BrowseDevicesValidator.Validate(query, requiredType);
        if (error is not null) return Result<PagedResult<DeviceCardResponse>>.Failure(error);

        var devices = db.Devices.AsNoTracking().Where(x => x.Status == DeviceStatus.Published);
        if (query.Brand is not null) devices = devices.Where(x => x.Brand.Slug == query.Brand);
        if (query.Year.HasValue) devices = devices.Where(x => x.ReleaseYear == query.Year);
        if (query.FromYear.HasValue) devices = devices.Where(x => x.ReleaseYear >= query.FromYear);
        if (query.ToYear.HasValue) devices = devices.Where(x => x.ReleaseYear <= query.ToYear);
        if (query.Decade.HasValue)
        {
            var end = query.Decade.Value + 9;
            devices = devices.Where(x => x.ReleaseYear >= query.Decade.Value && x.ReleaseYear <= end);
        }

        var type = requiredType ?? query.Type;
        if (query.Category is not null || type is not null)
        {
            // Taxonomy is small; one projection handles any depth without provider-specific SQL or N+1 queries.
            var categories = await db.Categories.AsNoTracking()
                .Select(x => new CategoryNode(x.Id, x.Slug, x.ParentCategoryId)).ToListAsync(cancellationToken);
            if (query.Category is not null)
            {
                var ids = DescendantIds(categories, query.Category);
                devices = devices.Where(x => ids.Contains(x.CategoryId));
            }
            if (type is not null)
            {
                var ids = DescendantIds(categories, type);
                devices = devices.Where(x => ids.Contains(x.CategoryId));
            }
        }

        var total = await devices.CountAsync(cancellationToken);
        var ordered = query.Sort switch
        {
            "name-asc" => devices.OrderBy(x => x.Name).ThenBy(x => x.Slug),
            "name-desc" => devices.OrderByDescending(x => x.Name).ThenBy(x => x.Slug),
            "release-asc" => devices.OrderBy(x => x.ReleaseYear == null).ThenBy(x => x.ReleaseYear).ThenBy(x => x.Slug),
            _ => devices.OrderBy(x => x.ReleaseYear == null).ThenByDescending(x => x.ReleaseYear).ThenBy(x => x.Slug)
        };
        var items = await ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DeviceCardResponse(x.Id, x.Name, x.Slug, x.ShortDescription,
                new BrandReference(x.Brand.Id, x.Brand.Name, x.Brand.Slug),
                new CategoryReference(x.Category.Id, x.Category.Name, x.Category.Slug,
                    x.Category.ParentCategoryId, x.Category.Parent == null ? null : x.Category.Parent.Slug),
                x.ReleaseYear, x.ReleaseDate)).ToListAsync(cancellationToken);
        return Result<PagedResult<DeviceCardResponse>>.Success(PagedResult<DeviceCardResponse>.Create(items, query.Page, query.PageSize, total));
    }

    private sealed record CategoryNode(Guid Id, string Slug, Guid? ParentId);

    private static Guid[] DescendantIds(IReadOnlyList<CategoryNode> categories, string slug)
    {
        var root = categories.SingleOrDefault(x => x.Slug == slug);
        if (root is null) return [];
        var children = categories.ToLookup(x => x.ParentId);
        var visited = new HashSet<Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(root.Id);
        while (pending.TryDequeue(out var id))
        {
            if (!visited.Add(id)) continue;
            foreach (var child in children[id]) pending.Enqueue(child.Id);
        }
        return visited.ToArray();
    }
}

using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Devices;

namespace TechVault.Application.Brands.GetBrands;

public sealed record GetBrandsQuery(int Page = 1, int PageSize = PaginationRules.DefaultPageSize);

public sealed class GetBrandsHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<BrandResponse>>> HandleAsync(GetBrandsQuery query, CancellationToken cancellationToken)
    {
        var error = PaginationRules.Validate(query.Page, query.PageSize);
        if (error is not null) return Result<PagedResult<BrandResponse>>.Failure(error);
        var brands = db.Brands.AsNoTracking();
        var total = await brands.CountAsync(cancellationToken);
        var items = await brands.OrderBy(x => x.Name).ThenBy(x => x.Slug)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new BrandResponse(x.Id, x.Name, x.Slug, x.Description, x.CreatedAt, x.UpdatedAt,
                db.Devices.Count(d => d.BrandId == x.Id && d.Status == DeviceStatus.Published)))
            .ToListAsync(cancellationToken);
        return Result<PagedResult<BrandResponse>>.Success(PagedResult<BrandResponse>.Create(items, query.Page, query.PageSize, total));
    }
}

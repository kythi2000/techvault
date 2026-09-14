using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Application.Common.Validation;
using TechVault.Domain.Devices;

namespace TechVault.Application.Brands.GetBrand;

public sealed record GetBrandQuery(string Slug);

public sealed class GetBrandHandler(ITechVaultDbContext db)
{
    public async Task<Result<BrandResponse>> HandleAsync(GetBrandQuery query, CancellationToken cancellationToken)
    {
        var error = RequestValidation.Slug(query.Slug, "slug");
        if (error is not null) return Result<BrandResponse>.Failure(error);
        var brand = await db.Brands.AsNoTracking().Where(x => x.Slug == query.Slug)
            .Select(x => new BrandResponse(x.Id, x.Name, x.Slug, x.Description, x.CreatedAt, x.UpdatedAt,
                db.Devices.Count(d => d.BrandId == x.Id && d.Status == DeviceStatus.Published)))
            .SingleOrDefaultAsync(cancellationToken);
        return brand is null
            ? Result<BrandResponse>.Failure(new Error("BRAND_NOT_FOUND", "Brand was not found.", ErrorType.NotFound))
            : Result<BrandResponse>.Success(brand);
    }
}

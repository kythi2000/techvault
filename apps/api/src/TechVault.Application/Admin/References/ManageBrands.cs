using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Brands;

namespace TechVault.Application.Admin.References;

public sealed class ManageBrandsHandler(ITechVaultDbContext db, IValidator<BrandInput> validator)
{
    public async Task<Result<PagedResult<AdminBrandResponse>>> ListAsync(AdminReferenceQuery query, CancellationToken ct)
    {
        if (PaginationRules.Validate(query.Page, query.PageSize) is { } error)
            return Result<PagedResult<AdminBrandResponse>>.Failure(error);
        var brands = db.Brands.AsNoTracking();
        var total = await brands.CountAsync(ct);
        var items = await brands.OrderBy(x => x.Slug).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AdminBrandResponse(x.Id, x.Name, x.Slug, x.Description)).ToListAsync(ct);
        return Result<PagedResult<AdminBrandResponse>>.Success(PagedResult<AdminBrandResponse>.Create(items, query.Page, query.PageSize, total));
    }

    public async Task<Result<AdminBrandResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Brands.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AdminBrandResponse(x.Id, x.Name, x.Slug, x.Description)).SingleOrDefaultAsync(ct);
        return item is null ? Result<AdminBrandResponse>.Failure(AdminValidation.Missing()) : Result<AdminBrandResponse>.Success(item);
    }

    public async Task<Result<AdminBrandResponse>> SaveAsync(Guid? id, BrandInput input, CancellationToken ct)
    {
        if (await AdminValidation.ValidateAsync(validator, input, ct) is { } error)
            return Result<AdminBrandResponse>.Failure(error);
        var brand = id.HasValue ? await db.Brands.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && brand is null) return Result<AdminBrandResponse>.Failure(AdminValidation.Missing());
        if (brand is not null && brand.Slug != input.Slug)
            return Result<AdminBrandResponse>.Failure(AdminValidation.Conflict("Brand slugs cannot be changed."));
        if (await db.Brands.AnyAsync(x => x.Slug == input.Slug && (!id.HasValue || x.Id != id), ct))
            return Result<AdminBrandResponse>.Failure(AdminValidation.Duplicate());
        brand ??= new Brand(input.Name, input.Slug, input.Description);
        if (AdminValidation.Domain(() => brand.UpdateDetails(input.Name, input.Description)) is { } domainError)
            return Result<AdminBrandResponse>.Failure(domainError);
        if (!id.HasValue) db.Brands.Add(brand);
        await db.SaveChangesAsync(ct);
        return Result<AdminBrandResponse>.Success(new(brand.Id, brand.Name, brand.Slug, brand.Description));
    }

    public async Task<Result<AdminDeletedResponse>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var brand = await db.Brands.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (brand is null) return Result<AdminDeletedResponse>.Failure(AdminValidation.Missing());
        if (await db.Devices.AnyAsync(x => x.BrandId == id, ct)) return Result<AdminDeletedResponse>.Failure(AdminValidation.Referenced());
        db.Brands.Remove(brand);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeletedResponse>.Success(new(id));
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Categories;

namespace TechVault.Application.Admin.References;

public sealed class ManageCategoriesHandler(ITechVaultDbContext db, IValidator<CategoryInput> validator)
{
    public async Task<Result<PagedResult<AdminCategoryResponse>>> ListAsync(AdminReferenceQuery query, CancellationToken ct)
    {
        if (PaginationRules.Validate(query.Page, query.PageSize) is { } error)
            return Result<PagedResult<AdminCategoryResponse>>.Failure(error);
        var categories = db.Categories.AsNoTracking();
        var total = await categories.CountAsync(ct);
        var items = await categories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Slug)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AdminCategoryResponse(x.Id, x.Name, x.Slug, x.Description, x.DisplayOrder, x.ParentCategoryId)).ToListAsync(ct);
        return Result<PagedResult<AdminCategoryResponse>>.Success(PagedResult<AdminCategoryResponse>.Create(items, query.Page, query.PageSize, total));
    }

    public async Task<Result<AdminCategoryResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Categories.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AdminCategoryResponse(x.Id, x.Name, x.Slug, x.Description, x.DisplayOrder, x.ParentCategoryId)).SingleOrDefaultAsync(ct);
        return item is null ? Result<AdminCategoryResponse>.Failure(AdminValidation.Missing()) : Result<AdminCategoryResponse>.Success(item);
    }

    public async Task<Result<AdminCategoryResponse>> SaveAsync(Guid? id, CategoryInput input, CancellationToken ct)
    {
        if (await AdminValidation.ValidateAsync(validator, input, ct) is { } error)
            return Result<AdminCategoryResponse>.Failure(error);
        var category = id.HasValue ? await db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && category is null) return Result<AdminCategoryResponse>.Failure(AdminValidation.Missing());
        if (category is not null && (category.Slug != input.Slug || category.ParentCategoryId != input.ParentCategoryId))
            return Result<AdminCategoryResponse>.Failure(AdminValidation.Conflict("Category slugs and parents cannot be changed."));
        if (await db.Categories.AnyAsync(x => x.Slug == input.Slug && (!id.HasValue || x.Id != id), ct))
            return Result<AdminCategoryResponse>.Failure(AdminValidation.Duplicate());
        var parent = input.ParentCategoryId.HasValue
            ? await db.Categories.SingleOrDefaultAsync(x => x.Id == input.ParentCategoryId, ct) : null;
        if (input.ParentCategoryId.HasValue && parent is null)
            return Result<AdminCategoryResponse>.Failure(Error.Validation("Parent category was not found."));
        category ??= new Category(input.Name, input.Slug, input.DisplayOrder, parent, input.Description);
        if (AdminValidation.Domain(() => category.UpdateDetails(input.Name, input.Description, input.DisplayOrder)) is { } domainError)
            return Result<AdminCategoryResponse>.Failure(domainError);
        if (!id.HasValue) db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return Result<AdminCategoryResponse>.Success(new(category.Id, category.Name, category.Slug, category.Description, category.DisplayOrder, category.ParentCategoryId));
    }

    public async Task<Result<AdminDeletedResponse>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (category is null) return Result<AdminDeletedResponse>.Failure(AdminValidation.Missing());
        if (await db.Devices.AnyAsync(x => x.CategoryId == id, ct) || await db.Categories.AnyAsync(x => x.ParentCategoryId == id, ct))
            return Result<AdminDeletedResponse>.Failure(AdminValidation.Referenced());
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeletedResponse>.Success(new(id));
    }
}

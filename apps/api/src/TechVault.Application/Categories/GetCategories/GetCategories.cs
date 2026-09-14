using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;

namespace TechVault.Application.Categories.GetCategories;

public sealed record GetCategoriesQuery(int Page = 1, int PageSize = PaginationRules.DefaultPageSize);
public sealed record CategoryResponse(Guid Id, string Name, string Slug, string Description,
    Guid? ParentCategoryId, string? ParentSlug, int DisplayOrder);

public sealed class GetCategoriesHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<CategoryResponse>>> HandleAsync(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        var error = PaginationRules.Validate(query.Page, query.PageSize);
        if (error is not null) return Result<PagedResult<CategoryResponse>>.Failure(error);
        var categories = db.Categories.AsNoTracking();
        var total = await categories.CountAsync(cancellationToken);
        var items = await categories.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Slug)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Slug, x.Description, x.ParentCategoryId,
                x.Parent == null ? null : x.Parent.Slug, x.DisplayOrder)).ToListAsync(cancellationToken);
        return Result<PagedResult<CategoryResponse>>.Success(PagedResult<CategoryResponse>.Create(items, query.Page, query.PageSize, total));
    }
}

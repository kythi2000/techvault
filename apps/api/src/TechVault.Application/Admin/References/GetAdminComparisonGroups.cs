using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;

namespace TechVault.Application.Admin.References;

public sealed class GetAdminComparisonGroupsHandler(ITechVaultDbContext db)
{
    public async Task<Result<PagedResult<AdminComparisonGroupResponse>>> HandleAsync(AdminReferenceQuery query, CancellationToken ct)
    {
        if (PaginationRules.Validate(query.Page, query.PageSize) is { } error)
            return Result<PagedResult<AdminComparisonGroupResponse>>.Failure(error);
        var groups = db.ComparisonGroups.AsNoTracking();
        var total = await groups.CountAsync(ct);
        var items = await groups.OrderBy(x => x.Key).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AdminComparisonGroupResponse(x.Id, x.Name, x.Key)).ToListAsync(ct);
        return Result<PagedResult<AdminComparisonGroupResponse>>.Success(
            PagedResult<AdminComparisonGroupResponse>.Create(items, query.Page, query.PageSize, total));
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Specifications;

namespace TechVault.Application.Admin.References;

public sealed class ManageSpecificationGroupsHandler(ITechVaultDbContext db, IValidator<SpecificationGroupInput> validator)
{
    public async Task<Result<PagedResult<AdminSpecificationGroupResponse>>> ListAsync(AdminReferenceQuery query, CancellationToken ct)
    {
        if (PaginationRules.Validate(query.Page, query.PageSize) is { } error)
            return Result<PagedResult<AdminSpecificationGroupResponse>>.Failure(error);
        var groups = db.SpecificationGroups.AsNoTracking();
        var total = await groups.CountAsync(ct);
        var items = await groups.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AdminSpecificationGroupResponse(x.Id, x.Name, x.Key, x.DisplayOrder)).ToListAsync(ct);
        return Result<PagedResult<AdminSpecificationGroupResponse>>.Success(PagedResult<AdminSpecificationGroupResponse>.Create(items, query.Page, query.PageSize, total));
    }

    public async Task<Result<AdminSpecificationGroupResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.SpecificationGroups.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AdminSpecificationGroupResponse(x.Id, x.Name, x.Key, x.DisplayOrder)).SingleOrDefaultAsync(ct);
        return item is null ? Result<AdminSpecificationGroupResponse>.Failure(AdminValidation.Missing()) : Result<AdminSpecificationGroupResponse>.Success(item);
    }

    public async Task<Result<AdminSpecificationGroupResponse>> SaveAsync(Guid? id, SpecificationGroupInput input, CancellationToken ct)
    {
        if (await AdminValidation.ValidateAsync(validator, input, ct) is { } error)
            return Result<AdminSpecificationGroupResponse>.Failure(error);
        var group = id.HasValue ? await db.SpecificationGroups.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && group is null) return Result<AdminSpecificationGroupResponse>.Failure(AdminValidation.Missing());
        if (group is not null && group.Key != input.Key)
            return Result<AdminSpecificationGroupResponse>.Failure(AdminValidation.Conflict("Specification group keys cannot be changed."));
        if (await db.SpecificationGroups.AnyAsync(x => x.Key == input.Key && (!id.HasValue || x.Id != id), ct))
            return Result<AdminSpecificationGroupResponse>.Failure(AdminValidation.Duplicate());
        group ??= new SpecificationGroup(input.Name, input.Key, input.DisplayOrder);
        if (AdminValidation.Domain(() => group.UpdateDetails(input.Name, input.DisplayOrder)) is { } domainError)
            return Result<AdminSpecificationGroupResponse>.Failure(domainError);
        if (!id.HasValue) db.SpecificationGroups.Add(group);
        await db.SaveChangesAsync(ct);
        return Result<AdminSpecificationGroupResponse>.Success(new(group.Id, group.Name, group.Key, group.DisplayOrder));
    }

    public async Task<Result<AdminDeletedResponse>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var group = await db.SpecificationGroups.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (group is null) return Result<AdminDeletedResponse>.Failure(AdminValidation.Missing());
        if (await db.SpecificationDefinitions.AnyAsync(x => x.GroupId == id, ct))
            return Result<AdminDeletedResponse>.Failure(AdminValidation.Referenced());
        db.SpecificationGroups.Remove(group);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeletedResponse>.Success(new(id));
    }
}

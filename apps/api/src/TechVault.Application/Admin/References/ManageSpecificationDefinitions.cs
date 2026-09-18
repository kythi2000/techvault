using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Domain.Specifications;

namespace TechVault.Application.Admin.References;

public sealed class ManageSpecificationDefinitionsHandler(ITechVaultDbContext db, IValidator<SpecificationDefinitionInput> validator)
{
    public async Task<Result<PagedResult<AdminSpecificationDefinitionResponse>>> ListAsync(AdminReferenceQuery query, CancellationToken ct)
    {
        if (PaginationRules.Validate(query.Page, query.PageSize) is { } error)
            return Result<PagedResult<AdminSpecificationDefinitionResponse>>.Failure(error);
        var definitions = db.SpecificationDefinitions.AsNoTracking();
        var total = await definitions.CountAsync(ct);
        var items = await definitions.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Key)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return Result<PagedResult<AdminSpecificationDefinitionResponse>>.Success(
            PagedResult<AdminSpecificationDefinitionResponse>.Create(items.Select(Response).ToList(), query.Page, query.PageSize, total));
    }

    public async Task<Result<AdminSpecificationDefinitionResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.SpecificationDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? Result<AdminSpecificationDefinitionResponse>.Failure(AdminValidation.Missing()) : Result<AdminSpecificationDefinitionResponse>.Success(Response(item));
    }

    public async Task<Result<AdminSpecificationDefinitionResponse>> SaveAsync(Guid? id, SpecificationDefinitionInput input, CancellationToken ct)
    {
        if (await AdminValidation.ValidateAsync(validator, input, ct) is { } error)
            return Result<AdminSpecificationDefinitionResponse>.Failure(error);
        var definition = id.HasValue ? await db.SpecificationDefinitions.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && definition is null) return Result<AdminSpecificationDefinitionResponse>.Failure(AdminValidation.Missing());
        var dataType = Enum.Parse<SpecificationDataType>(input.DataType, true);
        if (definition is not null && (definition.Key != input.Key || definition.DataType != dataType || definition.Unit != input.Unit?.Trim()))
            return Result<AdminSpecificationDefinitionResponse>.Failure(AdminValidation.Conflict("Definition key, data type, and unit cannot be changed; create a new definition."));
        if (await db.SpecificationDefinitions.AnyAsync(x => x.Key == input.Key && (!id.HasValue || x.Id != id), ct))
            return Result<AdminSpecificationDefinitionResponse>.Failure(AdminValidation.Duplicate());
        var group = await db.SpecificationGroups.SingleOrDefaultAsync(x => x.Id == input.GroupId, ct);
        if (group is null) return Result<AdminSpecificationDefinitionResponse>.Failure(Error.Validation("Specification group was not found."));
        definition ??= new SpecificationDefinition(input.Name, input.Key, group, dataType, input.DisplayOrder, input.Unit, input.IsComparable);
        if (AdminValidation.Domain(() => definition.UpdateDetails(input.Name, group, input.DisplayOrder, input.IsComparable)) is { } domainError)
            return Result<AdminSpecificationDefinitionResponse>.Failure(domainError);
        if (!id.HasValue) db.SpecificationDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        return Result<AdminSpecificationDefinitionResponse>.Success(Response(definition));
    }

    public async Task<Result<AdminDeletedResponse>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var definition = await db.SpecificationDefinitions.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (definition is null) return Result<AdminDeletedResponse>.Failure(AdminValidation.Missing());
        if (await db.DeviceSpecifications.AnyAsync(x => x.DefinitionId == id, ct))
            return Result<AdminDeletedResponse>.Failure(AdminValidation.Referenced());
        db.SpecificationDefinitions.Remove(definition);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeletedResponse>.Success(new(id));
    }

    private static AdminSpecificationDefinitionResponse Response(SpecificationDefinition item) =>
        new(item.Id, item.Name, item.Key, item.GroupId, item.DataType.ToString().ToLowerInvariant(), item.DisplayOrder, item.Unit, item.IsComparable);
}

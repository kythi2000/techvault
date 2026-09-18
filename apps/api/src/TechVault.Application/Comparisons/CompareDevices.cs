using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Application.Common.Validation;
using TechVault.Application.Devices;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Domain.Devices;

namespace TechVault.Application.Comparisons;

public sealed record CompareDevicesQuery(string? Devices = null, bool DifferencesOnly = false);

public sealed record ComparisonGroupResponse(Guid Id, string Key, string Name);
public sealed record ComparisonValueResponse(bool IsMissing, string? ValueText = null, decimal? ValueNumber = null,
    bool? ValueBoolean = null, DateOnly? ValueDate = null);
public sealed record ComparisonSpecificationResponse(Guid Id, string Key, string Name, string DataType, string? Unit,
    int DisplayOrder, bool IsDifferent, IReadOnlyList<ComparisonValueResponse> Values);
public sealed record ComparisonSpecificationGroupResponse(Guid Id, string Key, string Name, int DisplayOrder,
    IReadOnlyList<ComparisonSpecificationResponse> Specifications);
public sealed record CompareDevicesResponse(ComparisonGroupResponse ComparisonGroup, IReadOnlyList<DeviceCardResponse> Devices,
    bool DifferencesOnly, IReadOnlyList<ComparisonSpecificationGroupResponse> SpecificationGroups);

public static class CompareDevicesValidator
{
    public static Error? Validate(CompareDevicesQuery query)
    {
        // At most two 160-character slugs and one comma. Do not accept IDs, empty entries, or extra devices.
        if (string.IsNullOrWhiteSpace(query.Devices) || query.Devices.Length > 321)
            return Error.Validation("devices must contain exactly two distinct lowercase slugs separated by a comma.");
        var slugs = query.Devices.Split(',', 3);
        if (slugs.Length != 2 || slugs[0] == slugs[1])
            return Error.Validation("devices must contain exactly two distinct lowercase slugs separated by a comma.");
        return RequestValidation.Slug(slugs[0], "devices[0]") ?? RequestValidation.Slug(slugs[1], "devices[1]");
    }
}

public sealed class CompareDevicesHandler(ITechVaultDbContext db)
{
    public async Task<Result<CompareDevicesResponse>> HandleAsync(CompareDevicesQuery query, CancellationToken cancellationToken)
    {
        var error = CompareDevicesValidator.Validate(query);
        if (error is not null) return Result<CompareDevicesResponse>.Failure(error);
        var slugs = query.Devices!.Split(',');
        var devices = await db.Devices.AsNoTracking()
            .Where(x => slugs.Contains(x.Slug) && x.Status == DeviceStatus.Published)
            .Select(x => new
            {
                Card = new DeviceCardResponse(x.Id, x.Name, x.Slug, x.ShortDescription,
                    new BrandReference(x.Brand.Id, x.Brand.Name, x.Brand.Slug),
                    new CategoryReference(x.Category.Id, x.Category.Name, x.Category.Slug,
                        x.Category.ParentCategoryId, x.Category.Parent == null ? null : x.Category.Parent.Slug),
                    x.ReleaseYear, x.ReleaseDate),
                Group = x.ComparisonGroup == null ? null :
                    new ComparisonGroupResponse(x.ComparisonGroup.Id, x.ComparisonGroup.Key, x.ComparisonGroup.Name)
            }).ToListAsync(cancellationToken);
        if (devices.Count != 2) return Result<CompareDevicesResponse>.Failure(Error.DeviceNotFound());
        var first = devices.Single(x => x.Card.Slug == slugs[0]);
        var second = devices.Single(x => x.Card.Slug == slugs[1]);
        if (first.Group is null || second.Group is null)
            return Result<CompareDevicesResponse>.Failure(new Error("COMPARISON_UNAVAILABLE",
                "Both devices must have an assigned comparison group.", ErrorType.Validation));
        if (first.Group.Id != second.Group.Id)
            return Result<CompareDevicesResponse>.Failure(new Error("INCOMPATIBLE_DEVICES",
                "Devices must belong to the same comparison group.", ErrorType.Validation));

        var ids = new[] { first.Card.Id, second.Card.Id };
        var rows = await db.DeviceSpecifications.AsNoTracking()
            .Where(x => ids.Contains(x.DeviceId) && x.Definition.IsComparable &&
                db.Devices.Any(d => d.Id == x.DeviceId && d.Status == DeviceStatus.Published))
            .OrderBy(x => x.Definition.Group.DisplayOrder).ThenBy(x => x.Definition.Group.Key)
            .ThenBy(x => x.Definition.DisplayOrder).ThenBy(x => x.Definition.Key)
            .Select(x => new
            {
                x.DeviceId, x.DefinitionId, x.Definition.Name, x.Definition.Key, x.Definition.DataType,
                x.Definition.Unit, x.Definition.DisplayOrder,
                GroupId = x.Definition.Group.Id, GroupName = x.Definition.Group.Name,
                GroupKey = x.Definition.Group.Key, GroupOrder = x.Definition.Group.DisplayOrder,
                Value = new ComparisonValueResponse(false, x.ValueText, x.ValueNumber, x.ValueBoolean, x.ValueDate)
            }).ToListAsync(cancellationToken);

        // Align the union of assigned, opted-in definitions by identity, never by display label.
        var groups = rows.GroupBy(x => new { x.GroupId, x.GroupKey, x.GroupName, x.GroupOrder })
            .Select(group => new ComparisonSpecificationGroupResponse(group.Key.GroupId, group.Key.GroupKey,
                group.Key.GroupName, group.Key.GroupOrder, group.GroupBy(x => x.DefinitionId).Select(definition =>
                {
                    var metadata = definition.First();
                    var values = ids.Select(id => definition.SingleOrDefault(x => x.DeviceId == id)?.Value
                        ?? new ComparisonValueResponse(true)).ToArray();
                    // Record equality compares typed values exactly, including ordinal text. No scoring or conversion.
                    return new ComparisonSpecificationResponse(metadata.DefinitionId, metadata.Key, metadata.Name,
                        metadata.DataType.ToString().ToLowerInvariant(), metadata.Unit, metadata.DisplayOrder,
                        values[0] != values[1], values);
                }).Where(x => !query.DifferencesOnly || x.IsDifferent).ToArray()))
            .Where(group => group.Specifications.Count != 0).ToArray();

        return Result<CompareDevicesResponse>.Success(new(first.Group, [first.Card, second.Card], query.DifferencesOnly, groups));
    }
}

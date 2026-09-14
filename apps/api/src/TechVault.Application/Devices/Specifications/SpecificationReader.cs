using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Application.Devices.Specifications;

public sealed record SpecificationResponse(Guid Id, string Key, string Name, string DataType, string? Unit,
    int DisplayOrder, string? ValueText, decimal? ValueNumber, bool? ValueBoolean, DateOnly? ValueDate);
public sealed record SpecificationGroupResponse(Guid Id, string Key, string Name, int DisplayOrder,
    IReadOnlyList<SpecificationResponse> Specifications);

internal static class SpecificationReader
{
    public static async Task<IReadOnlyList<SpecificationGroupResponse>> ReadAsync(ITechVaultDbContext db,
        Guid deviceId, CancellationToken cancellationToken)
    {
        var rows = await db.DeviceSpecifications.AsNoTracking()
            .Where(x => x.DeviceId == deviceId && db.Devices.Any(d => d.Id == x.DeviceId && d.Status == DeviceStatus.Published))
            .OrderBy(x => x.Definition.Group.DisplayOrder).ThenBy(x => x.Definition.Group.Key)
            .ThenBy(x => x.Definition.DisplayOrder).ThenBy(x => x.Definition.Key)
            .Select(x => new Row(x.Definition.Group.Id, x.Definition.Group.Key, x.Definition.Group.Name,
                x.Definition.Group.DisplayOrder, x.DefinitionId, x.Definition.Key, x.Definition.Name,
                x.DataType, x.Definition.Unit, x.Definition.DisplayOrder, x.ValueText, x.ValueNumber,
                x.ValueBoolean, x.ValueDate)).ToListAsync(cancellationToken);

        return rows.GroupBy(x => new { x.GroupId, x.GroupKey, x.GroupName, x.GroupOrder })
            .Select(group => new SpecificationGroupResponse(group.Key.GroupId, group.Key.GroupKey,
                group.Key.GroupName, group.Key.GroupOrder,
                group.Select(x => new SpecificationResponse(x.Id, x.Key, x.Name,
                    x.DataType.ToString().ToLowerInvariant(), x.Unit, x.DisplayOrder,
                    x.ValueText, x.ValueNumber, x.ValueBoolean, x.ValueDate)).ToArray())).ToArray();
    }

    private sealed record Row(Guid GroupId, string GroupKey, string GroupName, int GroupOrder,
        Guid Id, string Key, string Name, SpecificationDataType DataType, string? Unit, int DisplayOrder,
        string? ValueText, decimal? ValueNumber, bool? ValueBoolean, DateOnly? ValueDate);
}

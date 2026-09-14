using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Application.Common.Validation;
using TechVault.Application.Devices.Specifications;
using TechVault.Domain.Devices;

namespace TechVault.Application.Devices.GetDeviceSpecifications;

public sealed record GetDeviceSpecificationsQuery(string Slug);
public sealed record GetDeviceSpecificationsResponse(Guid DeviceId, string Name, string Slug,
    IReadOnlyList<SpecificationGroupResponse> SpecificationGroups);

public sealed class GetDeviceSpecificationsHandler(ITechVaultDbContext db)
{
    public async Task<Result<GetDeviceSpecificationsResponse>> HandleAsync(GetDeviceSpecificationsQuery query,
        CancellationToken cancellationToken)
    {
        var error = RequestValidation.Slug(query.Slug, "slug");
        if (error is not null) return Result<GetDeviceSpecificationsResponse>.Failure(error);
        var device = await db.Devices.AsNoTracking()
            .Where(x => x.Slug == query.Slug && x.Status == DeviceStatus.Published)
            .Select(x => new { x.Id, x.Name, x.Slug }).SingleOrDefaultAsync(cancellationToken);
        if (device is null) return Result<GetDeviceSpecificationsResponse>.Failure(Error.DeviceNotFound());
        var groups = await SpecificationReader.ReadAsync(db, device.Id, cancellationToken);
        return Result<GetDeviceSpecificationsResponse>.Success(new(device.Id, device.Name, device.Slug, groups));
    }
}

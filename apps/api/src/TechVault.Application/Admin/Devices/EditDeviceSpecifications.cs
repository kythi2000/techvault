using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Domain.Devices;

namespace TechVault.Application.Admin.Devices;

public sealed class EditDeviceSpecificationsHandler(ITechVaultDbContext db, IValidator<SpecificationInput> validator)
{
    public async Task<Result<AdminDeviceState>> SetAsync(Guid deviceId, Guid definitionId, SpecificationInput input, CancellationToken ct)
    {
        var error = await AdminValidation.ValidateAsync(validator, input, ct);
        if (error is not null) return Result<AdminDeviceState>.Failure(error);
        var device = await db.Devices.Include(x => x.Specifications).SingleOrDefaultAsync(x => x.Id == deviceId, ct);
        if (device is null) return Result<AdminDeviceState>.Failure(AdminValidation.Missing());
        if (device.Status == DeviceStatus.Archived)
            return Result<AdminDeviceState>.Failure(AdminValidation.Conflict("Archived devices are read-only."));
        var definition = await db.SpecificationDefinitions.SingleOrDefaultAsync(x => x.Id == definitionId, ct);
        if (definition is null) return Result<AdminDeviceState>.Failure(Error.Validation("definitionId must reference an existing definition."));
        error = AdminValidation.Domain(() => device.SetSpecification(definition, input.ToValue()));
        if (error is not null) return Result<AdminDeviceState>.Failure(error);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeviceState>.Success(AdminDeviceState.From(device));
    }

    public async Task<Result<AdminDeviceState>> RemoveAsync(Guid deviceId, Guid definitionId, CancellationToken ct)
    {
        var device = await db.Devices.Include(x => x.Specifications).SingleOrDefaultAsync(x => x.Id == deviceId, ct);
        if (device is null) return Result<AdminDeviceState>.Failure(AdminValidation.Missing());
        if (device.Status == DeviceStatus.Archived)
            return Result<AdminDeviceState>.Failure(AdminValidation.Conflict("Archived devices are read-only."));
        if (!device.RemoveSpecification(definitionId)) return Result<AdminDeviceState>.Failure(AdminValidation.Missing());
        await db.SaveChangesAsync(ct);
        return Result<AdminDeviceState>.Success(AdminDeviceState.From(device));
    }
}

using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Domain.Devices;

namespace TechVault.Application.Admin.Devices;

public enum DeviceAction { Publish, Unpublish, Archive }

public sealed class DeviceLifecycleHandler(ITechVaultDbContext db)
{
    public async Task<Result<AdminDeviceState>> HandleAsync(Guid id, DeviceAction action, CancellationToken ct)
    {
        var device = await db.Devices.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (device is null) return Result<AdminDeviceState>.Failure(AdminValidation.Missing());
        if (device.Status == DeviceStatus.Archived && action != DeviceAction.Archive)
            return Result<AdminDeviceState>.Failure(AdminValidation.Conflict("Archived devices are read-only."));
        var error = AdminValidation.Domain(() =>
        {
            switch (action)
            {
                case DeviceAction.Publish: device.Publish(); break;
                case DeviceAction.Unpublish: device.Unpublish(); break;
                case DeviceAction.Archive: device.Archive(); break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        });
        if (error is not null) return Result<AdminDeviceState>.Failure(error);
        await db.SaveChangesAsync(ct);
        return Result<AdminDeviceState>.Success(AdminDeviceState.From(device));
    }
}

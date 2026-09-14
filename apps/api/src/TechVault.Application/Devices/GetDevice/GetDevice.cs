using Microsoft.EntityFrameworkCore;
using TechVault.Application.Common.Abstractions;
using TechVault.Application.Common.Results;
using TechVault.Application.Common.Validation;
using TechVault.Application.Devices.Specifications;
using TechVault.Domain.Devices;

namespace TechVault.Application.Devices.GetDevice;

public sealed record GetDeviceQuery(string Slug);
public sealed record PhysicalDetailsResponse(decimal? HeightMm, decimal? WidthMm, decimal? DepthMm, decimal? WeightGrams);
public sealed record GetDeviceResponse(Guid Id, string Name, string Slug, string ShortDescription,
    string Description, string History, string SeoTitle, string SeoDescription,
    BrandReference Brand, CategoryReference Category, int? ReleaseYear, DateOnly? ReleaseDate,
    DateOnly? DiscontinuedDate, PhysicalDetailsResponse PhysicalDetails,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt)
{
    public IReadOnlyList<SpecificationGroupResponse> SpecificationGroups { get; init; } = [];
}

public sealed class GetDeviceHandler(ITechVaultDbContext db)
{
    public async Task<Result<GetDeviceResponse>> HandleAsync(GetDeviceQuery query, CancellationToken cancellationToken)
    {
        var error = RequestValidation.Slug(query.Slug, "slug");
        if (error is not null) return Result<GetDeviceResponse>.Failure(error);
        var device = await db.Devices.AsNoTracking()
            .Where(x => x.Slug == query.Slug && x.Status == DeviceStatus.Published)
            .Select(x => new GetDeviceResponse(x.Id, x.Name, x.Slug, x.ShortDescription, x.Description,
                x.History, x.SeoTitle, x.SeoDescription,
                new BrandReference(x.Brand.Id, x.Brand.Name, x.Brand.Slug),
                new CategoryReference(x.Category.Id, x.Category.Name, x.Category.Slug,
                    x.Category.ParentCategoryId, x.Category.Parent == null ? null : x.Category.Parent.Slug),
                x.ReleaseYear, x.ReleaseDate, x.DiscontinuedDate,
                new PhysicalDetailsResponse(x.HeightMm, x.WidthMm, x.DepthMm, x.WeightGrams),
                x.CreatedAt, x.UpdatedAt, x.PublishedAt)).SingleOrDefaultAsync(cancellationToken);
        if (device is null) return Result<GetDeviceResponse>.Failure(Error.DeviceNotFound());
        var groups = await SpecificationReader.ReadAsync(db, device.Id, cancellationToken);
        return Result<GetDeviceResponse>.Success(device with { SpecificationGroups = groups });
    }
}

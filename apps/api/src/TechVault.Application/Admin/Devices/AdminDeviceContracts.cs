using FluentValidation;
using TechVault.Application.Common.Pagination;
using TechVault.Domain.Devices;
using TechVault.Domain.Specifications;

namespace TechVault.Application.Admin.Devices;

public sealed record DeviceInput(string Name, string Slug, Guid BrandId, Guid CategoryId,
    Guid? ComparisonGroupId = null, string ShortDescription = "", string Description = "", string History = "",
    string SeoTitle = "", string SeoDescription = "", string? ModelNumber = null,
    int? ReleaseYear = null, DateOnly? ReleaseDate = null, DateOnly? DiscontinuedDate = null,
    decimal? HeightMm = null, decimal? WidthMm = null, decimal? DepthMm = null, decimal? WeightGrams = null)
{
    public IReadOnlyList<string> Aliases { get; init; } = [];
}

public sealed class DeviceInputValidator : AbstractValidator<DeviceInput>
{
    public DeviceInputValidator()
    {
        RuleFor(x => x).Must(x => AdminValidation.HasNoNul(x.Name, x.Slug, x.ShortDescription, x.Description,
            x.History, x.SeoTitle, x.SeoDescription, x.ModelNumber) && (x.Aliases is null || x.Aliases.All(a => AdminValidation.HasNoNul(a))))
            .WithMessage("Text cannot contain NUL characters.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(160).Matches(AdminValidation.SlugPattern);
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.ComparisonGroupId).NotEqual(Guid.Empty);
        RuleFor(x => x.ShortDescription).NotNull().MaximumLength(500);
        RuleFor(x => x.Description).NotNull().MaximumLength(100_000);
        RuleFor(x => x.History).NotNull().MaximumLength(100_000);
        RuleFor(x => x.SeoTitle).NotNull().MaximumLength(200);
        RuleFor(x => x.SeoDescription).NotNull().MaximumLength(500);
        RuleFor(x => x.ModelNumber).NotEmpty().MaximumLength(100).When(x => x.ModelNumber is not null);
        RuleFor(x => x.Aliases).Cascade(CascadeMode.Stop).NotNull().Must(x => x.Count <= 20).WithMessage("At most 20 aliases are allowed.");
        RuleForEach(x => x.Aliases).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ReleaseYear).InclusiveBetween(1, 9999).When(x => x.ReleaseYear.HasValue);
        RuleFor(x => x).Must(x => !x.ReleaseYear.HasValue || !x.ReleaseDate.HasValue || x.ReleaseYear == x.ReleaseDate.Value.Year)
            .WithMessage("Release date must match the release year.");
        RuleFor(x => x).Must(x => !x.DiscontinuedDate.HasValue ||
            ((!x.ReleaseDate.HasValue || x.DiscontinuedDate >= x.ReleaseDate) &&
             (!x.ReleaseYear.HasValue || x.DiscontinuedDate.Value.Year >= x.ReleaseYear)))
            .WithMessage("Discontinuation cannot precede release.");
        RuleFor(x => x.HeightMm).Must(x => x is null or > 0);
        RuleFor(x => x.WidthMm).Must(x => x is null or > 0);
        RuleFor(x => x.DepthMm).Must(x => x is null or > 0);
        RuleFor(x => x.WeightGrams).Must(x => x is null or > 0);
    }
}

public sealed record AdminDevicesQuery(string? Status = null, int Page = 1, int PageSize = PaginationRules.DefaultPageSize);
public sealed record AdminDeviceSummary(Guid Id, string Name, string Slug, string Status, Guid BrandId, Guid CategoryId,
    Guid? ComparisonGroupId, DateTimeOffset UpdatedAt);
public sealed record AdminDeviceState(Guid Id, string Slug, string Status, DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt)
{
    public static AdminDeviceState From(Device device) => new(device.Id, device.Slug, device.Status.ToString().ToLowerInvariant(), device.UpdatedAt, device.PublishedAt);
}
public sealed record AdminSpecificationResponse(Guid DefinitionId, string Key, string DataType, string? Unit,
    string? ValueText, decimal? ValueNumber, bool? ValueBoolean, DateOnly? ValueDate);
public sealed record AdminDeviceDetail(Guid Id, string Status, DeviceInput Content, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt, IReadOnlyList<AdminSpecificationResponse> Specifications);

public sealed record SpecificationInput(string? ValueText = null, decimal? ValueNumber = null,
    bool? ValueBoolean = null, DateOnly? ValueDate = null)
{
    internal SpecificationValue ToValue() => ValueText is not null ? SpecificationValue.Text(ValueText) :
        ValueNumber.HasValue ? SpecificationValue.Number(ValueNumber.Value) :
        ValueBoolean.HasValue ? SpecificationValue.Boolean(ValueBoolean.Value) : SpecificationValue.Date(ValueDate!.Value);
}

public sealed class SpecificationInputValidator : AbstractValidator<SpecificationInput>
{
    public SpecificationInputValidator()
    {
        RuleFor(x => x.ValueText).Must(x => AdminValidation.HasNoNul(x)).WithMessage("Text cannot contain NUL characters.");
        RuleFor(x => x).Must(x => new object?[] { x.ValueText, x.ValueNumber, x.ValueBoolean, x.ValueDate }.Count(v => v is not null) == 1)
            .WithMessage("Exactly one typed value is required.");
        RuleFor(x => x.ValueText).NotEmpty().MaximumLength(10_000).When(x => x.ValueText is not null);
    }
}

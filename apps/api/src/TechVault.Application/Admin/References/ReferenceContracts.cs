using FluentValidation;
using TechVault.Application.Common.Pagination;

namespace TechVault.Application.Admin.References;

public sealed record AdminReferenceQuery(int Page = 1, int PageSize = PaginationRules.DefaultPageSize);
public sealed record BrandInput(string Name, string Slug, string Description = "");
public sealed record AdminBrandResponse(Guid Id, string Name, string Slug, string Description);
public sealed record CategoryInput(string Name, string Slug, int DisplayOrder = 0,
    Guid? ParentCategoryId = null, string Description = "");
public sealed record AdminCategoryResponse(Guid Id, string Name, string Slug, string Description,
    int DisplayOrder, Guid? ParentCategoryId);
public sealed record SpecificationGroupInput(string Name, string Key, int DisplayOrder = 0);
public sealed record AdminSpecificationGroupResponse(Guid Id, string Name, string Key, int DisplayOrder);
public sealed record SpecificationDefinitionInput(string Name, string Key, Guid GroupId, string DataType,
    int DisplayOrder = 0, string? Unit = null, bool IsComparable = false);
public sealed record AdminSpecificationDefinitionResponse(Guid Id, string Name, string Key, Guid GroupId,
    string DataType, int DisplayOrder, string? Unit, bool IsComparable);
public sealed record AdminComparisonGroupResponse(Guid Id, string Name, string Key);

public sealed class BrandInputValidator : AbstractValidator<BrandInput>
{
    public BrandInputValidator()
    {
        RuleFor(x => x).Must(x => AdminValidation.HasNoNul(x.Name, x.Slug, x.Description)).WithMessage("Text cannot contain NUL characters.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(160).Matches(AdminValidation.SlugPattern);
        RuleFor(x => x.Description).NotNull().MaximumLength(100_000);
    }
}

public sealed class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator()
    {
        RuleFor(x => x).Must(x => AdminValidation.HasNoNul(x.Name, x.Slug, x.Description)).WithMessage("Text cannot contain NUL characters.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(160).Matches(AdminValidation.SlugPattern);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParentCategoryId).NotEqual(Guid.Empty);
        RuleFor(x => x.Description).NotNull().MaximumLength(100_000);
    }
}

public sealed class SpecificationGroupInputValidator : AbstractValidator<SpecificationGroupInput>
{
    public SpecificationGroupInputValidator()
    {
        RuleFor(x => x).Must(x => AdminValidation.HasNoNul(x.Name, x.Key)).WithMessage("Text cannot contain NUL characters.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100).Matches(AdminValidation.KeyPattern);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class SpecificationDefinitionInputValidator : AbstractValidator<SpecificationDefinitionInput>
{
    public SpecificationDefinitionInputValidator()
    {
        RuleFor(x => x).Must(x => AdminValidation.HasNoNul(x.Name, x.Key, x.Unit)).WithMessage("Text cannot contain NUL characters.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(100).Matches(AdminValidation.KeyPattern);
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.DataType).Must(x => x is "text" or "number" or "boolean" or "date")
            .WithMessage("Use text, number, boolean, or date.");
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50).When(x => x.Unit is not null);
    }
}

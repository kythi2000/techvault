using TechVault.Domain.Brands;
using TechVault.Domain.Categories;
using TechVault.Domain.Common;
using TechVault.Domain.Specifications;

namespace TechVault.Domain.Devices;

public sealed class Device : BaseEntity
{
    private readonly List<DeviceSpecification> _specifications = [];
    private List<string> _aliases = [];

    private Device() { }

    public Device(string name, string slug, Brand brand, Category category)
    {
        ArgumentNullException.ThrowIfNull(brand);
        ArgumentNullException.ThrowIfNull(category);
        Name = CatalogRules.Required(name, 200, nameof(name));
        Slug = CatalogRules.Slug(slug);
        Brand = brand;
        BrandId = brand.Id;
        Category = category;
        CategoryId = category.Id;
    }

    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? ModelNumber { get; private set; }
    public IReadOnlyList<string> Aliases => _aliases.AsReadOnly();
    public Guid BrandId { get; private set; }
    public Brand Brand { get; private set; } = null!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public string ShortDescription { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string History { get; private set; } = "";
    public string SeoTitle { get; private set; } = "";
    public string SeoDescription { get; private set; } = "";
    public int? ReleaseYear { get; private set; }
    public DateOnly? ReleaseDate { get; private set; }
    public DateOnly? DiscontinuedDate { get; private set; }
    public decimal? HeightMm { get; private set; }
    public decimal? WidthMm { get; private set; }
    public decimal? DepthMm { get; private set; }
    public decimal? WeightGrams { get; private set; }
    public DeviceStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; private set; }
    public IReadOnlyCollection<DeviceSpecification> Specifications => _specifications.AsReadOnly();

    public void SetSearchMetadata(string? modelNumber, IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        modelNumber = modelNumber is null ? null : CatalogRules.Required(modelNumber, 100, nameof(modelNumber));
        var values = aliases.Take(21).Select(x => CatalogRules.Required(x, 100, nameof(aliases))).ToList();
        if (values.Count > 20)
            throw new ArgumentException("A device can have at most 20 aliases.", nameof(aliases));

        // Validate the whole edit first, and never retain a caller-owned mutable collection.
        _aliases = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        ModelNumber = modelNumber;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateContent(string shortDescription, string description, string history,
        string seoTitle, string seoDescription)
    {
        // Validate everything before changing any editorial content.
        shortDescription = CatalogRules.Required(shortDescription, 500, nameof(shortDescription));
        description = CatalogRules.Required(description, 100_000, nameof(description));
        history = CatalogRules.Required(history, 100_000, nameof(history));
        seoTitle = CatalogRules.Required(seoTitle, 200, nameof(seoTitle));
        seoDescription = CatalogRules.Required(seoDescription, 500, nameof(seoDescription));

        ShortDescription = shortDescription;
        Description = description;
        History = history;
        SeoTitle = seoTitle;
        SeoDescription = seoDescription;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetRelease(int? year, DateOnly? releaseDate = null, DateOnly? discontinuedDate = null)
    {
        year ??= releaseDate?.Year;
        if (year is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(year));
        if (releaseDate.HasValue && releaseDate.Value.Year != year)
            throw new ArgumentException("Release date must match the release year.", nameof(releaseDate));
        if (discontinuedDate.HasValue &&
            ((releaseDate.HasValue && discontinuedDate < releaseDate) || discontinuedDate.Value.Year < year))
            throw new ArgumentException("Discontinuation cannot precede release.", nameof(discontinuedDate));

        ReleaseYear = year;
        ReleaseDate = releaseDate;
        DiscontinuedDate = discontinuedDate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPhysicalDetails(decimal? heightMm, decimal? widthMm, decimal? depthMm, decimal? weightGrams)
    {
        if (heightMm is <= 0 || widthMm is <= 0 || depthMm is <= 0 || weightGrams is <= 0)
            throw new ArgumentOutOfRangeException(nameof(heightMm), "Known physical measurements must be positive.");
        HeightMm = heightMm;
        WidthMm = widthMm;
        DepthMm = depthMm;
        WeightGrams = weightGrams;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // Load the device's specifications before editing an existing device.
    public void SetSpecification(SpecificationDefinition definition, SpecificationValue value)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(value);
        if (definition.DataType != value.DataType)
            throw new ArgumentException("Specification value does not match its definition's data type.", nameof(value));

        var existing = _specifications.SingleOrDefault(x => x.DefinitionId == definition.Id);
        if (existing is null)
            _specifications.Add(new DeviceSpecification(Id, definition, value));
        else
            existing.SetValue(value);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Publish()
    {
        if (Status == DeviceStatus.Published)
            return;
        if (Status != DeviceStatus.Draft)
            throw new InvalidOperationException("Only a draft can be published.");
        if (string.IsNullOrWhiteSpace(ShortDescription) || string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(History) || string.IsNullOrWhiteSpace(SeoTitle) ||
            string.IsNullOrWhiteSpace(SeoDescription))
            throw new InvalidOperationException("Publication requires descriptive, historical, and SEO content.");

        Status = DeviceStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedAt = PublishedAt.Value;
    }
}

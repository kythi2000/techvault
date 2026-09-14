namespace TechVault.Domain.Specifications;

// A typed value prevents ambiguous parsing and makes false/zero different from unknown.
public sealed record SpecificationValue
{
    private SpecificationValue(SpecificationDataType dataType, string? text = null,
        decimal? number = null, bool? boolean = null, DateOnly? date = null)
    {
        DataType = dataType;
        ValueText = text;
        ValueNumber = number;
        ValueBoolean = boolean;
        ValueDate = date;
    }

    public SpecificationDataType DataType { get; }
    public string? ValueText { get; }
    public decimal? ValueNumber { get; }
    public bool? ValueBoolean { get; }
    public DateOnly? ValueDate { get; }

    public static SpecificationValue Text(string value) =>
        new(SpecificationDataType.Text, text: CatalogRules.Required(value, 10_000, nameof(value)));
    public static SpecificationValue Number(decimal value) => new(SpecificationDataType.Number, number: value);
    public static SpecificationValue Boolean(bool value) => new(SpecificationDataType.Boolean, boolean: value);
    public static SpecificationValue Date(DateOnly value) => new(SpecificationDataType.Date, date: value);
}

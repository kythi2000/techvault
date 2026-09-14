namespace TechVault.Domain.Specifications;

public sealed class DeviceSpecification
{
    private DeviceSpecification() { }

    internal DeviceSpecification(Guid deviceId, SpecificationDefinition definition, SpecificationValue value)
    {
        DeviceId = deviceId;
        Definition = definition;
        DefinitionId = definition.Id;
        DataType = definition.DataType;
        SetValue(value);
    }

    public Guid DeviceId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public SpecificationDefinition Definition { get; private set; } = null!;
    public SpecificationDataType DataType { get; private set; }
    public string? ValueText { get; private set; }
    public decimal? ValueNumber { get; private set; }
    public bool? ValueBoolean { get; private set; }
    public DateOnly? ValueDate { get; private set; }

    internal void SetValue(SpecificationValue value)
    {
        if (value.DataType != DataType)
            throw new ArgumentException("Specification data types must match.", nameof(value));
        ValueText = value.ValueText;
        ValueNumber = value.ValueNumber;
        ValueBoolean = value.ValueBoolean;
        ValueDate = value.ValueDate;
    }
}

namespace TechVault.Domain.Common;

// Share identity only; audit fields and business behavior belong to the entities that need them.
public abstract class BaseEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
}

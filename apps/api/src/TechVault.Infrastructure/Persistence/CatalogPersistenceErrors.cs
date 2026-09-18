using Microsoft.EntityFrameworkCore;
using Npgsql;
using TechVault.Application.Common.Results;

namespace TechVault.Infrastructure.Persistence;

// HTTP never exposes provider messages, SQL, constraint details, or request values.
// Constraints are also checked before writes; this handles races that pass those checks.
public static class CatalogPersistenceErrors
{
    public static Error? Classify(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => new("CATALOG_CONFLICT", "Catalog data changed during this request; reload and retry.", ErrorType.Conflict),
        DbUpdateException { InnerException: PostgresException postgres } => postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => new("DUPLICATE_IDENTIFIER", "An identifier is already in use.", ErrorType.Conflict),
            PostgresErrorCodes.ForeignKeyViolation => new("REFERENCE_CONFLICT", "A reference is missing or still in use.", ErrorType.Conflict),
            PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation or PostgresErrorCodes.StringDataRightTruncation
                => Error.Validation("Catalog data violates a storage constraint."),
            _ => null
        },
        _ => null
    };
}

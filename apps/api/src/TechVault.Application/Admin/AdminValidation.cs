using FluentValidation;
using TechVault.Application.Common.Results;

namespace TechVault.Application.Admin;

internal static class AdminValidation
{
    internal const string SlugPattern = "\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z";
    internal const string KeyPattern = "\\A[a-z][a-z0-9]*(?:_[a-z0-9]+)*\\z";

    internal static bool HasNoNul(params string?[] values) => values.All(x => x is null || !x.Contains('\0'));

    internal static async Task<Error?> ValidateAsync<T>(IValidator<T> validator, T input, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(input, ct);
        return result.IsValid ? null : Error.Validation(string.Join(" ", result.Errors.Take(10)
            .Select(x => $"{x.PropertyName}: {x.ErrorMessage}")));
    }

    internal static Error? Domain(Action edit)
    {
        try { edit(); return null; }
        catch (ArgumentException exception) { return Error.Validation(exception.Message); }
        catch (InvalidOperationException exception) { return Error.Validation(exception.Message); }
    }

    internal static Error Missing() => new("ADMIN_RESOURCE_NOT_FOUND", "Catalog resource was not found.", ErrorType.NotFound);
    internal static Error Conflict(string message, string code = "CATALOG_CONFLICT") => new(code, message, ErrorType.Conflict);
    internal static Error Duplicate() => Conflict("An identifier is already in use.", "DUPLICATE_IDENTIFIER");
    internal static Error Referenced() => Conflict("This resource is still referenced and cannot be deleted.", "REFERENCE_CONFLICT");
}

public sealed record AdminDeletedResponse(Guid Id);

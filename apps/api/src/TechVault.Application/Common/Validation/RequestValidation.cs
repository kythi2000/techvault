using System.Text.RegularExpressions;
using TechVault.Application.Common.Results;

namespace TechVault.Application.Common.Validation;

public static class RequestValidation
{
    public static Error? Slug(string? value, string parameter) =>
        value is null || value.Length > 160 || !Regex.IsMatch(value, "\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z")
            ? Error.Validation($"{parameter} must be a lowercase slug of at most 160 characters.") : null;
}

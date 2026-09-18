using System.Text.RegularExpressions;

namespace TechVault.Domain;

internal static class CatalogRules
{
    public static string Text(string value, int maxLength, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"Must contain at most {maxLength} characters.", parameter);
        return value;
    }

    public static string Required(string value, int maxLength, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"Must contain at most {maxLength} characters.", parameter);
        return value;
    }

    public static string Slug(string value)
    {
        value = Required(value, 160, nameof(value));
        if (!Regex.IsMatch(value, "\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z"))
            throw new ArgumentException("Use lowercase letters, digits, and single separating hyphens.", nameof(value));
        return value;
    }

    public static string Key(string value)
    {
        value = Required(value, 100, nameof(value));
        if (!Regex.IsMatch(value, "\\A[a-z][a-z0-9]*(?:_[a-z0-9]+)*\\z"))
            throw new ArgumentException("Use lowercase snake_case keys beginning with a letter.", nameof(value));
        return value;
    }
}

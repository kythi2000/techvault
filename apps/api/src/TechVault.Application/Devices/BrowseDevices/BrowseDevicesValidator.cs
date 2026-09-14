using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Application.Common.Validation;

namespace TechVault.Application.Devices.BrowseDevices;

public static class BrowseDevicesValidator
{
    public static Error? Validate(BrowseDevicesQuery query, string? requiredType = null)
    {
        var error = PaginationRules.Validate(query.Page, query.PageSize);
        if (error is not null) return error;
        if (query.Brand is not null && (error = RequestValidation.Slug(query.Brand, "brand")) is not null) return error;
        if (query.Category is not null && (error = RequestValidation.Slug(query.Category, "category")) is not null) return error;
        if (query.Type is not null and not "phones" and not "computers")
            return Error.Validation("type must be phones or computers.");
        if (requiredType is not null && query.Type is not null && query.Type != requiredType)
            return Error.Validation($"type conflicts with the {requiredType} endpoint.");
        if (query.Sort is not "name-asc" and not "name-desc" and not "release-asc" and not "release-desc")
            return Error.Validation("sort must be name-asc, name-desc, release-asc, or release-desc.");
        if (query.Year is < 1 or > 9999 || query.FromYear is < 1 or > 9999 || query.ToYear is < 1 or > 9999)
            return Error.Validation("Years must be between 1 and 9999.");
        if (query.FromYear > query.ToYear)
            return Error.Validation("fromYear must not exceed toYear.");
        if (query.Decade.HasValue && (query.Decade is < 10 or > 9990 || query.Decade % 10 != 0))
            return Error.Validation("decade must be a multiple of 10 between 10 and 9990, such as 1990.");
        return null;
    }
}

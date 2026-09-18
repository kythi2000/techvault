using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;
using TechVault.Application.Devices.BrowseDevices;

namespace TechVault.Application.Search;

public sealed record SearchDevicesQuery(string? Q = null, int Page = 1, int PageSize = PaginationRules.DefaultPageSize);

public static class SearchDevicesValidator
{
    public static Error? Validate(SearchDevicesQuery query)
    {
        var error = PaginationRules.Validate(query.Page, query.PageSize);
        if (error is not null) return error;
        if (string.IsNullOrWhiteSpace(query.Q) || query.Q.Length > 200 || !query.Q.Any(char.IsLetterOrDigit))
            return Error.Validation("q must contain 1 to 200 characters and at least one letter or digit.");
        return null;
    }
}

// The PostgreSQL-specific full-text query stays in Infrastructure, not in the Application or Domain model.
public interface IDeviceSearch
{
    Task<PagedResult<DeviceCardResponse>> SearchAsync(string text, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class SearchDevicesHandler(IDeviceSearch search)
{
    public async Task<Result<PagedResult<DeviceCardResponse>>> HandleAsync(SearchDevicesQuery query, CancellationToken cancellationToken)
    {
        var error = SearchDevicesValidator.Validate(query);
        if (error is not null) return Result<PagedResult<DeviceCardResponse>>.Failure(error);
        var result = await search.SearchAsync(query.Q!.Trim(), query.Page, query.PageSize, cancellationToken);
        return Result<PagedResult<DeviceCardResponse>>.Success(result);
    }
}

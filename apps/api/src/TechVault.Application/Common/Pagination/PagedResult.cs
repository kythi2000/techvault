using TechVault.Application.Common.Results;

namespace TechVault.Application.Common.Pagination;

public sealed record PaginationMetadata(int Page, int PageSize, int Total, int TotalPages);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, PaginationMetadata Pagination)
{
    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int total) =>
        new(items, new(page, pageSize, total, (int)Math.Ceiling((double)total / pageSize)));
}

public static class PaginationRules
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;
    public const int MaxPage = 10_000;

    public static Error? Validate(int page, int pageSize) =>
        page is < 1 or > MaxPage ? Error.Validation($"page must be between 1 and {MaxPage}.") :
        pageSize is < 1 or > MaxPageSize ? Error.Validation($"pageSize must be between 1 and {MaxPageSize}.") : null;
}

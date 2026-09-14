using System.Diagnostics;
using TechVault.Application.Common.Pagination;
using TechVault.Application.Common.Results;

namespace TechVault.Api.Responses;

public sealed record ApiResponse<T>(T Data);
public sealed record PaginatedResponse<T>(IReadOnlyList<T> Data, PaginationMetadata Pagination);
public sealed record ApiError(string Code, string Message, string TraceId);
public sealed record ApiErrorResponse(ApiError Error);

internal static class ApiResponses
{
    public static string TraceId(HttpContext context) => Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext context) where T : notnull =>
        result.IsSuccess ? TypedResults.Ok(new ApiResponse<T>(result.Value)) : Failure(result.Error!, context);

    public static IResult ToPagedHttpResult<T>(this Result<PagedResult<T>> result, HttpContext context) =>
        result.IsSuccess ? TypedResults.Ok(new PaginatedResponse<T>(result.Value.Items, result.Value.Pagination)) : Failure(result.Error!, context);

    private static IResult Failure(Error error, HttpContext context) => Results.Json(
        new ApiErrorResponse(new(error.Code, error.Message, TraceId(context))),
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            _ => throw new InvalidOperationException("Unmapped application error type.")
        });
}

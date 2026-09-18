using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TechVault.Api.Middleware;
using TechVault.Application.Admin.References;
using TechVault.Infrastructure.Persistence;
using static TechVault.IntegrationTests.AdminTestHttp;

namespace TechVault.IntegrationTests;

public sealed class AdminPersistenceErrorTests(AdminCatalogFixture fixture) : IClassFixture<AdminCatalogFixture>
{
    [Fact]
    public async Task Concurrent_duplicate_creates_have_one_winner_and_a_consistent_conflict()
    {
        var input = new BrandInput("Concurrent brand", "concurrent-brand");
        var requests = Enumerable.Range(0, 6).Select(_ => fixture.Client.PostAsJsonAsync("/api/v1/admin/brands", input, Ct));
        var responses = await Task.WhenAll(requests);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.Created) response.Dispose();
            else await Error(response, HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        }
        await using var db = fixture.Catalog.CreateContext();
        Assert.Equal(1, await db.Brands.CountAsync(x => x.Slug == input.Slug, Ct));
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, 409, "DUPLICATE_IDENTIFIER")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, 409, "REFERENCE_CONFLICT")]
    [InlineData(PostgresErrorCodes.CheckViolation, 400, "VALIDATION_ERROR")]
    [InlineData(PostgresErrorCodes.NotNullViolation, 400, "VALIDATION_ERROR")]
    [InlineData(PostgresErrorCodes.StringDataRightTruncation, 400, "VALIDATION_ERROR")]
    public async Task Storage_failures_are_mapped_without_leaking_provider_details(string sqlState, int status, string code)
    {
        var exception = new DbUpdateException("private SQL", new PostgresException("private value", "ERROR", "ERROR", sqlState));
        var middleware = new ApiErrorHandlingMiddleware(_ => throw exception, NullLogger<ApiErrorHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/admin/brands";
        using var body = new MemoryStream();
        context.Response.Body = body;
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        body.Position = 0;
        var content = await new StreamReader(body).ReadToEndAsync(Ct);
        Assert.Contains(code, content);
        Assert.DoesNotContain("private", content);
        Assert.Null(CatalogPersistenceErrors.Classify(new InvalidOperationException()));
        Assert.Equal("CATALOG_CONFLICT", CatalogPersistenceErrors.Classify(new DbUpdateConcurrencyException())!.Code);
    }
}

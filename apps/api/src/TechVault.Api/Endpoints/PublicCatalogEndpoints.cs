using TechVault.Api.Responses;
using TechVault.Application.Brands;
using TechVault.Application.Brands.GetBrand;
using TechVault.Application.Brands.GetBrands;
using TechVault.Application.Categories.GetCategories;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Devices.GetDevice;
using TechVault.Application.Devices.GetDeviceSpecifications;

namespace TechVault.Api.Endpoints;

public static class PublicCatalogEndpoints
{
    public static IEndpointRouteBuilder MapPublicCatalog(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").WithTags("Catalog");

        api.MapGet("/devices", async ([AsParameters] BrowseDevicesQuery query, BrowseDevicesHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(query, cancellationToken)).ToPagedHttpResult(context))
            .WithName("BrowseDevices").Produces<PaginatedResponse<DeviceCardResponse>>()
            .Produces<ApiErrorResponse>(400);

        api.MapGet("/phones", async ([AsParameters] BrowseDevicesQuery query, BrowseDevicesHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(query, cancellationToken, "phones")).ToPagedHttpResult(context))
            .WithName("BrowsePhones").Produces<PaginatedResponse<DeviceCardResponse>>()
            .Produces<ApiErrorResponse>(400);

        api.MapGet("/computers", async ([AsParameters] BrowseDevicesQuery query, BrowseDevicesHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(query, cancellationToken, "computers")).ToPagedHttpResult(context))
            .WithName("BrowseComputers").Produces<PaginatedResponse<DeviceCardResponse>>()
            .Produces<ApiErrorResponse>(400);

        api.MapGet("/devices/{slug}", async (string slug, GetDeviceHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(new(slug), cancellationToken)).ToHttpResult(context))
            .WithName("GetDevice").Produces<ApiResponse<GetDeviceResponse>>()
            .Produces<ApiErrorResponse>(400).Produces<ApiErrorResponse>(404);

        api.MapGet("/devices/{slug}/specifications", async (string slug, GetDeviceSpecificationsHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(new(slug), cancellationToken)).ToHttpResult(context))
            .WithName("GetDeviceSpecifications").Produces<ApiResponse<GetDeviceSpecificationsResponse>>()
            .Produces<ApiErrorResponse>(400).Produces<ApiErrorResponse>(404);

        api.MapGet("/brands", async ([AsParameters] GetBrandsQuery query, GetBrandsHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(query, cancellationToken)).ToPagedHttpResult(context))
            .WithName("GetBrands").Produces<PaginatedResponse<BrandResponse>>()
            .Produces<ApiErrorResponse>(400);

        api.MapGet("/brands/{slug}", async (string slug, GetBrandHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(new(slug), cancellationToken)).ToHttpResult(context))
            .WithName("GetBrand").Produces<ApiResponse<BrandResponse>>()
            .Produces<ApiErrorResponse>(400).Produces<ApiErrorResponse>(404);

        api.MapGet("/categories", async ([AsParameters] GetCategoriesQuery query, GetCategoriesHandler handler,
                HttpContext context, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(query, cancellationToken)).ToPagedHttpResult(context))
            .WithName("GetCategories").Produces<PaginatedResponse<CategoryResponse>>()
            .Produces<ApiErrorResponse>(400);

        return endpoints;
    }
}

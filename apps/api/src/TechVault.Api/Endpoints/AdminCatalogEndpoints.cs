using Microsoft.AspNetCore.Http.Metadata;
using TechVault.Api.Authentication;
using TechVault.Api.Responses;
using TechVault.Application.Admin;
using TechVault.Application.Admin.Devices;
using TechVault.Application.Admin.References;

namespace TechVault.Api.Endpoints;

public static class AdminCatalogEndpoints
{
    public static IEndpointRouteBuilder MapAdminCatalog(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin").WithTags("Admin")
            .RequireAuthorization(AdminAuthentication.Policy);
        foreach (var status in new[] { 400, 401, 403, 404, 409 })
            admin.WithMetadata(new ProducesResponseTypeMetadata(status, typeof(ApiErrorResponse), ["application/json"]));
        MapDevices(admin);
        MapBrands(admin);
        MapCategories(admin);
        MapSpecifications(admin);
        admin.MapGet("/comparison-groups", async ([AsParameters] AdminReferenceQuery query,
                GetAdminComparisonGroupsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(query, ct)).ToPagedHttpResult(context))
            .Produces<PaginatedResponse<AdminComparisonGroupResponse>>();
        return endpoints;
    }

    private static void MapDevices(RouteGroupBuilder admin)
    {
        var devices = admin.MapGroup("/devices");
        devices.MapGet("/", async ([AsParameters] AdminDevicesQuery query, GetAdminDevicesHandler handler,
                HttpContext context, CancellationToken ct) =>
            (await handler.ListAsync(query, ct)).ToPagedHttpResult(context))
            .Produces<PaginatedResponse<AdminDeviceSummary>>();
        devices.MapGet("/{id:guid}", async (Guid id, GetAdminDevicesHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.GetAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceDetail>>();
        devices.MapPost("/", async (DeviceInput input, SaveAdminDeviceHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(null, input, ct)).ToCreatedHttpResult(context, x => $"/api/v1/admin/devices/{x.Id}"))
            .Produces<ApiResponse<AdminDeviceState>>(201);
        devices.MapPut("/{id:guid}", async (Guid id, DeviceInput input, SaveAdminDeviceHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(id, input, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
        devices.MapDelete("/{id:guid}", async (Guid id, DeviceLifecycleHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(id, DeviceAction.Archive, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
        devices.MapPost("/{id:guid}/publish", async (Guid id, DeviceLifecycleHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(id, DeviceAction.Publish, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
        devices.MapPost("/{id:guid}/unpublish", async (Guid id, DeviceLifecycleHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(id, DeviceAction.Unpublish, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
        devices.MapPost("/{id:guid}/archive", async (Guid id, DeviceLifecycleHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.HandleAsync(id, DeviceAction.Archive, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
        devices.MapPut("/{id:guid}/specifications/{definitionId:guid}", async (Guid id, Guid definitionId,
                SpecificationInput input, EditDeviceSpecificationsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SetAsync(id, definitionId, input, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
        devices.MapDelete("/{id:guid}/specifications/{definitionId:guid}", async (Guid id, Guid definitionId,
                EditDeviceSpecificationsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.RemoveAsync(id, definitionId, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeviceState>>();
    }

    private static void MapBrands(RouteGroupBuilder admin)
    {
        var brands = admin.MapGroup("/brands");
        brands.MapGet("/", async ([AsParameters] AdminReferenceQuery query, ManageBrandsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.ListAsync(query, ct)).ToPagedHttpResult(context)).Produces<PaginatedResponse<AdminBrandResponse>>();
        brands.MapGet("/{id:guid}", async (Guid id, ManageBrandsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.GetAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminBrandResponse>>();
        brands.MapPost("/", async (BrandInput input, ManageBrandsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(null, input, ct)).ToCreatedHttpResult(context, x => $"/api/v1/admin/brands/{x.Id}"))
            .Produces<ApiResponse<AdminBrandResponse>>(201);
        brands.MapPut("/{id:guid}", async (Guid id, BrandInput input, ManageBrandsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(id, input, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminBrandResponse>>();
        brands.MapDelete("/{id:guid}", async (Guid id, ManageBrandsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.DeleteAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeletedResponse>>();
    }

    private static void MapCategories(RouteGroupBuilder admin)
    {
        var categories = admin.MapGroup("/categories");
        categories.MapGet("/", async ([AsParameters] AdminReferenceQuery query, ManageCategoriesHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.ListAsync(query, ct)).ToPagedHttpResult(context)).Produces<PaginatedResponse<AdminCategoryResponse>>();
        categories.MapGet("/{id:guid}", async (Guid id, ManageCategoriesHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.GetAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminCategoryResponse>>();
        categories.MapPost("/", async (CategoryInput input, ManageCategoriesHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(null, input, ct)).ToCreatedHttpResult(context, x => $"/api/v1/admin/categories/{x.Id}"))
            .Produces<ApiResponse<AdminCategoryResponse>>(201);
        categories.MapPut("/{id:guid}", async (Guid id, CategoryInput input, ManageCategoriesHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(id, input, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminCategoryResponse>>();
        categories.MapDelete("/{id:guid}", async (Guid id, ManageCategoriesHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.DeleteAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeletedResponse>>();
    }

    private static void MapSpecifications(RouteGroupBuilder admin)
    {
        var groups = admin.MapGroup("/specification-groups");
        groups.MapGet("/", async ([AsParameters] AdminReferenceQuery query, ManageSpecificationGroupsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.ListAsync(query, ct)).ToPagedHttpResult(context)).Produces<PaginatedResponse<AdminSpecificationGroupResponse>>();
        groups.MapGet("/{id:guid}", async (Guid id, ManageSpecificationGroupsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.GetAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminSpecificationGroupResponse>>();
        groups.MapPost("/", async (SpecificationGroupInput input, ManageSpecificationGroupsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(null, input, ct)).ToCreatedHttpResult(context, x => $"/api/v1/admin/specification-groups/{x.Id}"))
            .Produces<ApiResponse<AdminSpecificationGroupResponse>>(201);
        groups.MapPut("/{id:guid}", async (Guid id, SpecificationGroupInput input, ManageSpecificationGroupsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(id, input, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminSpecificationGroupResponse>>();
        groups.MapDelete("/{id:guid}", async (Guid id, ManageSpecificationGroupsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.DeleteAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeletedResponse>>();

        var definitions = admin.MapGroup("/specification-definitions");
        definitions.MapGet("/", async ([AsParameters] AdminReferenceQuery query, ManageSpecificationDefinitionsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.ListAsync(query, ct)).ToPagedHttpResult(context)).Produces<PaginatedResponse<AdminSpecificationDefinitionResponse>>();
        definitions.MapGet("/{id:guid}", async (Guid id, ManageSpecificationDefinitionsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.GetAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminSpecificationDefinitionResponse>>();
        definitions.MapPost("/", async (SpecificationDefinitionInput input, ManageSpecificationDefinitionsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(null, input, ct)).ToCreatedHttpResult(context, x => $"/api/v1/admin/specification-definitions/{x.Id}"))
            .Produces<ApiResponse<AdminSpecificationDefinitionResponse>>(201);
        definitions.MapPut("/{id:guid}", async (Guid id, SpecificationDefinitionInput input, ManageSpecificationDefinitionsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.SaveAsync(id, input, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminSpecificationDefinitionResponse>>();
        definitions.MapDelete("/{id:guid}", async (Guid id, ManageSpecificationDefinitionsHandler handler, HttpContext context, CancellationToken ct) =>
            (await handler.DeleteAsync(id, ct)).ToHttpResult(context)).Produces<ApiResponse<AdminDeletedResponse>>();
    }
}

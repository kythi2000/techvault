using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TechVault.Api.Responses;
using TechVault.Application.Admin;
using TechVault.Application.Admin.Devices;
using TechVault.Application.Admin.References;
using TechVault.Application.Comparisons;
using static TechVault.IntegrationTests.AdminTestHttp;

namespace TechVault.IntegrationTests;

public sealed class AdminReferenceTests(AdminCatalogFixture fixture) : IClassFixture<AdminCatalogFixture>
{
    private HttpClient Client => fixture.Client;

    [Fact]
    public async Task Brands_and_hierarchical_categories_support_safe_crud_without_reparenting_or_referenced_deletion()
    {
        const string brands = "/api/v1/admin/brands";
        const string categories = "/api/v1/admin/categories";
        var brandInput = new BrandInput("Private brand", "private-brand", "Before");
        var brand = await Data<AdminBrandResponse>(await Client.PostAsJsonAsync(brands, brandInput, Ct), HttpStatusCode.Created);
        await Error(await Client.PostAsJsonAsync(brands, brandInput, Ct), HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        var brandPath = $"{brands}/{brand.Id}";
        brand = await Data<AdminBrandResponse>(await Client.PutAsJsonAsync(brandPath, brandInput with { Name = "Renamed", Description = "After" }, Ct));
        Assert.Equal("Renamed", brand.Name);
        Assert.Equal("After", (await Data<AdminBrandResponse>(await Client.GetAsync(brandPath, Ct))).Description);
        await Error(await Client.PutAsJsonAsync(brandPath, brandInput with { Slug = "different" }, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");

        var parentInput = new CategoryInput("Parent", "private-parent");
        var parent = await Data<AdminCategoryResponse>(await Client.PostAsJsonAsync(categories, parentInput, Ct), HttpStatusCode.Created);
        var input = new CategoryInput("Child", "private-child", ParentCategoryId: parent.Id);
        var child = await Data<AdminCategoryResponse>(await Client.PostAsJsonAsync(categories, input, Ct), HttpStatusCode.Created);
        var childPath = $"{categories}/{child.Id}";
        await Error(await Client.PostAsJsonAsync(categories, input, Ct), HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        child = await Data<AdminCategoryResponse>(await Client.PutAsJsonAsync(childPath, input with { DisplayOrder = 8, Description = "Child description" }, Ct));
        Assert.Equal(parent.Id, child.ParentCategoryId);
        Assert.Equal(8, (await Data<AdminCategoryResponse>(await Client.GetAsync(childPath, Ct))).DisplayOrder);
        await Error(await Client.PutAsJsonAsync(childPath, input with { ParentCategoryId = child.Id }, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Client.PutAsJsonAsync(childPath, input with { Slug = "different" }, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Client.DeleteAsync($"{categories}/{parent.Id}", Ct), HttpStatusCode.Conflict, "REFERENCE_CONFLICT");
        await Error(await Client.PostAsJsonAsync(categories, new CategoryInput("Missing parent", "missing-parent", ParentCategoryId: Guid.NewGuid()), Ct), HttpStatusCode.BadRequest);
        await Data<AdminDeletedResponse>(await Client.DeleteAsync(childPath, Ct));
        await Data<AdminDeletedResponse>(await Client.DeleteAsync($"{categories}/{parent.Id}", Ct));
        await Data<AdminDeletedResponse>(await Client.DeleteAsync(brandPath, Ct));
        await Error(await Client.GetAsync(childPath, Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
        await Error(await Client.DeleteAsync(brandPath, Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");

        await using var db = fixture.Catalog.CreateContext();
        var nokia = await db.Brands.SingleAsync(x => x.Slug == "nokia", Ct);
        var phones = await db.Categories.SingleAsync(x => x.Slug == "feature-phones", Ct);
        await Error(await Client.DeleteAsync($"{brands}/{nokia.Id}", Ct), HttpStatusCode.Conflict, "REFERENCE_CONFLICT");
        await Error(await Client.DeleteAsync($"{categories}/{phones.Id}", Ct), HttpStatusCode.Conflict, "REFERENCE_CONFLICT");
    }

    [Fact]
    public async Task Definition_semantics_are_immutable_but_labels_group_order_and_comparability_are_editable()
    {
        const string groups = "/api/v1/admin/specification-groups";
        const string definitions = "/api/v1/admin/specification-definitions";
        var groupInput = new SpecificationGroupInput("Editorial specs", "editorial_specs");
        var group = await Data<AdminSpecificationGroupResponse>(await Client.PostAsJsonAsync(groups, groupInput, Ct), HttpStatusCode.Created);
        var groupPath = $"{groups}/{group.Id}";
        await Error(await Client.PostAsJsonAsync(groups, groupInput, Ct), HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        await Error(await Client.PutAsJsonAsync(groupPath, groupInput with { Key = "different" }, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Data<AdminSpecificationGroupResponse>(await Client.PutAsJsonAsync(groupPath, groupInput with { Name = "Edited group", DisplayOrder = 3 }, Ct));
        Assert.Equal(3, (await Data<AdminSpecificationGroupResponse>(await Client.GetAsync(groupPath, Ct))).DisplayOrder);
        var input = new SpecificationDefinitionInput("Editorial metric", "editorial_metric", group.Id, "number", Unit: "mm", IsComparable: true);
        var definition = await Data<AdminSpecificationDefinitionResponse>(await Client.PostAsJsonAsync(definitions, input, Ct), HttpStatusCode.Created);
        var definitionPath = $"{definitions}/{definition.Id}";
        await Error(await Client.PostAsJsonAsync(definitions, input, Ct), HttpStatusCode.Conflict, "DUPLICATE_IDENTIFIER");
        await Error(await Client.DeleteAsync(groupPath, Ct), HttpStatusCode.Conflict, "REFERENCE_CONFLICT");
        Guid phoneId;
        await using (var db = fixture.Catalog.CreateContext())
            phoneId = await db.Devices.Where(x => x.Slug == "nokia-3310").Select(x => x.Id).SingleAsync(Ct);
        var specPath = $"/api/v1/admin/devices/{phoneId}/specifications/{definition.Id}";
        await Data<AdminDeviceState>(await Client.PutAsJsonAsync(specPath, new SpecificationInput(ValueNumber: 0), Ct));
        foreach (var invalid in new[] { input with { DataType = "text" }, input with { Unit = "cm" }, input with { Key = "different" } })
            await Error(await Client.PutAsJsonAsync(definitionPath, invalid, Ct), HttpStatusCode.Conflict, "CATALOG_CONFLICT");
        await Error(await Client.DeleteAsync(definitionPath, Ct), HttpStatusCode.Conflict, "REFERENCE_CONFLICT");
        await Error(await Client.PutAsJsonAsync(definitionPath, input with { GroupId = Guid.NewGuid() }, Ct), HttpStatusCode.BadRequest);
        await Error(await Client.PutAsJsonAsync(specPath, new SpecificationInput(ValueText: "wrong type"), Ct), HttpStatusCode.BadRequest);
        await Error(await Client.PutAsJsonAsync(specPath, new SpecificationInput(), Ct), HttpStatusCode.BadRequest);
        await Error(await Client.PutAsJsonAsync(specPath, new SpecificationInput(ValueNumber: 1, ValueBoolean: false), Ct), HttpStatusCode.BadRequest);
        var comparison = await Compare();
        var metric = comparison.SpecificationGroups.SelectMany(x => x.Specifications).Single(x => x.Key == input.Key);
        Assert.Equal(0m, metric.Values[0].ValueNumber);
        Assert.True(metric.Values[1].IsMissing);
        var destination = await Data<AdminSpecificationGroupResponse>(await Client.PostAsJsonAsync(groups,
            new SpecificationGroupInput("Moved group", "moved_group", 1), Ct), HttpStatusCode.Created);
        input = input with { Name = "Changed metric", GroupId = destination.Id, DisplayOrder = 4, IsComparable = false };
        await Data<AdminSpecificationDefinitionResponse>(await Client.PutAsJsonAsync(definitionPath, input, Ct));
        Assert.DoesNotContain((await Compare()).SpecificationGroups.SelectMany(x => x.Specifications), x => x.Key == input.Key);
        var updated = await Data<AdminSpecificationDefinitionResponse>(await Client.GetAsync(definitionPath, Ct));
        Assert.Equal("Changed metric", updated.Name);
        Assert.Equal(destination.Id, updated.GroupId);
        Assert.Equal("number", updated.DataType);
        Assert.Equal("mm", updated.Unit);
        await Data<AdminSpecificationDefinitionResponse>(await Client.PutAsJsonAsync(definitionPath, input with { IsComparable = true }, Ct));
        Assert.Contains((await Compare()).SpecificationGroups, x => x.Key == "moved_group" && x.Specifications.Any(s => s.Key == input.Key));
        await Data<AdminDeviceState>(await Client.DeleteAsync(specPath, Ct));
        await Data<AdminDeletedResponse>(await Client.DeleteAsync(definitionPath, Ct));
        await Data<AdminDeletedResponse>(await Client.DeleteAsync(groupPath, Ct));
        await Data<AdminDeletedResponse>(await Client.DeleteAsync($"{groups}/{destination.Id}", Ct));
        await Error(await Client.GetAsync(definitionPath, Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
    }

    [Theory]
    [InlineData("text")]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("date")]
    public async Task All_typed_values_round_trip_including_zero_and_false(string type)
    {
        var group = await Data<AdminSpecificationGroupResponse>(await Client.PostAsJsonAsync("/api/v1/admin/specification-groups",
            new SpecificationGroupInput("Typed group", $"typed_{type}"), Ct), HttpStatusCode.Created);
        var definition = await Data<AdminSpecificationDefinitionResponse>(await Client.PostAsJsonAsync("/api/v1/admin/specification-definitions",
            new SpecificationDefinitionInput("Typed definition", $"typed_{type}", group.Id, type), Ct), HttpStatusCode.Created);
        Guid phoneId;
        await using (var db = fixture.Catalog.CreateContext())
            phoneId = await db.Devices.Where(x => x.Slug == "nokia-3210").Select(x => x.Id).SingleAsync(Ct);
        var value = type switch
        {
            "text" => new SpecificationInput(ValueText: "Editorial text"), "number" => new SpecificationInput(ValueNumber: 0),
            "boolean" => new SpecificationInput(ValueBoolean: false), _ => new SpecificationInput(ValueDate: new DateOnly(2000, 1, 2))
        };
        await Data<AdminDeviceState>(await Client.PutAsJsonAsync($"/api/v1/admin/devices/{phoneId}/specifications/{definition.Id}", value, Ct));
        var detail = await Data<AdminDeviceDetail>(await Client.GetAsync($"/api/v1/admin/devices/{phoneId}", Ct));
        var saved = detail.Specifications.Single(x => x.DefinitionId == definition.Id);
        Assert.Equal(value.ValueText, saved.ValueText);
        Assert.Equal(value.ValueNumber, saved.ValueNumber);
        Assert.Equal(value.ValueBoolean, saved.ValueBoolean);
        Assert.Equal(value.ValueDate, saved.ValueDate);
    }

    [Theory]
    [InlineData("brands", "{\"name\":\"\",\"slug\":\"valid\"}")]
    [InlineData("categories", "{\"name\":\"Category\",\"slug\":\"valid\",\"displayOrder\":-1}")]
    [InlineData("specification-groups", "{\"name\":\"Group\",\"key\":\"Bad Key\"}")]
    [InlineData("specification-definitions", "{\"name\":\"Definition\",\"key\":\"valid\",\"dataType\":\"1\"}")]
    public async Task Reference_validation_rejects_bad_payloads(string resource, string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        await Error(await Client.PostAsync($"/api/v1/admin/{resource}", content, Ct), HttpStatusCode.BadRequest);
        await Error(await Client.GetAsync($"/api/v1/admin/{resource}/{Guid.NewGuid()}", Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
        await Error(await Client.DeleteAsync($"/api/v1/admin/{resource}/{Guid.NewGuid()}", Ct), HttpStatusCode.NotFound, "ADMIN_RESOURCE_NOT_FOUND");
    }

    private async Task<CompareDevicesResponse> Compare() => await Data<CompareDevicesResponse>(
        await fixture.Catalog.Client.GetAsync("/api/v1/compare?devices=nokia-3310,nokia-3210", Ct));
}

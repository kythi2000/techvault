using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using TechVault.Application.Admin.Devices;
using TechVault.Application.Admin.References;
using TechVault.Application.Brands.GetBrand;
using TechVault.Application.Brands.GetBrands;
using TechVault.Application.Categories.GetCategories;
using TechVault.Application.Comparisons;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Devices.GetDevice;
using TechVault.Application.Devices.GetDeviceSpecifications;
using TechVault.Application.Search;
using TechVault.Application.Timeline;

namespace TechVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<BrowseDevicesHandler>();
        services.AddScoped<GetDeviceHandler>();
        services.AddScoped<GetDeviceSpecificationsHandler>();
        services.AddScoped<GetBrandsHandler>();
        services.AddScoped<GetBrandHandler>();
        services.AddScoped<GetCategoriesHandler>();
        services.AddScoped<SearchDevicesHandler>();
        services.AddScoped<GetTimelineHandler>();
        services.AddScoped<CompareDevicesHandler>();
        services.AddScoped<GetAdminDevicesHandler>();
        services.AddScoped<SaveAdminDeviceHandler>();
        services.AddScoped<DeviceLifecycleHandler>();
        services.AddScoped<EditDeviceSpecificationsHandler>();
        services.AddScoped<ManageBrandsHandler>();
        services.AddScoped<ManageCategoriesHandler>();
        services.AddScoped<ManageSpecificationGroupsHandler>();
        services.AddScoped<ManageSpecificationDefinitionsHandler>();
        services.AddScoped<GetAdminComparisonGroupsHandler>();
        services.AddScoped<IValidator<DeviceInput>, DeviceInputValidator>();
        services.AddScoped<IValidator<SpecificationInput>, SpecificationInputValidator>();
        services.AddScoped<IValidator<BrandInput>, BrandInputValidator>();
        services.AddScoped<IValidator<CategoryInput>, CategoryInputValidator>();
        services.AddScoped<IValidator<SpecificationGroupInput>, SpecificationGroupInputValidator>();
        services.AddScoped<IValidator<SpecificationDefinitionInput>, SpecificationDefinitionInputValidator>();
        return services;
    }
}

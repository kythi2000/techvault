using Microsoft.Extensions.DependencyInjection;
using TechVault.Application.Brands.GetBrand;
using TechVault.Application.Brands.GetBrands;
using TechVault.Application.Categories.GetCategories;
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
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using TechVault.Application.Brands.GetBrand;
using TechVault.Application.Brands.GetBrands;
using TechVault.Application.Categories.GetCategories;
using TechVault.Application.Devices.BrowseDevices;
using TechVault.Application.Devices.GetDevice;
using TechVault.Application.Devices.GetDeviceSpecifications;

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
        return services;
    }
}

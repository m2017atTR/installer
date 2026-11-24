using Installer.Core.Configuration;
using Installer.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Installer.Core;

public static class InstallerServiceCollectionExtensions
{
    public static IServiceCollection AddInstaller(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection("Database"));
        services.AddSingleton<IConfigReader, JsonConfigReader>();
        services.AddSingleton<IInstaller, DatabaseInstaller>();
        services.AddSingleton<InstallerOrchestrator>();
        
        // Configure MainApp URL
        var mainAppUrl = configuration["MainApp:Url"] ?? "http://localhost:5002";
        services.Configure<MainAppOptions>(o => o.Url = mainAppUrl);
        
        return services;
    }
}
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Installer.Core.Configuration;
using Installer.Core.Services;

namespace Installer.Core;

public static class InstallerHost
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddEnvironmentVariables(prefix: "INSTALLER_")
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<AppSettings>(context.Configuration.GetSection("Database"));
                services.AddSingleton<IConfigReader, JsonConfigReader>();
                services.AddSingleton<IInstaller, DatabaseInstaller>();
                services.AddHostedService<InstallerWorker>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });

    public static async Task<int> RunAsync(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("InstallerHost");
            logger.LogCritical(ex, "Application terminated unexpectedly");
            return 1;
        }
    }
}

public sealed class InstallerWorker : BackgroundService
{
    private readonly IInstaller _installer;
    private readonly ILogger<InstallerWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public InstallerWorker(IInstaller installer, ILogger<InstallerWorker> logger, IHostApplicationLifetime lifetime)
    {
        _installer = installer;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Installer starting...");

        var isInstalled = await _installer.IsInstalledAsync(stoppingToken);
        
        if (!isInstalled)
        {
            _logger.LogInformation("Application not installed, running installer...");
            var success = await _installer.RunAsync(stoppingToken);
            
            if (!success)
            {
                _logger.LogError("Installation failed");
                _lifetime.StopApplication();
                return;
            }
        }
        else
        {
            _logger.LogInformation("Application already installed, starting normally...");
        }

        // Application is installed - continue with normal startup
        // Replace this with your actual application startup logic
        _logger.LogInformation("Application ready");
        
        // Keep running until shutdown
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
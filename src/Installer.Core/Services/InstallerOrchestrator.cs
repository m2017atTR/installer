using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Installer.Core.Configuration;
using Installer.Core.Services;

namespace Installer.Core.Services;

public sealed class InstallerOrchestrator
{
    private readonly IInstaller _installer;
    private readonly IConfigReader _configReader;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InstallerOrchestrator> _logger;

    public InstallerOrchestrator(
        IInstaller installer,
        IConfigReader configReader,
        IConfiguration configuration,
        ILogger<InstallerOrchestrator> logger)
    {
        _installer = installer;
        _configReader = configReader;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OrchestratorResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("InstallerOrchestrator starting...");

        // Check if already installed via config
        var settings = _configReader.Read();
        var hasConnectionString = !string.IsNullOrWhiteSpace(settings.Database.ConnectionString);
        var isInstalled = settings.IsInstalled;

        _logger.LogInformation("Installation check: HasConnectionString={HasConnectionString}, IsInstalled={IsInstalled}", 
            hasConnectionString, isInstalled);

        // If already installed and configured, proceed to main app
        if (isInstalled && hasConnectionString)
        {
            _logger.LogInformation("Application already installed, proceeding to main app");
            
            // Verify database connection still works
            var connTest = await _installer.TestConnectionAsync(settings.Database.ConnectionString, settings.Database.Provider, cancellationToken);
            if (!connTest)
            {
                _logger.LogWarning("Database connection failed, may need reconfiguration");
                return OrchestratorResult.NeedsReconfiguration("Database connection failed");
            }

            return OrchestratorResult.ProceedToMainApp();
        }

        // Check for connection string from environment variables
        var envConnStr = _configuration["POSTGRES_CONNECTION_STRING"] 
            ?? BuildConnectionStringFromEnv();
        
        if (!string.IsNullOrWhiteSpace(envConnStr))
        {
            _logger.LogInformation("Found connection string from environment, attempting auto-install");
            
            var provider = _configuration["Database:Provider"] ?? "PostgreSQL";
            var testConn = await _installer.TestConnectionAsync(envConnStr, provider, cancellationToken);
            
            if (testConn)
            {
                _logger.LogInformation("Environment connection string valid, running installation");
                await _installer.RunMigrationsAsync(envConnStr, provider, cancellationToken);
                
                // Save configuration
                var dbSettings = new DatabaseSettings
                {
                    ConnectionString = envConnStr,
                    Provider = provider
                };
                settings.Database = dbSettings;
                settings.IsInstalled = true;
                _configReader.Write(settings);
                
                _logger.LogInformation("Auto-installation completed");
                return OrchestratorResult.ProceedToMainApp();
            }
            else
            {
                _logger.LogWarning("Environment connection string invalid, falling back to installer UI");
            }
        }

        // Check if auto-migrate is enabled
        var autoMigrate = _configuration.GetValue<bool>("INSTALLER_AUTO_MIGRATE", true);
        if (autoMigrate && !string.IsNullOrWhiteSpace(envConnStr))
        {
            // Already handled above
        }

        _logger.LogInformation("Installation required, showing installer UI");
        return OrchestratorResult.ShowInstallerUI();
    }

    private string? BuildConnectionStringFromEnv()
    {
        var host = _configuration["POSTGRES_HOST"] ?? _configuration["POSTGRES_HOST"];
        var port = _configuration["POSTGRES_PORT"] ?? "5432";
        var db = _configuration["POSTGRES_DB"] ?? "installerdb";
        var user = _configuration["POSTGRES_USER"] ?? "postgres";
        var password = _configuration["POSTGRES_PASSWORD"] ?? "postgres";

        if (string.IsNullOrWhiteSpace(host))
            return null;

        return $"Host={host};Port={port};Database={db};Username={user};Password={password};";
    }
}

public sealed class OrchestratorResult
{
    public OrchestratorAction Action { get; private set; }
    public string? Message { get; private set; }

    private OrchestratorResult(OrchestratorAction action, string? message = null)
    {
        Action = action;
        Message = message;
    }

    public static OrchestratorResult ProceedToMainApp() => new(OrchestratorAction.ProceedToMainApp);
    public static OrchestratorResult ShowInstallerUI() => new(OrchestratorAction.ShowInstallerUI);
    public static OrchestratorResult NeedsReconfiguration(string message) => new(OrchestratorAction.NeedsReconfiguration, message);
}

public enum OrchestratorAction
{
    ProceedToMainApp,
    ShowInstallerUI,
    NeedsReconfiguration
}
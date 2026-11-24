using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Installer.Core.Configuration;
using Npgsql;

namespace Installer.Core.Services;

public interface IInstaller
{
    Task<bool> RunAsync(CancellationToken cancellationToken = default);
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string connectionString, string provider, CancellationToken cancellationToken = default);
    Task RunMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task RunMigrationsAsync(string connectionString, string provider, CancellationToken cancellationToken = default);
}

public sealed class DatabaseInstaller : IInstaller
{
    private readonly IConfigReader _configReader;
    private readonly ILogger<DatabaseInstaller> _logger;

    public DatabaseInstaller(IConfigReader configReader, ILogger<DatabaseInstaller> logger)
    {
        _configReader = configReader;
        _logger = logger;
    }

    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configReader.Read();
        var hasConnection = !string.IsNullOrWhiteSpace(settings.Database.ConnectionString);
        
        _logger.LogInformation("Installation check: HasConnection={HasConnection}, IsInstalled={IsInstalled}", 
            hasConnection, settings.IsInstalled);
        
        return hasConnection && settings.IsInstalled;
    }

    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting installation process...");
        
        var settings = _configReader.Read();
        
        if (await IsInstalledAsync(cancellationToken))
        {
            _logger.LogInformation("Application already installed, skipping");
            return true;
        }

        var connectionString = await GetConnectionStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("No connection string provided, installation aborted");
            return false;
        }

        if (!await TestConnectionAsync(connectionString, settings.Database.Provider, cancellationToken))
        {
            _logger.LogError("Connection test failed");
            return false;
        }

        await RunMigrationsAsync(connectionString, settings.Database.Provider, cancellationToken);

        settings.Database.ConnectionString = connectionString;
        settings.IsInstalled = true;
        _configReader.Write(settings);

        _logger.LogInformation("Installation completed successfully");
        return true;
    }

    public async Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var settings = _configReader.Read();
        return await TestConnectionAsync(connectionString, settings.Database.Provider, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(string connectionString, string provider, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Testing database connection (Provider: {Provider})", provider);

        try
        {
            if (string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync(cancellationToken);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for provider {Provider}", provider);
        }

        return false;
    }

    public async Task RunMigrationsAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var settings = _configReader.Read();
        await RunMigrationsAsync(connectionString, settings.Database.Provider, cancellationToken);
    }

    public async Task RunMigrationsAsync(string connectionString, string provider, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running database migrations for {Provider}...", provider);
        
        if (string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            // Create a simple test table to verify schema access
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS installer_version (version TEXT, installed_at TIMESTAMP DEFAULT NOW())";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        
        await Task.Delay(500, cancellationToken);
        _logger.LogInformation("Migrations completed");
    }

    private Task<string> GetConnectionStringAsync(CancellationToken cancellationToken)
    {
        var connStr = Environment.GetEnvironmentVariable("INSTALLER_CONNECTION_STRING");
        return Task.FromResult(connStr ?? string.Empty);
    }
}
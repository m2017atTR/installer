using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Installer.Core.Configuration;

namespace Installer.Core.Services;

public interface IConfigReader
{
    AppSettings Read();
    void Write(AppSettings settings);
    bool ConfigExists();
}

public sealed class JsonConfigReader : IConfigReader
{
    private readonly string _configPath;
    private readonly ILogger<JsonConfigReader> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonConfigReader(ILogger<JsonConfigReader> logger, string? configPath = null)
    {
        _logger = logger;
        _configPath = configPath ?? GetDefaultConfigPath();
    }

    public AppSettings Read()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("Config file not found at {Path}, returning defaults", _configPath);
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            _logger.LogInformation("Configuration loaded from {Path}", _configPath);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read config from {Path}", _configPath);
            return new AppSettings();
        }
    }

    public void Write(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_configPath, json);
        _logger.LogInformation("Configuration written to {Path}", _configPath);
    }

    public bool ConfigExists() => File.Exists(_configPath);

    private static string GetDefaultConfigPath()
    {
        var basePath = AppContext.BaseDirectory;
        return Path.Combine(basePath, "appsettings.json");
    }
}
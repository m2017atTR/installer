namespace Installer.Core.Configuration;

public sealed class AppSettings
{
    public DatabaseSettings Database { get; set; } = new();
    public bool IsInstalled { get; set; }
}

public sealed class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Provider { get; set; } = "PostgreSQL";
}

public sealed class MainAppOptions
{
    public string Url { get; set; } = "http://localhost:5002";
}
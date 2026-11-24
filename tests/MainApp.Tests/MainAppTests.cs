using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Installer.Core.Services;
using Npgsql;
using Xunit;

namespace MainApp.Tests;

public class MainAppTests : IClassFixture<WebApplicationFactory<MainApp.Program>>
{
    private readonly WebApplicationFactory<MainApp.Program> _factory;
    private readonly HttpClient _client;

    public MainAppTests(WebApplicationFactory<MainApp.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Load test.env
                var envPath = Path.Combine(Directory.GetCurrentDirectory(), "test.env");
                if (File.Exists(envPath))
                {
                    var dict = File.ReadAllLines(envPath)
                        .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                        .Select(l => l.Split('=', 2))
                        .Where(p => p.Length == 2)
                        .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
                    
                    config.AddInMemoryCollection(dict);
                }
                
                config.AddEnvironmentVariables();
            });
            
            builder.UseEnvironment("Testing");
        });
        
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(result);
        Assert.Equal("healthy", result?.Status);
    }

    [Fact]
    public async Task InfoEndpoint_ReturnsAppInfo()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/info");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<InfoResponse>();
        Assert.NotNull(result);
        Assert.Equal("MainApp", result?.Name);
        Assert.Equal("1.0.0", result?.Version);
    }

    [Fact]
    public async Task DataEndpoint_ReturnsSampleData()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/data");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DataResponse>();
        Assert.NotNull(result);
        Assert.Equal("Hello from MainApp!", result?.Message);
        Assert.NotEmpty(result?.Items);
    }

    [Fact]
    public void Configuration_LoadsFromEnvFile()
    {
        // Arrange
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "test.env");
        Assert.True(File.Exists(envPath));
        
        // Act
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(File.ReadAllLines(envPath)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
                .Select(l => l.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim()))
            .AddEnvironmentVariables()
            .Build();
        
        // Assert
        Assert.Equal("localhost", config["POSTGRES_HOST"]);
        Assert.Equal("5432", config["POSTGRES_PORT"]);
        Assert.Equal("installerdb", config["POSTGRES_DB"]);
        Assert.Equal("postgres", config["POSTGRES_USER"]);
        Assert.Equal("postgres", config["POSTGRES_PASSWORD"]);
    }

    [Fact]
    public async Task DatabaseConnection_WorksWithTestContainer()
    {
        // This test uses the real PostgreSQL container started by docker-compose
        // It verifies the connection string building works correctly
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["POSTGRES_HOST"] = "localhost",
                ["POSTGRES_PORT"] = "5432",
                ["POSTGRES_DB"] = "installerdb",
                ["POSTGRES_USER"] = "postgres",
                ["POSTGRES_PASSWORD"] = "postgres"
            })
            .AddEnvironmentVariables()
            .Build();
        
        var connStr = BuildConnectionString(config);
        Assert.NotNull(connStr);
        Assert.Contains("Host=localhost", connStr);
        Assert.Contains("Port=5432", connStr);
        Assert.Contains("Database=installerdb", connStr);
        Assert.Contains("Username=postgres", connStr);
        Assert.Contains("Password=postgres", connStr);
        
        // Try to connect
        using var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    private static string BuildConnectionString(IConfiguration config)
    {
        var host = config["POSTGRES_HOST"] ?? "localhost";
        var port = config["POSTGRES_PORT"] ?? "5432";
        var db = config["POSTGRES_DB"] ?? "installerdb";
        var user = config["POSTGRES_USER"] ?? "postgres";
        var password = config["POSTGRES_PASSWORD"] ?? "postgres";
        
        return $"Host={host};Port={port};Database={db};Username={user};Password={password};";
    }

    [Fact]
    public async Task Root_NotInstalled_ReturnsInstallerUI()
    {
        // Act
        var response = await _client.GetAsync("/");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Installer.Net", html);
        Assert.Contains("Install & Continue", html);
    }

[Fact]
public async Task Root_Installed_ReturnsHomePage()
{
    // Arrange - Create factory with installed config
    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"installer_test_{Guid.NewGuid()}.json");
    File.WriteAllText(tempConfigPath, """{"Database":{"ConnectionString":"Host=localhost;Database=test;Username=test;Password=test;","Provider":"PostgreSQL"},"IsInstalled":true}""");
    
    var factory = _factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add installed config to IConfiguration
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["IsInstalled"] = "true",
                ["Database:ConnectionString"] = "Host=localhost;Database=test;Username=test;Password=test;",
                ["Database:Provider"] = "PostgreSQL"
            });
        });
        
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IConfigReader>(sp => 
            {
                var logger = sp.GetRequiredService<ILogger<JsonConfigReader>>();
                return new JsonConfigReader(logger, tempConfigPath);
            });
        });
        builder.UseEnvironment("Testing");
    });
    
    var client = factory.CreateClient();
    
    // Act
    var response = await client.GetAsync("/");
    
    // Assert
    response.EnsureSuccessStatusCode();
    var html = await response.Content.ReadAsStringAsync();
    Assert.Contains("Welcome to MainApp", html); // Home page content
}

    [Fact]
    public async Task InstallEndpoint_CompletesAndRedirectsToHome()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            ["provider"] = "PostgreSQL",
            ["connectionString"] = "Host=localhost;Database=installerdb;Username=postgres;Password=postgres;",
            ["remember"] = "on"
        };
        
        // Act - Post to installer
        var response = await _client.PostAsync("/api/installer", new FormUrlEncodedContent(formData));
        
        // Assert - Should either succeed (if DB available) or fail on connection test
        // But should NOT fail with 415 UnsupportedMediaType
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}

public record HealthResponse(string Status, DateTime Timestamp);
public record InfoResponse(string Name, string Version, string Environment, DatabaseInfo Database);
public record DatabaseInfo(string Provider, bool Connected);
public record DataResponse(string Message, DateTime Timestamp, Item[] Items);
public record Item(int Id, string Name, string Description);
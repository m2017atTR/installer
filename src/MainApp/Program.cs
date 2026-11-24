using Installer.Core;
using Installer.Core.Configuration;
using Installer.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Scalar.AspNetCore;

namespace MainApp;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Load .env file FIRST - before any other configuration
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
        builder.Logging.AddConsole();
        var loggerFactory = builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MainApp.Startup");

        logger.LogInformation("Current directory: {Dir}", Directory.GetCurrentDirectory());
        logger.LogInformation("Env path: {Path}", envPath);
        logger.LogInformation("Env exists: {Exists}", File.Exists(envPath));

        if (File.Exists(envPath))
        {
            var lines = File.ReadAllLines(envPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;
                    
                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    Environment.SetEnvironmentVariable(key, value);
                    logger.LogInformation("Set env: {Key}={Value}", key, value);
                }
            }
        }

        // Now load configuration - environment variables will include .env values
        builder.Configuration.AddEnvironmentVariables();
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // Log loaded configuration
        foreach (var kvp in builder.Configuration.AsEnumerable())
        {
            if (kvp.Key.StartsWith("POSTGRES") || kvp.Key.StartsWith("Database"))
            {
                logger.LogInformation("Config: {Key} = {Value}", kvp.Key, kvp.Value);
            }
        }

        // Add Installer services
        builder.Services.AddInstaller(builder.Configuration);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference("/scalar", opt => opt.WithTitle("MainApp API"));
        }

        app.UseHttpsRedirection();

        // Health check
        app.MapGet("/health", () => Results.Ok(new { 
            status = "healthy", 
            service = "MainApp",
            timestamp = DateTime.UtcNow 
        }));

        // Home page (when installed)
        app.MapGet("/", async (HttpContext context, IConfiguration config) =>
        {
            var isInstalled = config.GetValue<bool>("IsInstalled", false);
            var hasConnectionString = !string.IsNullOrWhiteSpace(config["Database:ConnectionString"]);
            
            if (!isInstalled || !hasConnectionString)
            {
                // Not installed - serve installer UI
                await ServeInstallerUI(context, config, app.Environment.WebRootPath);
                return;
            }
            
            // Installed - serve home page
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(GetHomePageHtml());
        });

        // Installer endpoints
        app.MapPost("/api/installer", async (HttpContext context, IInstaller installer, IConfiguration config, CancellationToken ct) =>
        {
            // Validate content type
            var contentType = context.Request.ContentType?.ToLowerInvariant() ?? "";
            var isFormUrlEncoded = contentType.StartsWith("application/x-www-form-urlencoded");
            var isMultipartFormData = contentType.StartsWith("multipart/form-data");
            
            if (!isFormUrlEncoded && !isMultipartFormData)
            {
                return Results.Text("Only application/x-www-form-urlencoded and multipart/form-data are supported", "application/json", System.Text.Encoding.UTF8, StatusCodes.Status415UnsupportedMediaType);
            }

            var form = await context.Request.ReadFormAsync();
            var provider = form["provider"].FirstOrDefault() ?? "PostgreSQL";
            var connectionString = form["connectionString"].FirstOrDefault() ?? string.Empty;
            var remember = form["remember"] == "on";

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Results.BadRequest(new { error = "Connection string is required" });
            }

            var testConn = await installer.TestConnectionAsync(connectionString, provider, ct);
            if (!testConn)
            {
                return Results.BadRequest(new { error = "Connection test failed. Please check your connection string." });
            }

            await installer.RunMigrationsAsync(connectionString, provider, ct);

            var databaseSettings = new DatabaseSettings
            {
                ConnectionString = connectionString,
                Provider = provider
            };
            config["Database:ConnectionString"] = connectionString;
            config["Database:Provider"] = provider;
            config["IsInstalled"] = "true";

            var appSettings = new AppSettings
            {
                Database = databaseSettings,
                IsInstalled = true
            };
            var configService = context.RequestServices.GetRequiredService<IConfigReader>();
            configService.Write(appSettings);

            return Results.Ok(new { 
                message = "Installation completed successfully", 
                redirect = "/" 
            });
        });

        app.MapGet("/api/installer/status", async (IInstaller installer, IConfiguration config) =>
        {
            var isInstalled = await installer.IsInstalledAsync();
            var connectionString = config["Database:ConnectionString"] ?? string.Empty;
            return new { isInstalled, connectionString };
        });

        // MainApp API endpoints
        var api = app.MapGroup("/api/v1").WithTags("MainApp");

        app.MapGet("/api/v1/info", (IConfiguration config) => 
        {
            var settings = config.GetSection("Database").Get<DatabaseSettings>() ?? new DatabaseSettings();
            return Results.Ok(new 
            {
                name = "MainApp",
                version = "1.0.0",
                environment = app.Environment.EnvironmentName,
                database = new
                {
                    provider = settings.Provider,
                    connected = !string.IsNullOrWhiteSpace(settings.ConnectionString)
                }
            });
        });

        app.MapGet("/api/v1/data", () => 
        {
            return Results.Ok(new 
            {
                message = "Hello from MainApp!",
                timestamp = DateTime.UtcNow,
                items = new[]
                {
                    new { id = 1, name = "Item 1", description = "First item" },
                    new { id = 2, name = "Item 2", description = "Second item" },
                    new { id = 3, name = "Item 3", description = "Third item" }
                }
            });
        });

        app.MapGet("/api/v1/db-test", async (IConfiguration config) => 
        {
            logger.LogInformation("POSTGRES_HOST from config: {Host}", config["POSTGRES_HOST"]);
            logger.LogInformation("POSTGRES_PORT from config: {Port}", config["POSTGRES_PORT"]);
            logger.LogInformation("POSTGRES_DB from config: {Db}", config["POSTGRES_DB"]);
            logger.LogInformation("POSTGRES_USER from config: {User}", config["POSTGRES_USER"]);
            logger.LogInformation("POSTGRES_PASSWORD from config: {Pass}", config["POSTGRES_PASSWORD"]);
            
            var connStr = config["Database:ConnectionString"] 
                ?? config.GetConnectionString("DefaultConnection")
                ?? BuildConnectionString(config);
            
            logger.LogInformation("Final connection string: {ConnStr}", connStr ?? "NULL/EMPTY");
            
            if (string.IsNullOrWhiteSpace(connStr))
            {
                return Results.BadRequest(new { error = "No connection string configured" });
            }

            try
            {
                using var connection = new Npgsql.NpgsqlConnection(connStr);
                await connection.OpenAsync();
                
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT version(), current_database(), current_user, now()";
                
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Results.Ok(new 
                    {
                        success = true,
                        version = reader.GetString(0),
                        database = reader.GetString(1),
                        user = reader.GetString(2),
                        serverTime = reader.GetDateTime(3)
                    });
                }
                
                return Results.Ok(new { success = true, message = "Connected but no data returned" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database connection failed");
                return Results.Problem($"Database connection failed: {ex.Message}");
            }
        });

        static string BuildConnectionString(IConfiguration config)
        {
            var host = config["POSTGRES_HOST"] ?? "localhost";
            var port = config["POSTGRES_PORT"] ?? "5432";
            var db = config["POSTGRES_DB"] ?? "installerdb";
            var user = config["POSTGRES_USER"] ?? "postgres";
            var password = config["POSTGRES_PASSWORD"] ?? "postgres";
            
            var result = $"Host={host};Port={port};Database={db};Username={user};Password={password};";
            return result;
        }

        static async Task ServeInstallerUI(HttpContext context, IConfiguration config, string webRootPath)
        {
            // Build default connection string from environment/config
            var defaultConnStr = BuildDefaultConnectionString(config);
            
            // Serve installer UI with default connection string injected
            context.Response.ContentType = "text/html";
            var html = await File.ReadAllTextAsync(Path.Combine(webRootPath, "index.html"));
            
            // Inject default connection string as data attribute
            html = html.Replace("<form id=\"installerForm\">", 
                $"<form id=\"installerForm\" data-default-connection-string=\"{System.Net.WebUtility.HtmlEncode(defaultConnStr)}\">");
            
            await context.Response.WriteAsync(html);
        }

        static string BuildDefaultConnectionString(IConfiguration config)
        {
            var host = config["POSTGRES_HOST"] ?? "localhost";
            var port = config["POSTGRES_PORT"] ?? "5432";
            var db = config["POSTGRES_DB"] ?? "installerdb";
            var user = config["POSTGRES_USER"] ?? "postgres";
            var password = config["POSTGRES_PASSWORD"] ?? "postgres";
            
            return $"Host={host};Port={port};Database={db};Username={user};Password={password};";
        }

        static string GetHomePageHtml()
        {
            return """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>MainApp</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <style>
        body { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #f8f9fa; }
        .card { width: 100%; max-width: 480px; padding: 2rem; }
    </style>
</head>
<body>
    <div class="card text-center">
        <h3 class="mb-3">Welcome to MainApp</h3>
        <p class="text-muted mb-4">Application is installed and ready to use.</p>
        <a href="/api/v1/info" class="btn btn-primary me-2">API Info</a>
        <a href="/api/v1/data" class="btn btn-outline-primary me-2">Sample Data</a>
        <a href="/api/v1/db-test" class="btn btn-outline-secondary">DB Test</a>
        <hr class="my-3">
        <a href="/scalar" class="btn btn-link">API Documentation (Scalar)</a>
    </div>
</body>
</html>
""";
        }

        app.Run();
    }
}
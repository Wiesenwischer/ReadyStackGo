using System.Text;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Api.BackgroundServices;
using ReadyStackGo.Api.Hubs;
using ReadyStackGo.Api.Services;
using ReadyStackGo.Application;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure;

namespace ReadyStackGo.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddFastEndpoints();

        // Add JWT Authentication
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Disable claim mapping to preserve original JWT claim names
                // This allows us to use "roles" claim without ASP.NET Core transforming it
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
                };

                // Configure JWT authentication for SignalR
                // SignalR sends the token via query string for WebSocket connections
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        // If the request is for a hub, read token from query string
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        // Add SignalR for real-time health updates
        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
        builder.Services.AddScoped<IHealthNotificationService, HealthNotificationService>();

        // Health Collector Background Service (v0.11)
        builder.Services.Configure<HealthCollectorOptions>(
            builder.Configuration.GetSection(HealthCollectorOptions.SectionName));
        var healthCollectorOptions = builder.Configuration
            .GetSection(HealthCollectorOptions.SectionName)
            .Get<HealthCollectorOptions>() ?? new HealthCollectorOptions();
        if (healthCollectorOptions.Enabled)
        {
            builder.Services.AddHostedService<HealthCollectorBackgroundService>();
        }

        // Maintenance Observer Background Service (v0.11)
        builder.Services.AddHostedService<MaintenanceObserverBackgroundService>();

        // Add CORS for development
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("DevCorsPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5175")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials(); // Required for SignalR
            });
        });

        var app = builder.Build();

        // Initialize SQLite database
        app.Services.EnsureDatabaseCreated();

        // Bootstrap: Generate TLS certificate if not exists
        await BootstrapTlsCertificateAsync(app);

        // Initialize wizard timeout (timer starts at container startup, not browser access)
        await InitializeWizardTimeoutAsync(app);

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("DevCorsPolicy");
        }

        app.UseHttpsRedirection();

        // Log web root path for debugging
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("WebRootPath: {WebRoot}", app.Environment.WebRootPath);
        logger.LogInformation("ContentRootPath: {ContentRoot}", app.Environment.ContentRootPath);

        // Serve static files from wwwroot with explicit configuration
        // This ensures files are served with correct MIME types before hitting the SPA fallback
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ServeUnknownFileTypes = false,
            OnPrepareResponse = ctx =>
            {
                // Log static file requests for debugging
                var path = ctx.File.PhysicalPath;
                logger.LogDebug("Serving static file: {Path}", path);
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpoints();

        // Map SignalR hubs
        app.MapHub<HealthHub>("/hubs/health");

        // SPA fallback: serve index.html for non-API, non-file routes
        // This must come after static files middleware so that actual files are served first
        app.MapFallback(async context =>
        {
            // Only serve SPA for non-API routes
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync(
                    Path.Combine(app.Environment.WebRootPath, "index.html"));
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        });

        await app.RunAsync();
    }

    /// <summary>
    /// Initialize wizard timeout window at application startup.
    /// The 5-minute timer starts when the container starts, not when the user first accesses the UI.
    /// </summary>
    private static async Task InitializeWizardTimeoutAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var timeoutService = scope.ServiceProvider.GetRequiredService<ReadyStackGo.Application.Services.IWizardTimeoutService>();

        await timeoutService.InitializeOnStartupAsync();
    }

    /// <summary>
    /// Bootstrap TLS certificate generation on first startup
    /// </summary>
    private static async Task BootstrapTlsCertificateAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var tlsService = scope.ServiceProvider.GetRequiredService<ReadyStackGo.Infrastructure.Tls.ITlsService>();
        var configStore = scope.ServiceProvider.GetRequiredService<ReadyStackGo.Infrastructure.Configuration.IConfigStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // Check if TLS is already configured
            var tlsConfig = await configStore.GetTlsConfigAsync();

            if (tlsConfig.CertificatePath == null || !File.Exists(tlsConfig.CertificatePath))
            {
                logger.LogInformation("TLS certificate not found. Generating self-signed certificate...");

                // Get hostname from configuration or use localhost
                var hostname = app.Configuration.GetValue<string>("Hostname") ?? "localhost";

                // Generate self-signed certificate
                var certPassword = await tlsService.GenerateSelfSignedCertificateAsync(hostname);

                logger.LogInformation("Self-signed TLS certificate generated successfully for hostname: {Hostname}", hostname);
                logger.LogInformation("Certificate path: {CertPath}", tlsConfig.CertificatePath);
                logger.LogWarning("IMPORTANT: Store the certificate password securely. It is saved in rsgo.tls.json");
            }
            else
            {
                logger.LogInformation("TLS certificate already exists at: {CertPath}", tlsConfig.CertificatePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to bootstrap TLS certificate. The application will continue but HTTPS may not work properly.");
        }
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Api.BackgroundServices;
using ReadyStackGo.Api.Hubs;
using ReadyStackGo.Api.Services;
using ReadyStackGo.Application;
using ReadyStackGo.Application.Services;
using Microsoft.AspNetCore.Authentication;
using ReadyStackGo.Infrastructure;
using ReadyStackGo.Infrastructure.DataAccess;
using ReadyStackGo.Infrastructure.Security.Authentication;

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

        // Add Authentication with multi-scheme support (JWT + API Key)
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        builder.Services.AddAuthentication("MultiScheme")
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, null)
            .AddPolicyScheme("MultiScheme", "API Key or JWT", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName))
                        return ApiKeyAuthenticationHandler.SchemeName;
                    return JwtBearerDefaults.AuthenticationScheme;
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
        builder.Services.AddScoped<IDeploymentNotificationService, DeploymentNotificationService>();
        builder.Services.AddSingleton<IUpdateNotificationService, UpdateNotificationService>();

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

        // Deployment Recovery Service - handles stuck deployments on startup (v0.15)
        builder.Services.AddHostedService<DeploymentRecoveryService>();

        // Product Deployment Health Sync Service - eventual consistency between aggregates
        builder.Services.AddHostedService<ProductDeploymentHealthSyncService>();

        // Certificate Renewal Background Service (v0.16)
        builder.Services.Configure<CertificateRenewalOptions>(
            builder.Configuration.GetSection(CertificateRenewalOptions.SectionName));
        builder.Services.AddHostedService<CertificateRenewalBackgroundService>();

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

        // Configure reverse proxy / forwarded headers if enabled
        var reverseProxyConfig = await ConfigureReverseProxyAsync(app);

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("DevCorsPolicy");
        }

        // Only use HTTPS redirect if not in a mode where proxy handles SSL
        // - Skip for SSL Termination (proxy handles all SSL)
        // - Skip for Re-Encryption (proxy handles client SSL, backend handles its own)
        // - Keep for SSL Passthrough (backend handles TLS directly)
        // - Keep when not behind a proxy
        if (!reverseProxyConfig.SkipHttpsRedirect)
        {
            app.UseHttpsRedirection();
        }

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
        app.MapHub<DeploymentHub>("/hubs/deployment");
        app.MapHub<UpdateHub>("/hubs/update");

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

    /// <summary>
    /// Configure reverse proxy / forwarded headers middleware if enabled in TLS config
    /// </summary>
    /// <returns>Configuration result indicating whether HTTPS redirect should be skipped</returns>
    private static async Task<ReverseProxyConfigResult> ConfigureReverseProxyAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<ReadyStackGo.Infrastructure.Configuration.IConfigStore>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var tlsConfig = await configStore.GetTlsConfigAsync();
            var proxyConfig = tlsConfig.ReverseProxy;

            if (proxyConfig == null || !proxyConfig.Enabled)
            {
                logger.LogDebug("Reverse proxy mode is disabled");
                return new ReverseProxyConfigResult { Enabled = false, SkipHttpsRedirect = false };
            }

            logger.LogInformation("Reverse proxy mode is enabled. SSL Mode: {SslMode}", proxyConfig.SslMode);

            // For SSL Passthrough, the proxy forwards encrypted traffic directly to backend
            // We don't process forwarded headers (traffic is encrypted) but we still need HTTPS
            if (proxyConfig.SslMode == Infrastructure.Configuration.ReverseProxySslMode.Passthrough)
            {
                logger.LogInformation("SSL Passthrough mode: Backend handles TLS directly. No forwarded headers processing.");

                // Configure path base if set (still useful for routing)
                if (!string.IsNullOrEmpty(proxyConfig.PathBase))
                {
                    app.UsePathBase(proxyConfig.PathBase);
                    logger.LogInformation("Using path base: {PathBase}", proxyConfig.PathBase);
                }

                return new ReverseProxyConfigResult { Enabled = true, SkipHttpsRedirect = false };
            }

            // For Termination and ReEncryption modes, process forwarded headers
            // - Termination: Proxy terminates SSL, sends HTTP to backend
            // - ReEncryption: Proxy terminates SSL, then re-encrypts to backend

            // Build forwarded headers options
            var forwardedHeadersOptions = new ForwardedHeadersOptions();

            // Configure which headers to trust
            if (proxyConfig.TrustForwardedFor)
                forwardedHeadersOptions.ForwardedHeaders |= ForwardedHeaders.XForwardedFor;
            if (proxyConfig.TrustForwardedProto)
                forwardedHeadersOptions.ForwardedHeaders |= ForwardedHeaders.XForwardedProto;
            if (proxyConfig.TrustForwardedHost)
                forwardedHeadersOptions.ForwardedHeaders |= ForwardedHeaders.XForwardedHost;

            // Configure forward limit
            if (proxyConfig.ForwardLimit.HasValue)
            {
                forwardedHeadersOptions.ForwardLimit = proxyConfig.ForwardLimit.Value;
            }

            // Configure known proxies
            if (proxyConfig.KnownProxies.Count > 0)
            {
                foreach (var proxy in proxyConfig.KnownProxies)
                {
                    if (IPAddress.TryParse(proxy, out var ipAddress))
                    {
                        forwardedHeadersOptions.KnownProxies.Add(ipAddress);
                        logger.LogDebug("Added known proxy: {Proxy}", proxy);
                    }
                    else
                    {
                        // Could be a CIDR notation - add to known networks
                        logger.LogWarning("Proxy address '{Proxy}' is not a valid IP address. CIDR notation not yet supported.", proxy);
                    }
                }
            }
            else
            {
                // Clear known proxies and networks to trust all proxies
                // This is less secure but simpler for Docker networks
                forwardedHeadersOptions.KnownProxies.Clear();
                forwardedHeadersOptions.KnownNetworks.Clear();
                logger.LogWarning("No known proxies configured. Trusting all forwarded headers. This is less secure.");
            }

            // Apply forwarded headers middleware
            app.UseForwardedHeaders(forwardedHeadersOptions);

            // Configure path base if set
            if (!string.IsNullOrEmpty(proxyConfig.PathBase))
            {
                app.UsePathBase(proxyConfig.PathBase);
                logger.LogInformation("Using path base: {PathBase}", proxyConfig.PathBase);
            }

            logger.LogInformation("Reverse proxy configuration applied. Forwarded headers: {Headers}",
                forwardedHeadersOptions.ForwardedHeaders);

            // Skip HTTPS redirect for both Termination and ReEncryption modes
            // - Termination: Proxy already handles SSL, backend receives HTTP
            // - ReEncryption: Proxy handles client SSL, creates new HTTPS connection (no redirect needed)
            return new ReverseProxyConfigResult { Enabled = true, SkipHttpsRedirect = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure reverse proxy. Continuing without forwarded headers support.");
            return new ReverseProxyConfigResult { Enabled = false, SkipHttpsRedirect = false };
        }
    }

    private record ReverseProxyConfigResult
    {
        public bool Enabled { get; init; }
        public bool SkipHttpsRedirect { get; init; }
    }
}

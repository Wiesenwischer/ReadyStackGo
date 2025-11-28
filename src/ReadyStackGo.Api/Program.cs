using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Infrastructure;

namespace ReadyStackGo.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddFastEndpoints();

        // Add JWT Authentication
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
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
            });

        builder.Services.AddAuthorization();

        // Add CORS for development
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("DevCorsPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5175")
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Bootstrap: Generate TLS certificate if not exists
        await BootstrapTlsCertificateAsync(app);

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

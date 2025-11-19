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

        // Serve static files from wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseFastEndpoints();

        // Fallback to index.html for SPA routing
        app.MapFallbackToFile("index.html");

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

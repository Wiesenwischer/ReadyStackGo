using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Infrastructure;

namespace ReadyStackGo.Api;

public class Program
{
    public static void Main(string[] args)
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

        app.Run();
    }
}

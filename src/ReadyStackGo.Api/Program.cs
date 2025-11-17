using FastEndpoints;
using ReadyStackGo.Infrastructure;

namespace ReadyStackGo.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddInfrastructure();
        builder.Services.AddFastEndpoints();

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
        app.UseFastEndpoints();

        app.Run();
    }
}

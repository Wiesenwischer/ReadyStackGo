using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Infrastructure.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        // JWT Authentication
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IRbacService, RbacService>();

        // Password hashing
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}

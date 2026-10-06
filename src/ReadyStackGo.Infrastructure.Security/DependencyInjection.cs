using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Infrastructure.Security.Authentication;
using ReadyStackGo.Infrastructure.Security.IdentityProviders;

namespace ReadyStackGo.Infrastructure.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        // JWT Authentication
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IEmailVerificationTokenService, EmailVerificationTokenService>();
        services.AddSingleton<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddSingleton<ITokenRevocationService, TokenRevocationService>();
        // OIDC client, connection checks and client registration kinds. All calls to identity
        // providers go through the named client "Oidc" (10 s per call).
        services.AddHttpClient(Application.Services.Oidc.IOidcService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<Application.Services.Oidc.IOidcService, OidcService>();
        services.AddSingleton<Application.Services.Oidc.IOidcConnectionChecker, OidcConnectionChecker>();
        services.AddSingleton<IClientRegistrationMethod, ManualRegistrationMethod>();
        services.AddSingleton<IClientRegistrationMethod, PairingRegistrationMethod>();
        services.AddSingleton<IRbacService, RbacService>();

        // Password hashing
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        return services;
    }
}

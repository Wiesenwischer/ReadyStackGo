using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Roles;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Authentication handler that validates API keys via X-Api-Key header.
/// Looks up the key hash in the database and builds a ClaimsPrincipal
/// compatible with the existing RBAC system.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
    private const string KeyPrefix = "rsgo_";

    private readonly IServiceProvider _serviceProvider;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceProvider serviceProvider)
        : base(options, logger, encoder)
    {
        _serviceProvider = serviceProvider;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var rawKey = headerValue.ToString();
        if (string.IsNullOrEmpty(rawKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!rawKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key format."));
        }

        // Use a scope to resolve scoped services (repository)
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        var apiKey = repository.GetByKeyHash(keyHash);

        if (apiKey == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        if (apiKey.IsRevoked)
        {
            return Task.FromResult(AuthenticateResult.Fail("API key has been revoked."));
        }

        if (apiKey.IsExpired())
        {
            return Task.FromResult(AuthenticateResult.Fail("API key has expired."));
        }

        // Record usage
        apiKey.RecordUsage();
        repository.Update(apiKey);
        repository.SaveChanges();

        // Build claims
        var claims = new List<Claim>
        {
            new(RbacClaimTypes.ApiKeyId, apiKey.Id.Value.ToString()),
            new(RbacClaimTypes.ApiKeyName, apiKey.Name),
            new(RbacClaimTypes.UserId, $"apikey:{apiKey.Id.Value}")
        };

        // Add Operator-level role assignment at Organization scope
        var roleAssignments = new List<RoleAssignmentClaim>
        {
            new()
            {
                Role = RoleId.Operator.Value,
                Scope = ScopeType.Organization.ToString(),
                ScopeId = apiKey.OrganizationId.Value.ToString()
            }
        };
        claims.Add(new Claim(RbacClaimTypes.RoleAssignments, JsonSerializer.Serialize(roleAssignments)));

        // Add direct API key permissions
        foreach (var permission in apiKey.Permissions)
        {
            claims.Add(new Claim(RbacClaimTypes.ApiPermission, permission));
        }

        // Add environment scope if set
        if (apiKey.EnvironmentId.HasValue)
        {
            claims.Add(new Claim(RbacClaimTypes.EnvironmentId, apiKey.EnvironmentId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

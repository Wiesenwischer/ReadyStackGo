using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;
using DomainUser = ReadyStackGo.Domain.IdentityAccess.Users.User;

namespace ReadyStackGo.API.Endpoints.Auth;

/// <summary>Transient state for an in-flight OIDC authorization-code flow.</summary>
public record OidcFlowState(string Provider, string Nonce, string CodeVerifier, string RedirectUri);

internal static class OidcPkce
{
    public static string NewToken()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    public static string Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public class OidcProviderDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>GET /api/auth/oidc/providers — enabled OIDC providers for login buttons. Anonymous.</summary>
public class OidcProvidersEndpoint : EndpointWithoutRequest<List<OidcProviderDto>>
{
    private readonly IOidcSettingsService _settings;

    public OidcProvidersEndpoint(IOidcSettingsService settings)
    {
        _settings = settings;
    }

    public override void Configure()
    {
        Get("/api/auth/oidc/providers");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var providers = await _settings.GetAllAsync(ct);
        Response = providers
            .Where(p => p.Enabled)
            .Select(p => new OidcProviderDto { Name = p.Name, DisplayName = p.DisplayName })
            .ToList();
    }
}

public class OidcRouteRequest
{
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/auth/oidc/{provider}/challenge — starts the OIDC flow: generates state/nonce/PKCE,
/// stores them server-side and redirects to the provider. Anonymous.
/// </summary>
public class OidcChallengeEndpoint : Endpoint<OidcRouteRequest>
{
    private readonly IOidcSettingsService _settings;
    private readonly IOidcService _oidc;
    private readonly ISystemConfigService _systemConfig;
    private readonly IMemoryCache _cache;

    public OidcChallengeEndpoint(
        IOidcSettingsService settings,
        IOidcService oidc,
        ISystemConfigService systemConfig,
        IMemoryCache cache)
    {
        _settings = settings;
        _oidc = oidc;
        _systemConfig = systemConfig;
        _cache = cache;
    }

    public override void Configure()
    {
        Get("/api/auth/oidc/{provider}/challenge");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(OidcRouteRequest req, CancellationToken ct)
    {
        var provider = await _settings.GetByNameAsync(req.Provider, ct);
        if (provider is not { Enabled: true })
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var baseUrl = (await _systemConfig.GetBaseUrlAsync()).TrimEnd('/');
        var redirectUri = $"{baseUrl}/api/auth/oidc/{provider.Name}/callback";

        var state = OidcPkce.NewToken();
        var nonce = OidcPkce.NewToken();
        var codeVerifier = OidcPkce.NewToken();
        var codeChallenge = OidcPkce.Challenge(codeVerifier);

        _cache.Set(
            StateKey(state),
            new OidcFlowState(provider.Name, nonce, codeVerifier, redirectUri),
            TimeSpan.FromMinutes(10));

        var authorizeUrl = await _oidc.BuildAuthorizeUrlAsync(provider, redirectUri, state, nonce, codeChallenge, ct);

        await Send.RedirectAsync(authorizeUrl, isPermanent: false, allowRemoteRedirects: true);
    }

    internal static string StateKey(string state) => $"oidc_state:{state}";
}

public class OidcCallbackRequest
{
    public string Provider { get; set; } = string.Empty;

    [QueryParam]
    public string? Code { get; set; }

    [QueryParam]
    public string? State { get; set; }

    [QueryParam]
    public string? Error { get; set; }
}

/// <summary>
/// GET /api/auth/oidc/{provider}/callback — completes the OIDC flow, performs just-in-time
/// account mapping, mints the ReadyStackGo session token and redirects back to the SPA.
/// Anonymous.
/// </summary>
public class OidcCallbackEndpoint : Endpoint<OidcCallbackRequest>
{
    private readonly IOidcSettingsService _settings;
    private readonly IOidcService _oidc;
    private readonly ISystemConfigService _systemConfig;
    private readonly IMemoryCache _cache;
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;
    private readonly ITokenService _tokenService;

    public OidcCallbackEndpoint(
        IOidcSettingsService settings,
        IOidcService oidc,
        ISystemConfigService systemConfig,
        IMemoryCache cache,
        IUserRepository users,
        IInvitationRepository invitations,
        ITokenService tokenService)
    {
        _settings = settings;
        _oidc = oidc;
        _systemConfig = systemConfig;
        _cache = cache;
        _users = users;
        _invitations = invitations;
        _tokenService = tokenService;
    }

    public override void Configure()
    {
        Get("/api/auth/oidc/{provider}/callback");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(OidcCallbackRequest req, CancellationToken ct)
    {
        var baseUrl = (await _systemConfig.GetBaseUrlAsync()).TrimEnd('/');

        if (!string.IsNullOrEmpty(req.Error) || string.IsNullOrEmpty(req.Code) || string.IsNullOrEmpty(req.State))
        {
            await RedirectToLoginError(baseUrl, "oidc_failed", ct);
            return;
        }

        if (!_cache.TryGetValue(OidcChallengeEndpoint.StateKey(req.State), out OidcFlowState? flow) || flow is null)
        {
            await RedirectToLoginError(baseUrl, "oidc_state", ct);
            return;
        }
        _cache.Remove(OidcChallengeEndpoint.StateKey(req.State));

        if (!string.Equals(flow.Provider, req.Provider, StringComparison.OrdinalIgnoreCase))
        {
            await RedirectToLoginError(baseUrl, "oidc_state", ct);
            return;
        }

        var provider = await _settings.GetByNameAsync(req.Provider, ct);
        if (provider is not { Enabled: true })
        {
            await RedirectToLoginError(baseUrl, "oidc_provider", ct);
            return;
        }

        var userInfo = await _oidc.ExchangeCodeAsync(
            provider, req.Code, flow.RedirectUri, flow.CodeVerifier, flow.Nonce, ct);

        if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
        {
            await RedirectToLoginError(baseUrl, "oidc_token", ct);
            return;
        }

        var user = ResolveUser(provider.Name, userInfo);
        if (user == null)
        {
            // No existing account and no pending invitation: access is denied.
            await RedirectToLoginError(baseUrl, "oidc_no_account", ct);
            return;
        }

        var token = _tokenService.GenerateToken(user);
        // Hand the token to the SPA via the URL fragment (not sent to the server / logs).
        await Send.RedirectAsync($"{baseUrl}/oidc-callback#token={Uri.EscapeDataString(token)}",
            isPermanent: false, allowRemoteRedirects: true);
    }

    /// <summary>
    /// Just-in-time mapping: (1) existing external identity, (2) existing user by email,
    /// (3) pending invitation, else null (access denied).
    /// </summary>
    private DomainUser? ResolveUser(string providerName, OidcUserInfo userInfo)
    {
        var now = SystemClock.UtcNow;
        var email = new EmailAddress(userInfo.Email!);

        // (1) Already linked to this provider's subject.
        var existingByEmail = _users.FindByEmail(email);
        if (existingByEmail != null)
        {
            var link = existingByEmail.FindExternalIdentity(providerName);
            if (link == null)
            {
                existingByEmail.LinkExternalIdentity(providerName, userInfo.Subject);
            }
            // The IdP asserted ownership of this email; reflect that honestly.
            if (!existingByEmail.IsEmailVerified)
            {
                existingByEmail.VerifyEmail(now);
            }
            _users.Update(existingByEmail);
            return existingByEmail;
        }

        // (3) Pending invitation for this email → just-in-time provisioning.
        var invitation = _invitations.FindPendingByEmail(email);
        if (invitation == null)
        {
            return null;
        }

        var username = ResolveUsername(email.Value);
        var newUser = DomainUser.RegisterExternal(_users.NextIdentity(), username, email, providerName, userInfo.Subject);
        newUser.AssignRole(invitation.ToRoleAssignment());

        try
        {
            invitation.Accept(now);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        _users.Add(newUser);
        _invitations.Update(invitation);
        return newUser;
    }

    private string ResolveUsername(string email)
    {
        var baseName = email.Split('@')[0];
        if (baseName.Length < 3) baseName = baseName.PadRight(3, '0');
        if (baseName.Length > 40) baseName = baseName[..40];

        var candidate = baseName;
        var suffix = 1;
        while (_users.FindByUsername(candidate) != null)
        {
            candidate = $"{baseName}{suffix++}";
        }
        return candidate;
    }

    private async Task RedirectToLoginError(string baseUrl, string reason, CancellationToken ct)
    {
        await Send.RedirectAsync($"{baseUrl}/login?error={reason}", isPermanent: false, allowRemoteRedirects: true);
    }
}

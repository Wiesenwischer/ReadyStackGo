using FastEndpoints;
using ReadyStackGo.API.Endpoints.Sso;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.API.Endpoints.Auth;

public class OidcProviderDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Icon of the provider's template, or null (the UI shows a key).</summary>
    public string? IconUrl { get; set; }
}

/// <summary>GET /api/auth/oidc/providers — enabled OIDC providers for login buttons. Anonymous.</summary>
public class OidcProvidersEndpoint : EndpointWithoutRequest<List<OidcProviderDto>>
{
    private readonly IOidcSettingsService _settings;
    private readonly IIdentityProviderTemplateCatalog _templates;

    public OidcProvidersEndpoint(IOidcSettingsService settings, IIdentityProviderTemplateCatalog templates)
    {
        _settings = settings;
        _templates = templates;
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
            .Select(p => new OidcProviderDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                IconUrl = TemplateIcons.Url(_templates, p.Template)
            })
            .ToList();
    }
}

public class OidcRouteRequest
{
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/auth/oidc/{provider}/challenge — starts the sign-in: generates state/nonce/PKCE,
/// stores them server-side and redirects to the provider (via PAR where required). Anonymous.
/// </summary>
public class OidcChallengeEndpoint : Endpoint<OidcRouteRequest>
{
    private readonly IOidcSettingsService _settings;
    private readonly IOidcService _oidc;
    private readonly ISystemConfigService _systemConfig;
    private readonly SsoFlowStore _flows;

    public OidcChallengeEndpoint(
        IOidcSettingsService settings,
        IOidcService oidc,
        ISystemConfigService systemConfig,
        SsoFlowStore flows)
    {
        _settings = settings;
        _oidc = oidc;
        _systemConfig = systemConfig;
        _flows = flows;
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
        var redirectUri = BaseUrlRules.ProviderRedirectUri(baseUrl, provider.Name);

        var state = SsoTokens.NewToken();
        var nonce = SsoTokens.NewToken();
        var codeVerifier = SsoTokens.NewToken();

        var result = await _oidc.BuildAuthorizeUrlAsync(
            provider, redirectUri, state, nonce, SsoTokens.PkceChallenge(codeVerifier), ct);

        if (!result.Succeeded)
        {
            var error = await OidcSignInFailures.RecordAsync(_settings, provider, result.Error!, result.ErrorDescription, ct);
            await Send.RedirectAsync($"{baseUrl}/login?error={error}", isPermanent: false, allowRemoteRedirects: true);
            return;
        }

        _flows.PutOidc(state, new OidcFlowState(provider.Name, nonce, codeVerifier, redirectUri));
        await Send.RedirectAsync(result.Url!, isPermanent: false, allowRemoteRedirects: true);
    }
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
/// GET /api/auth/oidc/{provider}/callback — completes an OIDC round trip. Depending on the
/// purpose of the flow it signs the user in (account mapping, ReadyStackGo token), stores the
/// result of a test sign-in in its setup session, or creates the first system administrator
/// of a wizard run. Anonymous.
/// </summary>
public class OidcCallbackEndpoint : Endpoint<OidcCallbackRequest>
{
    private readonly IOidcSettingsService _settings;
    private readonly IOidcService _oidc;
    private readonly ISystemConfigService _systemConfig;
    private readonly SsoFlowStore _flows;
    private readonly OidcAccountResolver _resolver;
    private readonly ITokenService _tokenService;
    private readonly SsoSetupService _setup;
    private readonly WizardSsoService _wizard;
    private readonly ILogger<OidcCallbackEndpoint> _logger;

    public OidcCallbackEndpoint(
        IOidcSettingsService settings,
        IOidcService oidc,
        ISystemConfigService systemConfig,
        SsoFlowStore flows,
        OidcAccountResolver resolver,
        ITokenService tokenService,
        SsoSetupService setup,
        WizardSsoService wizard,
        ILogger<OidcCallbackEndpoint> logger)
    {
        _settings = settings;
        _oidc = oidc;
        _systemConfig = systemConfig;
        _flows = flows;
        _resolver = resolver;
        _tokenService = tokenService;
        _setup = setup;
        _wizard = wizard;
        _logger = logger;
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

        var flow = string.IsNullOrEmpty(req.State) ? null : _flows.TakeOidc(req.State);
        if (flow == null || !string.Equals(flow.Provider, req.Provider, StringComparison.OrdinalIgnoreCase))
        {
            await Redirect($"{baseUrl}/login?error={OidcSignInErrors.State}");
            return;
        }

        switch (flow.Purpose)
        {
            case OidcFlowPurpose.TestSignIn:
                await Redirect(await _setup.CompleteTestSignInAsync(flow, HttpContext, req.Code, req.Error, baseUrl, ct));
                return;
            case OidcFlowPurpose.WizardAdmin:
                await Redirect(await _wizard.CompleteSignInAsync(flow, HttpContext, req.Code, req.Error, baseUrl, ct));
                return;
        }

        if (!string.IsNullOrEmpty(req.Error) || string.IsNullOrEmpty(req.Code))
        {
            await Redirect($"{baseUrl}/login?error={OidcSignInErrors.Failed}");
            return;
        }

        var provider = await _settings.GetByNameAsync(req.Provider, ct);
        if (provider is not { Enabled: true })
        {
            await Redirect($"{baseUrl}/login?error={OidcSignInErrors.Provider}");
            return;
        }

        var exchange = await _oidc.ExchangeCodeAsync(provider, req.Code, flow.RedirectUri, flow.CodeVerifier, flow.Nonce, ct);
        if (!exchange.Succeeded)
        {
            var error = await OidcSignInFailures.RecordAsync(_settings, provider, exchange.Error!, exchange.ErrorDescription, ct);
            await Redirect($"{baseUrl}/login?error={error}");
            return;
        }

        var resolution = _resolver.Resolve(provider, exchange.UserInfo!, SystemClock.UtcNow);
        if (resolution.User == null)
        {
            _logger.LogInformation("OIDC sign-in via {Provider} rejected: {Reason}", provider.Name, resolution.Error);
            await Redirect($"{baseUrl}/login?error={resolution.Error}");
            return;
        }

        if (provider.ReconnectNeeded || provider.LastResult is { Passed: false })
        {
            await _settings.RecordResultAsync(provider.Name,
                new OidcLastResult(SystemClock.UtcNow, OidcResultKinds.SignIn, true, null),
                reconnectNeeded: false, ct);
        }

        var token = _tokenService.GenerateToken(resolution.User);
        // Hand the token to the SPA via the URL fragment (not sent to the server / logs).
        await Redirect($"{baseUrl}/oidc-callback#token={Uri.EscapeDataString(token)}");
    }

    private Task Redirect(string url) => Send.RedirectAsync(url, isPermanent: false, allowRemoteRedirects: true);
}

/// <summary>Maps failed calls of a sign-in to login errors and records them at the provider.</summary>
internal static class OidcSignInFailures
{
    /// <summary>
    /// Records the failure as the provider's last result. invalid_client of a paired provider
    /// marks it "Reconnect needed" (the pairing was probably removed at the provider).
    /// Returns the error code for the login page.
    /// </summary>
    public static async Task<string> RecordAsync(
        IOidcSettingsService settings, OidcProviderSettings provider, string error, string? description, CancellationToken ct)
    {
        var (loginError, message) = error switch
        {
            OidcErrorCodes.InvalidClient => (OidcSignInErrors.ProviderRejected, "Sign-in failed: the provider rejected the client (invalid_client)"),
            OidcErrorCodes.Unreachable => (OidcSignInErrors.Unreachable, "Sign-in failed: the provider is not reachable"),
            OidcErrorCodes.InvalidToken => (OidcSignInErrors.Token, "Sign-in failed: the id token was rejected"),
            _ => (OidcSignInErrors.Failed, $"Sign-in failed: {description ?? error}")
        };

        bool? reconnectNeeded = error == OidcErrorCodes.InvalidClient && provider.IsPaired ? true : null;
        await settings.RecordResultAsync(provider.Name,
            new OidcLastResult(SystemClock.UtcNow, OidcResultKinds.SignIn, false, message),
            reconnectNeeded, ct);
        return loginError;
    }
}

/// <summary>Builds the anonymous URL of a template icon.</summary>
internal static class TemplateIcons
{
    public static string? Url(IIdentityProviderTemplateCatalog catalog, string? templateId)
    {
        var template = catalog.GetTemplate(templateId);
        return template is { HasIcon: true } ? $"/api/identity-provider-templates/{template.Id}/icon" : null;
    }
}

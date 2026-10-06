using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.API.Endpoints.Sso;

/// <summary>In-memory wizard runs (lost on restart).</summary>
public class WizardSsoRunStore
{
    private readonly ConcurrentDictionary<string, WizardSsoRun> _runs = new(StringComparer.Ordinal);

    public void Put(WizardSsoRun run) => _runs[run.Id] = run;

    public WizardSsoRun? Get(string? id) => id != null && _runs.TryGetValue(id, out var run) ? run : null;

    /// <summary>The most recent run started by the browser that presents <paramref name="flowSecret"/>.</summary>
    public WizardSsoRun? FindBySecret(string? flowSecret) =>
        string.IsNullOrEmpty(flowSecret)
            ? null
            : _runs.Values.Where(r => r.FlowSecret == flowSecret).OrderByDescending(r => r.StartedAt).FirstOrDefault();

    public void Remove(string id) => _runs.TryRemove(id, out _);
}

/// <summary>
/// The setup wizard's path in which an identity provider account becomes the first system
/// administrator (E19): start (inside the setup window) → pairing in the browser → continue
/// (redeem, save the provider disabled) → sign-in in the browser → admin created, provider
/// enabled. Every step after the start is bound to the browser that started it (flow cookie)
/// and checks the run's own expiry and that no system administrator exists yet. All redirects
/// of a run go to the address the run was started with; the installation's base URL is saved
/// only when the run creates the administrator, so a second, anonymous start cannot redirect
/// another run or leave its address behind.
/// </summary>
public class WizardSsoService
{
    public const string HttpsRequired = "https_required";
    public const string RunCookieLifetimeKey = "Wizard:SsoRunSeconds";

    private readonly WizardSsoRunStore _runs;
    private readonly SsoFlowStore _flows;
    private readonly IIdentityProviderTemplateCatalog _templates;
    private readonly IEnumerable<IClientRegistrationMethod> _registrationMethods;
    private readonly IOidcSettingsService _settings;
    private readonly IOidcService _oidc;
    private readonly IOidcConnectionChecker _checker;
    private readonly ISystemConfigService _systemConfig;
    private readonly SystemAdminRegistrationService _adminRegistration;
    private readonly UsernameGenerator _usernames;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WizardSsoService> _logger;

    public WizardSsoService(
        WizardSsoRunStore runs,
        SsoFlowStore flows,
        IIdentityProviderTemplateCatalog templates,
        IEnumerable<IClientRegistrationMethod> registrationMethods,
        IOidcSettingsService settings,
        IOidcService oidc,
        IOidcConnectionChecker checker,
        ISystemConfigService systemConfig,
        SystemAdminRegistrationService adminRegistration,
        UsernameGenerator usernames,
        ITokenService tokens,
        IConfiguration configuration,
        ILogger<WizardSsoService> logger)
    {
        _runs = runs;
        _flows = flows;
        _templates = templates;
        _registrationMethods = registrationMethods;
        _settings = settings;
        _oidc = oidc;
        _checker = checker;
        _systemConfig = systemConfig;
        _adminRegistration = adminRegistration;
        _usernames = usernames;
        _tokens = tokens;
        _configuration = configuration;
        _logger = logger;
    }

    public IReadOnlyList<IdentityProviderTemplate> OfferedTemplates() =>
        _templates.GetTemplates()
            .Where(t => t.OfferInSetup && t.HasFixedAuthority && _registrationMethods.Any(m =>
                m.Kind == t.RegistrationKind && m.Interaction == RegistrationInteraction.Connect))
            .ToList();

    public TimeSpan RunDuration =>
        _configuration.GetValue<int?>(RunCookieLifetimeKey) is { } seconds && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : WizardSsoRun.DefaultDuration;

    /// <summary>Starts a run and the pairing. Throws <see cref="SsoSetupException"/> with a code the wizard explains.</summary>
    public async Task<(WizardSsoRun Run, RegistrationStart Start)> StartAsync(HttpContext http, string templateId, string? baseUrl, CancellationToken ct)
    {
        if (_adminRegistration.SystemAdminExists())
        {
            throw new SsoSetupException(WizardSsoFailure.CompletedElsewhere, "Setup was completed elsewhere.", StatusCodes.Status409Conflict);
        }

        var template = OfferedTemplates().FirstOrDefault(t => t.Id == templateId)
            ?? throw new SsoSetupException("template_unknown", "This sign-in method is not offered.", StatusCodes.Status404NotFound);

        var normalized = BaseUrlRules.Normalize(baseUrl)
            ?? throw new SsoSetupException("base_url_invalid", "Enter the address of this installation as an absolute http or https address.");
        if (template.RequireHttps && !BaseUrlRules.SatisfiesHttpsRequirement(normalized))
        {
            throw new SsoSetupException(HttpsRequired,
                $"{template.Name} only connects installations that use HTTPS. http:// is allowed for localhost only.");
        }

        var discovery = await _checker.CheckAsync(
            new OidcCheckRequest(template.AuthorityUrl!, null, null, template.Scopes, null, template.RequirePar),
            OidcCheckScope.Discovery, ct);
        if (discovery.Items.FirstOrDefault(i => i.Id == OidcCheckIds.Discovery)?.Status != OidcCheckStatus.Passed)
        {
            var detail = discovery.Items.FirstOrDefault()?.Detail;
            throw new SsoSetupException(WizardSsoFailure.Unreachable,
                $"ReadyStackGo could not reach {template.AuthorityUrl}{(detail != null ? $" ({detail.TrimEnd('.')})" : "")}.",
                StatusCodes.Status502BadGateway);
        }

        var duration = RunDuration;
        var secret = SsoFlowCookie.Ensure(http, duration + TimeSpan.FromMinutes(5));
        var run = new WizardSsoRun(SsoTokens.NewToken(), template.Id, secret, normalized, SystemClock.UtcNow, duration);
        _runs.Put(run);

        var start = await StartPairingAsync(run, template, ct);
        return (run, start);
    }

    /// <summary>The run of this browser, or null.</summary>
    public WizardSsoRun? CurrentRun(HttpContext http)
    {
        var run = _runs.FindBySecret(SsoFlowCookie.Read(http));
        if (run == null)
        {
            return null;
        }
        Refresh(run);
        return run;
    }

    /// <summary>Called by the registration callback: stores the pairing code or the error.</summary>
    public void AcceptRegistrationReturn(WizardSsoRun run, string state, string? code, string? error)
    {
        if (run.State != WizardSsoRunState.Started)
        {
            return;
        }
        if (!string.Equals(run.PendingRegistrationState, state, StringComparison.Ordinal))
        {
            run.Fail(WizardSsoFailure.RegistrationFailed, "The connection does not belong to this setup run.");
            return;
        }

        switch (error)
        {
            case null or "" when !string.IsNullOrEmpty(code):
                run.PendingRegistrationCode = code;
                break;
            case "access_denied":
                run.Fail(WizardSsoFailure.Cancelled);
                break;
            case "limit_reached":
                run.Fail(WizardSsoFailure.LimitReached);
                break;
            default:
                run.Fail(WizardSsoFailure.RegistrationFailed, error);
                break;
        }
    }

    /// <summary>Redeems the pairing code and saves the provider disabled (Started → Registered).</summary>
    public async Task ContinueAsync(WizardSsoRun run, CancellationToken ct)
    {
        Refresh(run);
        if (run.State != WizardSsoRunState.Started)
        {
            return;
        }
        if (string.IsNullOrEmpty(run.PendingRegistrationCode) || string.IsNullOrEmpty(run.PendingRegistrationState))
        {
            throw new SsoSetupException("registration_pending", "The connection has not come back yet.", StatusCodes.Status409Conflict);
        }

        var template = _templates.GetTemplate(run.TemplateId);
        if (template == null)
        {
            run.Fail(WizardSsoFailure.RegistrationFailed, "The template is no longer offered.");
            return;
        }

        var method = _registrationMethods.First(m => m.Kind == template.RegistrationKind);
        var providers = await _settings.GetAllAsync(ct);
        var name = ProviderNameFor(template, providers);

        RegisteredClient client;
        try
        {
            client = await method.CompleteAsync(Context(run, template, name, run.PendingRegistrationState), run.PendingRegistrationCode, ct);
        }
        catch (ClientRegistrationException ex)
        {
            run.Fail(ex.Code == "unreachable" ? WizardSsoFailure.Unreachable : WizardSsoFailure.RegistrationFailed, ex.Message);
            return;
        }

        // Another run may have saved or enabled this provider while the code was redeemed.
        Refresh(run);
        if (run.State != WizardSsoRunState.Started)
        {
            return;
        }
        var existing = await _settings.GetByNameAsync(name, ct);
        if (existing != null && (existing.Enabled || existing.Template != template.Id))
        {
            run.Fail(WizardSsoFailure.RegistrationFailed, $"The provider \"{name}\" was set up elsewhere in the meantime.");
            return;
        }

        var provider = new OidcProviderSettings
        {
            Name = name,
            DisplayName = template.ProviderDisplayName ?? template.Name,
            Authority = template.AuthorityUrl!,
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            Scopes = template.Scopes,
            Enabled = false,
            Template = template.Id,
            Registration = template.RegistrationKind,
            RequirePar = template.RequirePar,
            Claims = template.Claims,
            PairedAt = client.RegisteredAt,
            PairedBy = client.ConfirmedBy,
            TrustUnverifiedEmail = false
        };

        if (existing != null)
        {
            await _settings.UpdateAsync(provider, ct);
        }
        else
        {
            await _settings.AddAsync(provider, ct);
        }

        try
        {
            run.MarkRegistered(name, SystemClock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            // Expired while saving: the provider stays disabled, the run ends.
            run.Fail(WizardSsoFailure.Expired);
        }
    }

    /// <summary>Builds the sign-in URL of a registered run (browser navigation).</summary>
    public async Task<string?> SignInUrlAsync(WizardSsoRun run, CancellationToken ct)
    {
        Refresh(run);
        if (run.State != WizardSsoRunState.Registered)
        {
            return null;
        }

        var provider = await _settings.GetByNameAsync(run.ProviderName!, ct);
        if (provider == null)
        {
            run.Fail(WizardSsoFailure.SignInFailed, "The provider was removed.");
            return null;
        }

        var redirectUri = BaseUrlRules.ProviderRedirectUri(run.BaseUrl, provider.Name);
        var state = SsoTokens.NewToken();
        var nonce = SsoTokens.NewToken();
        var verifier = SsoTokens.NewToken();
        var result = await _oidc.BuildAuthorizeUrlAsync(provider, redirectUri, state, nonce, SsoTokens.PkceChallenge(verifier), ct);
        if (!result.Succeeded)
        {
            run.Fail(result.Error == OidcErrorCodes.Unreachable ? WizardSsoFailure.Unreachable : WizardSsoFailure.SignInFailed,
                result.ErrorDescription);
            return null;
        }

        _flows.PutOidc(state, new OidcFlowState(provider.Name, nonce, verifier, redirectUri, OidcFlowPurpose.WizardAdmin, run.Id, run.FlowSecret));
        return result.Url;
    }

    /// <summary>OIDC callback of the wizard sign-in: creates the first system administrator.</summary>
    public async Task<string> CompleteSignInAsync(OidcFlowState flow, HttpContext http, string? code, string? error, string baseUrl, CancellationToken ct)
    {
        var run = _runs.Get(flow.ContextId);
        if (run == null || !SsoFlowCookie.Matches(http, run.FlowSecret))
        {
            // Another browser cannot finish a run (E19).
            return $"{baseUrl}/wizard?sso=foreign";
        }

        // From here on every redirect goes to the address this run was started with.
        var returned = $"{run.BaseUrl.TrimEnd('/')}/wizard?sso=returned";

        Refresh(run);
        if (run.State != WizardSsoRunState.Registered)
        {
            return returned;
        }

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            run.Fail(error == "access_denied" ? WizardSsoFailure.Cancelled : WizardSsoFailure.SignInFailed, error);
            return returned;
        }

        var provider = await _settings.GetByNameAsync(flow.Provider, ct);
        if (provider == null)
        {
            run.Fail(WizardSsoFailure.SignInFailed, "The provider was removed.");
            return returned;
        }

        var exchange = await _oidc.ExchangeCodeAsync(provider, code, flow.RedirectUri, flow.CodeVerifier, flow.Nonce, ct);
        if (!exchange.Succeeded)
        {
            run.Fail(exchange.Error == OidcErrorCodes.Unreachable ? WizardSsoFailure.Unreachable : WizardSsoFailure.SignInFailed,
                exchange.ErrorDescription);
            return returned;
        }

        var info = exchange.UserInfo!;
        if (!info.EmailVerified || string.IsNullOrEmpty(info.Email))
        {
            run.Fail(WizardSsoFailure.EmailUnverified);
            return returned;
        }

        EmailAddress email;
        try
        {
            email = new EmailAddress(info.Email);
        }
        catch (ArgumentException)
        {
            run.Fail(WizardSsoFailure.EmailInvalid);
            return returned;
        }

        // The exchange may have taken a while: check expiry and state right before the
        // administrator is created. After that nothing may fail before the provider is enabled.
        Refresh(run);
        if (run.State != WizardSsoRunState.Registered)
        {
            return returned;
        }

        User admin;
        try
        {
            var username = _usernames.Generate(info.Username, email.Value);
            admin = _adminRegistration.RegisterExternalSystemAdmin(username, email, provider.Name, info.Subject);
        }
        catch (InvalidOperationException)
        {
            if (run.State != WizardSsoRunState.SignedIn)
            {
                run.Fail(WizardSsoFailure.CompletedElsewhere);
            }
            return returned;
        }

        var now = SystemClock.UtcNow;
        run.MarkSignedIn(admin.Username, admin.Email.Value, info.DisplayName);

        provider.Enabled = true;
        provider.TestedSignIn = new OidcTestedSignIn(now, OidcConnectionFingerprint.Compute(provider));
        provider.LastResult = new OidcLastResult(now, OidcResultKinds.SignIn, true, null);
        await _settings.UpdateAsync(provider, ct);
        await _systemConfig.SetBaseUrlAsync(run.BaseUrl);

        _logger.LogInformation("First system administrator {Username} created through {Provider}", admin.Username, provider.Name);

        var token = _tokens.GenerateToken(admin);
        return $"{returned}#token={Uri.EscapeDataString(token)}";
    }

    /// <summary>
    /// "Try again" inside the run: pair again (returns the pairing start) or sign in again
    /// (returns null; the browser navigates to the sign-in endpoint).
    /// </summary>
    public async Task<RegistrationStart?> RetryAsync(WizardSsoRun run, CancellationToken ct)
    {
        Refresh(run);
        try
        {
            run.Retry(SystemClock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new SsoSetupException("retry_not_possible", ex.Message, StatusCodes.Status409Conflict);
        }

        if (run.State == WizardSsoRunState.Registered)
        {
            return null;
        }

        var template = _templates.GetTemplate(run.TemplateId)
            ?? throw new SsoSetupException("template_unknown", "This sign-in method is not offered.", StatusCodes.Status404NotFound);
        return await StartPairingAsync(run, template, ct);
    }

    /// <summary>"Use built-in sign-in instead": ends the run. A paired provider stays disabled.</summary>
    public void End(WizardSsoRun run) => _runs.Remove(run.Id);

    /// <summary>Applies expiry and "another path won" to a run that is not final.</summary>
    private void Refresh(WizardSsoRun run)
    {
        if (run.State is WizardSsoRunState.SignedIn)
        {
            return;
        }
        if (run.State != WizardSsoRunState.Failed || !WizardSsoFailure.IsFinal(run.FailureReason!))
        {
            if (run.IsExpired(SystemClock.UtcNow))
            {
                run.Fail(WizardSsoFailure.Expired);
            }
            else if (_adminRegistration.SystemAdminExists())
            {
                run.Fail(WizardSsoFailure.CompletedElsewhere);
            }
        }
    }

    private async Task<RegistrationStart> StartPairingAsync(WizardSsoRun run, IdentityProviderTemplate template, CancellationToken ct)
    {
        var method = _registrationMethods.First(m => m.Kind == template.RegistrationKind);
        var providers = await _settings.GetAllAsync(ct);
        var name = ProviderNameFor(template, providers);
        var state = SsoTokens.NewToken();

        RegistrationStart start;
        try
        {
            start = await method.StartAsync(Context(run, template, name, state), ct);
        }
        catch (ClientRegistrationException ex)
        {
            run.Fail(ex.Code == "unreachable" ? WizardSsoFailure.Unreachable : WizardSsoFailure.RegistrationFailed, ex.Message);
            throw new SsoSetupException(ex.Code == "unreachable" ? WizardSsoFailure.Unreachable : WizardSsoFailure.RegistrationFailed,
                ex.Message, StatusCodes.Status502BadGateway);
        }

        run.PendingRegistrationState = state;
        run.PendingRegistrationCode = null;
        // The pairing may take as long as the run (for example with an account created at the provider).
        var lifetime = run.ExpiresAt - SystemClock.UtcNow;
        _flows.PutRegistration(state, new RegistrationFlowState(RegistrationFlowOwner.WizardRun, run.Id, run.FlowSecret),
            lifetime > SsoFlowStore.Lifetime ? lifetime : SsoFlowStore.Lifetime);
        return start;
    }

    private RegistrationContext Context(WizardSsoRun run, IdentityProviderTemplate template, string providerName, string state) => new(
        template.AuthorityUrl!,
        providerName,
        SsoSetupService.InstallationName(run.BaseUrl),
        run.BaseUrl,
        BaseUrlRules.ProviderRedirectUri(run.BaseUrl, providerName),
        run.BaseUrl.TrimEnd('/') + SsoSetupService.RegistrationCallbackPath,
        state);

    /// <summary>
    /// The template's provider name, reusing a disabled provider of the same template left by an
    /// earlier, aborted run; otherwise the next free name.
    /// </summary>
    private static string ProviderNameFor(IdentityProviderTemplate template, IReadOnlyList<OidcProviderSettings> providers)
    {
        var candidate = template.ProviderName;
        for (var i = 2; ; i++)
        {
            var existing = providers.FirstOrDefault(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (existing == null || (!existing.Enabled && existing.Template == template.Id))
            {
                return candidate;
            }
            candidate = $"{template.ProviderName}-{i}";
        }
    }
}

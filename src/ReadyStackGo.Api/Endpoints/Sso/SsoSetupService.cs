using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.API.Endpoints.Sso;

/// <summary>A setup step failed for a reason the UI shows (HTTP status and machine-readable code).</summary>
public class SsoSetupException : Exception
{
    public SsoSetupException(string code, string message, int statusCode = StatusCodes.Status400BadRequest, string? reason = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Reason = reason;
    }

    public string Code { get; }
    public int StatusCode { get; }

    /// <summary>The underlying cause on its own (e.g. "connection refused"), for UIs that build their own text.</summary>
    public string? Reason { get; }
}

/// <summary>
/// The rules of "Add provider" and of a provider's page: sessions, discovery, address of this
/// installation, registration (manual or pairing), checks, test sign-in, saving, removing,
/// and the lockout protection (E14).
/// </summary>
public class SsoSetupService
{
    /// <summary>Path the pairing returns to (outside /api/auth/oidc/{name}/ so no provider name collides).</summary>
    public const string RegistrationCallbackPath = "/api/sso/registration/callback";

    private readonly SsoSetupSessionStore _sessions;
    private readonly SsoFlowStore _flows;
    private readonly IIdentityProviderTemplateCatalog _templates;
    private readonly IEnumerable<IClientRegistrationMethod> _registrationMethods;
    private readonly IOidcSettingsService _settings;
    private readonly IOidcService _oidc;
    private readonly IOidcConnectionChecker _checker;
    private readonly ISystemConfigService _systemConfig;
    private readonly ICredentialEncryptionService _encryption;
    private readonly IUserRepository _users;
    private readonly ILogger<SsoSetupService> _logger;

    public SsoSetupService(
        SsoSetupSessionStore sessions,
        SsoFlowStore flows,
        IIdentityProviderTemplateCatalog templates,
        IEnumerable<IClientRegistrationMethod> registrationMethods,
        IOidcSettingsService settings,
        IOidcService oidc,
        IOidcConnectionChecker checker,
        ISystemConfigService systemConfig,
        ICredentialEncryptionService encryption,
        IUserRepository users,
        ILogger<SsoSetupService> logger)
    {
        _sessions = sessions;
        _flows = flows;
        _templates = templates;
        _registrationMethods = registrationMethods;
        _settings = settings;
        _oidc = oidc;
        _checker = checker;
        _systemConfig = systemConfig;
        _encryption = encryption;
        _users = users;
        _logger = logger;
    }

    // ---------------------------------------------------------------- sessions

    public async Task<SsoSetupSession> CreateForTemplateAsync(Guid userId, string templateId, CancellationToken ct)
    {
        var template = _templates.GetTemplate(templateId)
            ?? throw new SsoSetupException("template_unknown", "This template is not offered.", StatusCodes.Status404NotFound);

        var providers = await _settings.GetAllAsync(ct);
        var session = new SsoSetupSession
        {
            Id = SsoTokens.NewToken(),
            OwnerUserId = userId,
            TemplateId = template.Id,
            Authority = template.AuthorityUrl,
            Name = SuggestName(template.ProviderName, providers),
            DisplayName = template.ProviderDisplayName ?? string.Empty,
            Scopes = template.Scopes,
            RegistrationKind = template.RegistrationKind,
            RequirePar = template.RequirePar,
            RequireHttps = template.RequireHttps,
            Claims = template.Claims,
            TrustUnverifiedEmail = false,
            BaseUrl = await _systemConfig.GetConfiguredBaseUrlAsync()
        };
        _sessions.Put(session);
        return session;
    }

    public async Task<SsoSetupSession> CreateForProviderAsync(Guid userId, string providerName, CancellationToken ct)
    {
        var provider = await _settings.GetByNameAsync(providerName, ct)
            ?? throw new SsoSetupException("provider_unknown", "This provider does not exist.", StatusCodes.Status404NotFound);
        var template = _templates.GetTemplate(provider.Template);

        var session = new SsoSetupSession
        {
            Id = SsoTokens.NewToken(),
            OwnerUserId = userId,
            TemplateId = provider.Template,
            ExistingProvider = provider.Name,
            Authority = provider.Authority,
            Name = provider.Name,
            DisplayName = provider.DisplayName,
            Scopes = provider.Scopes,
            RegistrationKind = provider.Registration,
            RequirePar = provider.RequirePar,
            RequireHttps = template?.RequireHttps ?? false,
            Claims = provider.Claims,
            TrustUnverifiedEmail = provider.TrustUnverifiedEmail,
            BaseUrl = await _systemConfig.GetEffectiveBaseUrlAsync(),
            ClientId = provider.ClientId,
            EncryptedClientSecret = string.IsNullOrEmpty(provider.ClientSecret) ? null : _encryption.Encrypt(provider.ClientSecret),
            PairedAt = provider.PairedAt,
            PairedBy = provider.PairedBy,
            ProviderFingerprintAtStart = OidcConnectionFingerprint.Compute(provider)
        };
        _sessions.Put(session);
        return session;
    }

    public SsoSetupSession Get(string id, Guid userId) =>
        _sessions.Get(id, userId)
        ?? throw new SsoSetupException("session_unknown", "This setup session does not exist or has expired.", StatusCodes.Status404NotFound);

    public void Cancel(string id, Guid userId)
    {
        Get(id, userId);
        _sessions.Remove(id);
    }

    public void Update(SsoSetupSession session, string? displayName, bool? trustUnverifiedEmail)
    {
        if (displayName != null)
        {
            session.DisplayName = displayName.Trim();
        }
        if (trustUnverifiedEmail.HasValue)
        {
            session.TrustUnverifiedEmail = trustUnverifiedEmail.Value;
        }
        _sessions.Put(session);
    }

    // ---------------------------------------------------------------- steps

    /// <summary>Step "Provider address": checks 1 to 3 against the entered authority.</summary>
    public async Task<OidcCheckReport> CheckDiscoveryAsync(SsoSetupSession session, string? authority, CancellationToken ct)
    {
        var template = _templates.GetTemplate(session.TemplateId);
        if (template?.HasFixedAuthority == true)
        {
            authority = template.AuthorityUrl;
        }

        if (string.IsNullOrWhiteSpace(authority) ||
            !Uri.TryCreate(authority.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new SsoSetupException("authority_invalid", "Enter the provider address as an absolute http or https address.");
        }

        var normalized = authority.Trim();
        if (!string.Equals(session.Authority, normalized, StringComparison.Ordinal))
        {
            session.Authority = normalized;
            InvalidateConnectionResults(session);
        }

        var report = await _checker.CheckAsync(
            new OidcCheckRequest(normalized, null, null, session.Scopes, null, session.RequirePar),
            OidcCheckScope.Discovery, ct);
        session.Discovery = report;
        _sessions.Put(session);
        return report;
    }

    /// <summary>
    /// Step "This installation": validates and stores the base URL (E8) and, for a new provider,
    /// the name that becomes part of the redirect URI (E7).
    /// </summary>
    public async Task SetInstallationAsync(SsoSetupSession session, string? baseUrl, string? name, CancellationToken ct)
    {
        var normalized = BaseUrlRules.Normalize(baseUrl)
            ?? throw new SsoSetupException("base_url_invalid", "Enter the address of this installation as an absolute http or https address without path parameters.");

        if (session.RequireHttps && !BaseUrlRules.SatisfiesHttpsRequirement(normalized))
        {
            throw new SsoSetupException(WizardSsoService.HttpsRequired,
                "This provider only connects installations that use HTTPS. http:// is allowed for localhost only.");
        }

        if (session.IsNew)
        {
            var candidate = (name ?? session.Name).Trim();
            if (!ThemeId.IsValid(candidate))
            {
                throw new SsoSetupException("name_invalid",
                    "The name may contain lowercase letters, digits and dashes (1 to 40 characters) and must start with a letter or digit.");
            }

            var providers = await _settings.GetAllAsync(ct);
            if (providers.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                throw new SsoSetupException("name_taken", $"A provider named '{candidate}' already exists.", StatusCodes.Status409Conflict);
            }

            if (!string.Equals(session.Name, candidate, StringComparison.Ordinal))
            {
                session.Name = candidate;
                // The redirect URI changed: credentials and results of the old one no longer apply.
                InvalidateConnectionResults(session);
            }
        }

        if (!string.Equals(session.BaseUrl, normalized, StringComparison.Ordinal))
        {
            session.BaseUrl = normalized;
            InvalidateConnectionResults(session);
        }

        await _systemConfig.SetBaseUrlAsync(normalized);
        _sessions.Put(session);
    }

    /// <summary>Step "Register" (manual kind): stores client ID, secret (empty keeps it) and scopes.</summary>
    public void SetManualCredentials(SsoSetupSession session, string? clientId, string? clientSecret, string? scopes)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new SsoSetupException("client_id_missing", "Enter the client ID.");
        }

        if (scopes != null)
        {
            var list = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!list.Contains("openid", StringComparer.Ordinal))
            {
                throw new SsoSetupException("scopes_invalid", "The scopes must contain openid.");
            }
            session.Scopes = string.Join(' ', list);
        }

        session.ClientId = clientId.Trim();
        if (!string.IsNullOrEmpty(clientSecret))
        {
            session.EncryptedClientSecret = _encryption.Encrypt(clientSecret);
        }
        InvalidateConnectionResults(session);
        _sessions.Put(session);
    }

    /// <summary>Step "Connect" (pairing kind): starts the browser round trip to the provider.</summary>
    public async Task<RegistrationStart> StartRegistrationAsync(SsoSetupSession session, HttpContext http, CancellationToken ct)
    {
        var method = Method(session.RegistrationKind);
        if (method.Interaction != RegistrationInteraction.Connect)
        {
            throw new SsoSetupException("registration_manual", "This provider is registered by entering client ID and secret.");
        }
        EnsureReadyForRegistration(session);

        var state = SsoTokens.NewToken();
        var context = RegistrationContext(session, state);
        RegistrationStart start;
        try
        {
            start = await method.StartAsync(context, ct);
        }
        catch (ClientRegistrationException ex)
        {
            session.RegistrationError = ex.Code;
            session.RegistrationErrorDescription = ex.Message;
            _sessions.Put(session);
            throw new SsoSetupException(ex.Code, ex.Message, StatusCodes.Status502BadGateway);
        }

        if (start.Kind == RegistrationStartKind.Completed)
        {
            ApplyRegisteredClient(session, start.Client!);
            _sessions.Put(session);
            return start;
        }

        var secret = SsoFlowCookie.Ensure(http, SsoFlowStore.Lifetime);
        _flows.PutRegistration(state, new RegistrationFlowState(RegistrationFlowOwner.SetupSession, session.Id, secret));
        session.PendingRegistrationState = state;
        session.PendingRegistrationCode = null;
        session.RegistrationError = null;
        session.RegistrationErrorDescription = null;
        _sessions.Put(session);
        return start;
    }

    /// <summary>Redeems the code the pairing brought back.</summary>
    public async Task CompleteRegistrationAsync(SsoSetupSession session, CancellationToken ct)
    {
        if (session.RegistrationError != null && session.PendingRegistrationCode == null)
        {
            throw new SsoSetupException(session.RegistrationError,
                session.RegistrationErrorDescription ?? "The connection was not completed.");
        }
        if (string.IsNullOrEmpty(session.PendingRegistrationCode) || string.IsNullOrEmpty(session.PendingRegistrationState))
        {
            throw new SsoSetupException("registration_pending", "Connect first.");
        }

        var method = Method(session.RegistrationKind);
        var context = RegistrationContext(session, session.PendingRegistrationState);
        try
        {
            // Not cancellable: once redeemed, the code is gone, and the provider may already have
            // replaced the old client; the result must reach the session.
            var client = await method.CompleteAsync(context, session.PendingRegistrationCode, CancellationToken.None);
            ApplyRegisteredClient(session, client);
        }
        catch (ClientRegistrationException ex)
        {
            session.RegistrationError = ex.Code;
            session.RegistrationErrorDescription = ex.Message;
            throw new SsoSetupException(ex.Code, ex.Message, StatusCodes.Status502BadGateway);
        }
        finally
        {
            session.PendingRegistrationCode = null;
            session.PendingRegistrationState = null;
            _sessions.Put(session);
        }

        await AdoptRedeemedPairingAsync(session);
    }

    /// <summary>
    /// Reconnect of a paired provider (Marcus, 12.10.2026): WYSCH invalidates the old client as soon
    /// as a new code for the same redirect URI is redeemed by the same account, so the old settings
    /// cannot stay active (E14). The new client is taken over right away when its redirect URI is the
    /// one ReadyStackGo signs in with now (then it is the client sign-ins need, whether the old one
    /// was replaced or had another redirect URI) and the server-side check of client ID and secret
    /// passed. Otherwise E14 applies. Enabling a disabled provider still needs a test sign-in: the
    /// provider's tested fingerprint stays the old one.
    /// </summary>
    private async Task<bool> AdoptionApplicableAsync(SsoSetupSession session)
    {
        if (session.IsNew || session.RegistrationKind != RegistrationKinds.Pairing || session.ExistingProvider == null ||
            !session.RegisteredInSession || session.AdoptedAfterPairing)
        {
            return false;
        }
        var signInBaseUrl = await _systemConfig.GetEffectiveBaseUrlAsync();
        return string.Equals(BaseUrlRules.ProviderRedirectUri(signInBaseUrl, session.Name), session.RedirectUri, StringComparison.Ordinal);
    }

    /// <summary>Takes the session's new client over if the client check of <paramref name="report"/> passed.</summary>
    private async Task AdoptIfClientPassedAsync(SsoSetupSession session, OidcCheckReport report)
    {
        if (report.Items.FirstOrDefault(i => i.Id == OidcCheckIds.Client)?.Status != OidcCheckStatus.Passed)
        {
            return;
        }
        // Not cancelled with the request: WYSCH has replaced the old client already.
        var existing = await _settings.GetByNameAsync(session.ExistingProvider!, CancellationToken.None);
        if (existing == null)
        {
            return;
        }
        ApplyConnection(existing, session);
        existing.ReconnectNeeded = false;
        existing.LastResult = new OidcLastResult(SystemClock.UtcNow, OidcResultKinds.Checks, report.Passed,
            report.Passed ? null : report.Items.FirstOrDefault(i => i.Status == OidcCheckStatus.Failed)?.Title);
        await _settings.UpdateAsync(existing, CancellationToken.None);

        session.AdoptedAfterPairing = true;
        session.ProviderFingerprintAtStart = OidcConnectionFingerprint.Compute(existing);
        _logger.LogInformation("Reconnect of {Provider}: new client {ClientId} taken over after the pairing", existing.Name, existing.ClientId);
    }

    private async Task AdoptRedeemedPairingAsync(SsoSetupSession session)
    {
        if (!await AdoptionApplicableAsync(session))
        {
            return;
        }
        var report = ForRegistrationKind(await _checker.CheckAsync(
            new OidcCheckRequest(session.Authority!, session.ClientId, Secret(session), session.Scopes, session.RedirectUri, session.RequirePar),
            OidcCheckScope.Full, CancellationToken.None), session);
        session.Checks = report;
        session.ChecksFingerprint = Fingerprint(session);
        await AdoptIfClientPassedAsync(session, report);
        _sessions.Put(session);
    }

    /// <summary>Called by the registration callback: stores the code or the error in the session.</summary>
    public void AcceptRegistrationReturn(SsoSetupSession session, string state, string? code, string? error, string? errorDescription)
    {
        if (!string.Equals(session.PendingRegistrationState, state, StringComparison.Ordinal))
        {
            session.RegistrationError = "state_mismatch";
            session.RegistrationErrorDescription = "The connection does not belong to this setup. Connect again.";
        }
        else if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            session.RegistrationError = error ?? "registration_failed";
            session.RegistrationErrorDescription = error switch
            {
                "access_denied" => "The connection was cancelled at the provider.",
                "limit_reached" => "Your account at the provider has reached the maximum number of connected installations. Remove one there, then connect again.",
                _ => errorDescription ?? "The provider did not complete the connection."
            };
            session.PendingRegistrationCode = null;
        }
        else
        {
            session.PendingRegistrationCode = code;
            session.RegistrationError = null;
            session.RegistrationErrorDescription = null;
        }
        _sessions.Put(session);
    }

    /// <summary>
    /// A paired provider has no secret anybody could copy: rejected credentials mean the
    /// connection was removed at the provider, and the way out is Reconnect (design frame 176:4851).
    /// </summary>
    internal static OidcCheckReport ForRegistrationKind(OidcCheckReport report, SsoSetupSession session)
    {
        if (session.RegistrationKind != RegistrationKinds.Pairing)
        {
            return report;
        }
        var name = string.IsNullOrWhiteSpace(session.DisplayName) ? "The provider" : session.DisplayName;
        var items = report.Items
            .Select(i => i.Code == OidcErrorCodes.InvalidClient
                ? i with
                {
                    Detail = $"{name} answered invalid_client. The connection was probably removed in {name}. " +
                             "Reconnect to connect this installation again."
                }
                : i)
            .ToList();
        return report with { Items = items };
    }

    /// <summary>Step "Test": all checks without sign-in (E12).</summary>
    public async Task<OidcCheckReport> RunChecksAsync(SsoSetupSession session, CancellationToken ct)
    {
        EnsureConnection(session);
        var report = ForRegistrationKind(await _checker.CheckAsync(
            new OidcCheckRequest(session.Authority!, session.ClientId, Secret(session), session.Scopes, session.RedirectUri, session.RequirePar),
            OidcCheckScope.Full, ct), session);

        session.Checks = report;
        session.ChecksFingerprint = Fingerprint(session);
        if (await AdoptionApplicableAsync(session))
        {
            // A Reconnect whose first check failed (timeout, 5xx) is taken over by a later passing one.
            await AdoptIfClientPassedAsync(session, report);
        }
        _sessions.Put(session);

        // The provider's last result describes its own connection, not one waiting in this session.
        var existingForResult = session.IsNew ? null : await _settings.GetByNameAsync(session.ExistingProvider!, ct);
        if (existingForResult != null && OidcConnectionFingerprint.Compute(existingForResult) == Fingerprint(session))
        {
            await _settings.RecordResultAsync(session.ExistingProvider!, new OidcLastResult(
                SystemClock.UtcNow, OidcResultKinds.Checks, report.Passed,
                report.Passed ? "Checks passed" : string.Join("; ", report.Items.Where(i => i.Status == OidcCheckStatus.Failed).Select(i => i.Title))),
                reconnectNeeded: report.Items.Any(i => i.Code == OidcErrorCodes.InvalidClient) && session.RegistrationKind == RegistrationKinds.Pairing ? true : null,
                ct);
        }
        return report;
    }

    /// <summary>Step "Test": starts the test sign-in; possible once the checks passed for the current connection.</summary>
    public async Task<string> StartTestSignInAsync(SsoSetupSession session, HttpContext http, CancellationToken ct)
    {
        EnsureConnection(session);
        if (!ChecksPassedForCurrentConnection(session))
        {
            throw new SsoSetupException("checks_required", "Run the checks first; the test sign-in is available when all checks pass.", StatusCodes.Status409Conflict);
        }

        var snapshot = ToProviderSettings(session);
        var state = SsoTokens.NewToken();
        var nonce = SsoTokens.NewToken();
        var verifier = SsoTokens.NewToken();
        var result = await _oidc.BuildAuthorizeUrlAsync(snapshot, session.RedirectUri!, state, nonce, SsoTokens.PkceChallenge(verifier), ct);
        if (!result.Succeeded)
        {
            throw new SsoSetupException(result.Error!, result.ErrorDescription ?? "The test sign-in could not be started.", StatusCodes.Status502BadGateway);
        }

        var secret = SsoFlowCookie.Ensure(http, SsoFlowStore.Lifetime);
        _flows.PutOidc(state, new OidcFlowState(session.Name, nonce, verifier, session.RedirectUri!, OidcFlowPurpose.TestSignIn, session.Id, secret, snapshot));
        return result.Url!;
    }

    /// <summary>OIDC callback of a test sign-in: stores the result in the session, returns the UI address to go back to.</summary>
    public async Task<string> CompleteTestSignInAsync(OidcFlowState flow, HttpContext http, string? code, string? error, string baseUrl, CancellationToken ct)
    {
        var session = flow.ContextId == null ? null : _sessions.GetForCallback(flow.ContextId);
        if (session == null || flow.Snapshot == null || !SsoFlowCookie.Matches(http, flow.FlowSecret))
        {
            return $"{baseUrl}/settings/oidc?error=test_sign_in_lost";
        }

        var fingerprint = OidcConnectionFingerprint.Compute(flow.Snapshot);
        var now = SystemClock.UtcNow;

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            session.TestSignIn = TestSignInResult.Failed(fingerprint, now,
                error == "access_denied" ? "The test sign-in was cancelled" : "The test sign-in failed",
                error == "access_denied" ? "The sign-in at the provider was cancelled." : $"The provider answered {error ?? "without a code"}.");
        }
        else
        {
            var exchange = await _oidc.ExchangeCodeAsync(flow.Snapshot, code, flow.RedirectUri, flow.CodeVerifier, flow.Nonce, ct);
            session.TestSignIn = exchange.Succeeded
                ? TestSignInResult.FromUserInfo(exchange.UserInfo!, session.Claims, fingerprint, now)
                : TestSignInResult.Failed(fingerprint, now,
                    exchange.Error == OidcErrorCodes.InvalidClient ? "Client ID and secret rejected" : "The test sign-in failed",
                    exchange.ErrorDescription ?? exchange.Error);
        }
        _sessions.Put(session);

        if (!session.IsNew)
        {
            await ApplyTestedConnectionAsync(session, ct);
        }

        var target = session.IsNew
            ? $"{baseUrl}/settings/oidc/add?session={Uri.EscapeDataString(session.Id)}"
            : $"{baseUrl}/settings/oidc/providers/{Uri.EscapeDataString(session.ExistingProvider!)}?session={Uri.EscapeDataString(session.Id)}";
        return target;
    }

    // ---------------------------------------------------------------- save and remove

    /// <summary>
    /// Step "Save" and "Save changes" (E14). New providers are enabled only with passed checks
    /// and a passed test sign-in for the current connection; otherwise only saved disabled.
    /// </summary>
    public async Task<OidcProviderSettings> SaveAsync(SsoSetupSession session, User actingUser, bool enabled, CancellationToken ct)
    {
        EnsureConnection(session);
        var testedNow = TestPassedForCurrentConnection(session);

        if (session.IsNew)
        {
            if (enabled && !(testedNow && ChecksPassedForCurrentConnection(session)))
            {
                throw new SsoSetupException("test_required",
                    "Run the checks and a test sign-in before you enable this provider. You can save it disabled now.",
                    StatusCodes.Status409Conflict);
            }

            var provider = ToProviderSettings(session);
            provider.Enabled = enabled;
            provider.TestedSignIn = testedNow ? new OidcTestedSignIn(session.TestSignIn!.At, session.TestSignIn.Fingerprint) : null;
            provider.LastResult = session.TestSignIn != null
                ? new OidcLastResult(session.TestSignIn.At, OidcResultKinds.TestSignIn, session.TestSignIn.Passed, session.TestSignIn.Error)
                : session.Checks != null
                    ? new OidcLastResult(SystemClock.UtcNow, OidcResultKinds.Checks, session.Checks.Passed, null)
                    : null;
            try
            {
                await _settings.AddAsync(provider, ct);
            }
            catch (InvalidOperationException ex)
            {
                throw new SsoSetupException("name_taken", ex.Message, StatusCodes.Status409Conflict);
            }
            _sessions.Remove(session.Id);
            return provider;
        }

        var existing = await _settings.GetByNameAsync(session.ExistingProvider!, ct)
            ?? throw new SsoSetupException("provider_unknown", "This provider does not exist.", StatusCodes.Status404NotFound);

        if (existing.Enabled && !enabled && await IsOnlySignInOfAsync(actingUser, existing.Name, ct))
        {
            throw new SsoSetupException("lockout",
                "You sign in with this provider and have no local password. Set a local password in your profile before you disable it.",
                StatusCodes.Status409Conflict);
        }

        var updated = existing.Clone();
        updated.DisplayName = string.IsNullOrWhiteSpace(session.DisplayName) ? existing.DisplayName : session.DisplayName;
        updated.TrustUnverifiedEmail = session.TrustUnverifiedEmail;

        var connectionChanged = Fingerprint(session) != OidcConnectionFingerprint.Compute(existing);
        if (connectionChanged && ChangedElsewhere(session, existing))
        {
            throw new SsoSetupException("provider_changed",
                "The connection of this provider was changed elsewhere in the meantime, for example by a Reconnect in another tab. Open the provider again.",
                StatusCodes.Status409Conflict);
        }
        if (connectionChanged)
        {
            if (!existing.Enabled || !enabled)
            {
                // Disabled providers (also one disabled with this save) take every change right away.
                ApplyConnection(updated, session);
            }
            else if (testedNow)
            {
                ApplyConnection(updated, session);
                updated.ReconnectNeeded = false;
            }
            // Enabled and not tested: the old connection stays active (E14).
        }

        if (enabled && !existing.Enabled)
        {
            var tested = testedNow || updated.HasPassedTestSignInForCurrentConnection;
            if (!tested)
            {
                throw new SsoSetupException("test_required",
                    "Run the checks and a test sign-in before you enable this provider.",
                    StatusCodes.Status409Conflict);
            }
        }
        updated.Enabled = enabled;

        if (testedNow)
        {
            updated.TestedSignIn = new OidcTestedSignIn(session.TestSignIn!.At, session.TestSignIn.Fingerprint);
        }

        await _settings.UpdateAsync(updated, ct);
        _sessions.Remove(session.Id);
        return updated;
    }

    /// <summary>
    /// Removes a provider and all account links to it (E21). Refused (409) if it is the acting
    /// user's only way to sign in.
    /// </summary>
    public async Task RemoveProviderAsync(string name, User actingUser, CancellationToken ct)
    {
        var provider = await _settings.GetByNameAsync(name, ct)
            ?? throw new SsoSetupException("provider_unknown", "This provider does not exist.", StatusCodes.Status404NotFound);

        if (provider.Enabled && await IsOnlySignInOfAsync(actingUser, provider.Name, ct))
        {
            throw new SsoSetupException("lockout",
                "You sign in with this provider and have no local password. Set a local password in your profile before you remove it.",
                StatusCodes.Status409Conflict);
        }

        await _settings.RemoveAsync(provider.Name, ct);
        var removed = _users.RemoveExternalIdentitiesOfProvider(provider.Name);
        _logger.LogInformation("OIDC provider {Provider} removed with {Links} account links", provider.Name, removed);
    }

    /// <summary>
    /// True if <paramref name="user"/> has no local password and <paramref name="providerName"/>
    /// is the only enabled provider the user is linked to.
    /// </summary>
    public async Task<bool> IsOnlySignInOfAsync(User user, string providerName, CancellationToken ct)
    {
        if (user.HasPassword || user.FindExternalIdentity(providerName) == null)
        {
            return false;
        }

        var providers = await _settings.GetAllAsync(ct);
        var otherEnabledLinked = providers.Any(p =>
            p.Enabled &&
            !string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase) &&
            user.FindExternalIdentity(p.Name) != null);
        return !otherEnabledLinked;
    }

    // ---------------------------------------------------------------- helpers

    public bool ChecksPassedForCurrentConnection(SsoSetupSession session) =>
        session.Checks is { Passed: true } && session.ChecksFingerprint == Fingerprint(session);

    public bool TestPassedForCurrentConnection(SsoSetupSession session) =>
        session.TestSignIn is { Passed: true } && session.TestSignIn.Fingerprint == Fingerprint(session);

    public string Fingerprint(SsoSetupSession session) =>
        OidcConnectionFingerprint.Compute(session.Authority ?? string.Empty, session.ClientId ?? string.Empty, Secret(session), session.Scopes);

    public IClientRegistrationMethod Method(string kind) =>
        _registrationMethods.FirstOrDefault(m => m.Kind == kind)
        ?? throw new SsoSetupException("registration_unknown", $"Registration kind '{kind}' is not available.");

    private async Task ApplyTestedConnectionAsync(SsoSetupSession session, CancellationToken ct)
    {
        var existing = await _settings.GetByNameAsync(session.ExistingProvider!, ct);
        if (existing == null)
        {
            return;
        }

        var result = session.TestSignIn!;
        var ownConnection = OidcConnectionFingerprint.Compute(existing) == Fingerprint(session);
        var takeOver = TestPassedForCurrentConnection(session) && !ChangedElsewhere(session, existing);
        if (!ownConnection && !takeOver)
        {
            // The tested connection is not the provider's and does not become it (a failed test of a
            // changed connection, or one changed elsewhere meanwhile): its result is not the provider's.
            return;
        }
        existing.LastResult = new OidcLastResult(result.At, OidcResultKinds.TestSignIn, result.Passed, result.Error);
        if (takeOver)
        {
            // A passed test sign-in with a new connection (reconnect, changed settings) takes it over,
            // unless the provider's connection was changed elsewhere since this session started.
            // (The session's own takeover below moves its start fingerprint along.)
            ApplyConnection(existing, session);
            existing.TestedSignIn = new OidcTestedSignIn(result.At, result.Fingerprint);
            existing.ReconnectNeeded = false;
            session.ProviderFingerprintAtStart = OidcConnectionFingerprint.Compute(existing);
        }
        await _settings.UpdateAsync(existing, ct);
    }

    /// <summary>The session holds a connection the existing provider has not taken over (yet).</summary>
    public async Task<bool> ConnectionPendingAsync(SsoSetupSession session, CancellationToken ct)
    {
        if (session.IsNew || session.ExistingProvider == null)
        {
            return false;
        }
        var existing = await _settings.GetByNameAsync(session.ExistingProvider, ct);
        return existing != null && OidcConnectionFingerprint.Compute(existing) != Fingerprint(session);
    }

    /// <summary>The provider's connection was changed elsewhere since this session started (save answers 409).</summary>
    public async Task<bool> ProviderChangedElsewhereAsync(SsoSetupSession session, CancellationToken ct)
    {
        if (session.IsNew || session.ExistingProvider == null)
        {
            return false;
        }
        var existing = await _settings.GetByNameAsync(session.ExistingProvider, ct);
        return existing != null && ChangedElsewhere(session, existing);
    }

    private static bool ChangedElsewhere(SsoSetupSession session, OidcProviderSettings existing) =>
        session.ProviderFingerprintAtStart != null &&
        session.ProviderFingerprintAtStart != OidcConnectionFingerprint.Compute(existing);

    private void ApplyConnection(OidcProviderSettings target, SsoSetupSession session)
    {
        target.Authority = session.Authority!;
        target.ClientId = session.ClientId!;
        target.ClientSecret = Secret(session);
        target.Scopes = session.Scopes;
        target.PairedAt = session.PairedAt;
        target.PairedBy = session.PairedBy;
    }

    private OidcProviderSettings ToProviderSettings(SsoSetupSession session) => new()
    {
        Name = session.Name,
        DisplayName = string.IsNullOrWhiteSpace(session.DisplayName) ? session.Name : session.DisplayName,
        Authority = session.Authority!,
        ClientId = session.ClientId!,
        ClientSecret = Secret(session),
        Scopes = session.Scopes,
        Template = session.TemplateId,
        Registration = session.RegistrationKind,
        RequirePar = session.RequirePar,
        Claims = session.Claims,
        PairedAt = session.PairedAt,
        PairedBy = session.PairedBy,
        TrustUnverifiedEmail = session.TrustUnverifiedEmail
    };

    private RegistrationContext RegistrationContext(SsoSetupSession session, string state) => new(
        session.Authority!,
        session.Name,
        InstallationName(session.BaseUrl!),
        session.BaseUrl!,
        session.RedirectUri!,
        session.BaseUrl!.TrimEnd('/') + RegistrationCallbackPath,
        state);

    internal static string InstallationName(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? $"ReadyStackGo ({uri.Authority})" : "ReadyStackGo";

    private void ApplyRegisteredClient(SsoSetupSession session, RegisteredClient client)
    {
        session.ClientId = client.ClientId;
        session.EncryptedClientSecret = _encryption.Encrypt(client.ClientSecret);
        session.PairedAt = client.RegisteredAt;
        session.PairedBy = client.ConfirmedBy;
        session.RegisteredInSession = true;
        session.AdoptedAfterPairing = false;
        session.RegistrationError = null;
        session.RegistrationErrorDescription = null;
        if (!string.IsNullOrEmpty(client.Issuer) && session.Authority == null)
        {
            session.Authority = client.Issuer;
        }
        InvalidateConnectionResults(session);
    }

    private string? Secret(SsoSetupSession session) =>
        string.IsNullOrEmpty(session.EncryptedClientSecret) ? null : _encryption.Decrypt(session.EncryptedClientSecret);

    private static void InvalidateConnectionResults(SsoSetupSession session)
    {
        session.Checks = null;
        session.ChecksFingerprint = null;
        session.TestSignIn = null;
    }

    private static void EnsureReadyForRegistration(SsoSetupSession session)
    {
        if (string.IsNullOrEmpty(session.Authority))
        {
            throw new SsoSetupException("authority_missing", "Enter the provider address first.");
        }
        if (string.IsNullOrEmpty(session.BaseUrl))
        {
            throw new SsoSetupException("base_url_missing", "Confirm the address of this installation first.");
        }
        if (session.RequireHttps && !BaseUrlRules.SatisfiesHttpsRequirement(session.BaseUrl))
        {
            throw new SsoSetupException(WizardSsoService.HttpsRequired,
                "This provider only connects installations that use HTTPS. http:// is allowed for localhost only.");
        }
    }

    private static void EnsureConnection(SsoSetupSession session)
    {
        EnsureReadyForRegistration(session);
        if (!session.HasCredentials)
        {
            throw new SsoSetupException("credentials_missing", "Register ReadyStackGo at the provider first.");
        }
    }

    private static string SuggestName(string preferred, IReadOnlyList<OidcProviderSettings> providers)
    {
        var taken = providers.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(preferred))
        {
            return preferred;
        }
        for (var i = 2; ; i++)
        {
            var candidate = $"{preferred}-{i}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}

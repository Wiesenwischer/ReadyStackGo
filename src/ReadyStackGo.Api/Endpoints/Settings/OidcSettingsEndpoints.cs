using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.API.Endpoints.Sso;
using ReadyStackGo.Api.Endpoints.User;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.API.Endpoints.Settings;

/// <summary>
/// OIDC provider as listed under Settings › Single Sign-On. Secrets are never returned.
/// </summary>
public class OidcProviderSettingsDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public bool HasClientSecret { get; set; }
    public string Scopes { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Template { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Registration { get; set; } = string.Empty;
    public bool IsPaired { get; set; }
    public DateTime? PairedAt { get; set; }
    public string? PairedBy { get; set; }
    public bool TrustUnverifiedEmail { get; set; }
    public bool ReconnectNeeded { get; set; }
    public OidcLastResult? LastResult { get; set; }
    public bool HasPassedTestSignIn { get; set; }

    /// <summary>The current user signs in only through this provider (cannot disable or remove it).</summary>
    public bool IsOnlySignInOfCurrentUser { get; set; }
}

public class OidcSettingsDto
{
    public List<OidcProviderSettingsDto> Providers { get; set; } = new();

    /// <summary>True while no system administrator has a local password.</summary>
    public bool NoAdminWithPassword { get; set; }

    /// <summary>Number of system administrators (for the wording of the warning).</summary>
    public int SystemAdminCount { get; set; }
}

/// <summary>GET /api/settings/oidc — configured OIDC providers with status (secrets never returned).</summary>
[RequireSystemAdmin]
public class GetOidcSettingsEndpoint : EndpointWithoutRequest<OidcSettingsDto>
{
    private readonly IOidcSettingsService _settings;
    private readonly IIdentityProviderTemplateCatalog _templates;
    private readonly IUserRepository _users;
    private readonly SsoSetupService _setup;

    public GetOidcSettingsEndpoint(IOidcSettingsService settings, IIdentityProviderTemplateCatalog templates, IUserRepository users, SsoSetupService setup)
    {
        _settings = settings;
        _templates = templates;
        _users = users;
        _setup = setup;
    }

    public override void Configure()
    {
        Get("/api/settings/oidc");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var providers = await _settings.GetAllAsync(ct);
        var currentUser = ListExternalIdentitiesEndpoint.CurrentUser(_users, HttpContext);
        var admins = _users.GetAll().Where(u => u.HasRole(RoleId.SystemAdmin)).ToList();

        var list = new List<OidcProviderSettingsDto>();
        foreach (var p in providers)
        {
            var template = _templates.GetTemplate(p.Template);
            list.Add(new OidcProviderSettingsDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Authority = p.Authority,
                ClientId = p.ClientId,
                HasClientSecret = !string.IsNullOrEmpty(p.ClientSecret),
                Scopes = p.Scopes,
                Enabled = p.Enabled,
                Template = p.Template,
                TemplateName = template?.Name ?? (p.Template == OidcProviderSettings.GenericTemplateId ? "Generic OIDC" : p.Template),
                IconUrl = template is { HasIcon: true } ? $"/api/identity-provider-templates/{template.Id}/icon" : null,
                Registration = p.Registration,
                IsPaired = p.IsPaired,
                PairedAt = p.PairedAt,
                PairedBy = p.PairedBy,
                TrustUnverifiedEmail = p.TrustUnverifiedEmail,
                ReconnectNeeded = p.ReconnectNeeded,
                LastResult = p.LastResult,
                HasPassedTestSignIn = p.HasPassedTestSignInForCurrentConnection,
                IsOnlySignInOfCurrentUser = currentUser != null && p.Enabled && await _setup.IsOnlySignInOfAsync(currentUser, p.Name, ct)
            });
        }

        Response = new OidcSettingsDto
        {
            Providers = list,
            SystemAdminCount = admins.Count,
            NoAdminWithPassword = admins.Count > 0 && admins.All(a => !a.HasPassword)
        };
    }
}

/// <summary>GET /api/settings/oidc/templates — the offered provider templates.</summary>
[RequireSystemAdmin]
public class GetOidcTemplatesEndpoint : EndpointWithoutRequest<List<IdentityProviderTemplateDto>>
{
    private readonly IIdentityProviderTemplateCatalog _templates;
    private readonly IEnumerable<IClientRegistrationMethod> _methods;

    public GetOidcTemplatesEndpoint(IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
    {
        _templates = templates;
        _methods = methods;
    }

    public override void Configure()
    {
        Get("/api/settings/oidc/templates");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response = _templates.GetTemplates().Select(t => IdentityProviderTemplateDto.From(t, _methods)).ToList();
        return Task.CompletedTask;
    }
}

public class RemoveOidcProviderRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// DELETE /api/settings/oidc/providers/{name} — removes a provider and its account links.
/// 409 if it is the current user's only way to sign in.
/// </summary>
[RequireSystemAdmin]
public class RemoveOidcProviderEndpoint : Endpoint<RemoveOidcProviderRequest>
{
    private readonly SsoSetupService _setup;
    private readonly IUserRepository _users;

    public RemoveOidcProviderEndpoint(SsoSetupService setup, IUserRepository users)
    {
        _setup = setup;
        _users = users;
    }

    public override void Configure()
    {
        Delete("/api/settings/oidc/providers/{name}");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<RemoveOidcProviderRequest>>();
    }

    public override async Task HandleAsync(RemoveOidcProviderRequest req, CancellationToken ct)
    {
        var user = ListExternalIdentitiesEndpoint.CurrentUser(_users, HttpContext);
        if (user == null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        try
        {
            await _setup.RemoveProviderAsync(req.Name, user, ct);
            await Send.NoContentAsync(ct);
        }
        catch (SsoSetupException ex)
        {
            await SsoResponses.SendErrorAsync(HttpContext, ex, ct);
        }
    }
}

// ------------------------------------------------------------------ setup sessions

public class CreateSetupSessionRequest
{
    /// <summary>Template of a new provider ("Add provider").</summary>
    public string? TemplateId { get; set; }

    /// <summary>Existing provider to test, reconnect or change.</summary>
    public string? Provider { get; set; }
}

public class SetupSessionRequest
{
    public string Id { get; set; } = string.Empty;
}

public class UpdateSetupSessionRequest : SetupSessionRequest
{
    public string? DisplayName { get; set; }
    public bool? TrustUnverifiedEmail { get; set; }
}

public class DiscoveryRequest : SetupSessionRequest
{
    public string? Authority { get; set; }
}

public class InstallationRequest : SetupSessionRequest
{
    public string? BaseUrl { get; set; }
    public string? Name { get; set; }
}

public class RegistrationRequest : SetupSessionRequest
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scopes { get; set; }
}

public class SaveSetupRequest : SetupSessionRequest
{
    public bool Enabled { get; set; }
}

public class TestSignInStartDto
{
    public string Url { get; set; } = string.Empty;
}

/// <summary>Shared handling of setup session endpoints: current user, session lookup, errors.</summary>
public abstract class SetupSessionEndpointBase<TRequest> : Endpoint<TRequest> where TRequest : notnull
{
    protected SetupSessionEndpointBase(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
    {
        Setup = setup;
        Users = users;
        SystemConfig = systemConfig;
        Templates = templates;
        Methods = methods;
    }

    protected SsoSetupService Setup { get; }
    protected IUserRepository Users { get; }
    protected ISystemConfigService SystemConfig { get; }
    protected IIdentityProviderTemplateCatalog Templates { get; }
    protected IEnumerable<IClientRegistrationMethod> Methods { get; }

    protected abstract Task<object?> RunAsync(TRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct);

    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        var user = ListExternalIdentitiesEndpoint.CurrentUser(Users, HttpContext);
        if (user == null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        try
        {
            var result = await RunAsync(req, user, ct);
            if (result == null)
            {
                await Send.NoContentAsync(ct);
            }
            else
            {
                await HttpContext.Response.SendAsync(result, StatusCodes.Status200OK, cancellation: ct);
            }
        }
        catch (SsoSetupException ex)
        {
            await SsoResponses.SendErrorAsync(HttpContext, ex, ct);
        }
    }

    protected async Task<SetupSessionDto> ToDtoAsync(SsoSetupSession session)
    {
        var template = Templates.GetTemplate(session.TemplateId);
        var configuredBaseUrl = await SystemConfig.GetConfiguredBaseUrlAsync();
        var interaction = Methods.FirstOrDefault(m => m.Kind == session.RegistrationKind)?.Interaction;
        var testCurrent = Setup.TestPassedForCurrentConnection(session) ||
                          (session.TestSignIn != null && session.TestSignIn.Fingerprint == Setup.Fingerprint(session));
        return new SetupSessionDto
        {
            Id = session.Id,
            IsNew = session.IsNew,
            ExistingProvider = session.ExistingProvider,
            Template = template == null ? null : IdentityProviderTemplateDto.From(template, Methods),
            TemplateId = session.TemplateId,
            RegistrationKind = session.RegistrationKind,
            Interaction = IdentityProviderTemplateDto.InteractionName(interaction),
            RequireHttps = session.RequireHttps,
            Authority = session.Authority,
            Name = session.Name,
            DisplayName = session.DisplayName,
            Scopes = session.Scopes,
            BaseUrl = session.BaseUrl,
            BaseUrlConfigured = configuredBaseUrl != null,
            RedirectUri = session.RedirectUri,
            ClientId = session.ClientId,
            HasClientSecret = !string.IsNullOrEmpty(session.EncryptedClientSecret),
            PairedAt = session.PairedAt,
            PairedBy = session.PairedBy,
            RegistrationError = session.RegistrationError,
            RegistrationErrorDescription = session.RegistrationErrorDescription,
            RegistrationPending = !string.IsNullOrEmpty(session.PendingRegistrationCode),
            Discovery = CheckReportDto.From(session.Discovery),
            Checks = CheckReportDto.From(session.Checks, session.ChecksFingerprint == Setup.Fingerprint(session)),
            TestSignIn = session.TestSignIn == null ? null : new TestSignInDto
            {
                Passed = session.TestSignIn.Passed,
                Current = testCurrent,
                At = session.TestSignIn.At,
                SignedInAs = session.TestSignIn.SignedInAs,
                Claims = session.TestSignIn.Claims.ToList(),
                WarningTitle = session.TestSignIn.WarningTitle,
                WarningBody = session.TestSignIn.WarningBody,
                Error = session.TestSignIn.Error,
                ErrorDetail = session.TestSignIn.ErrorDetail
            },
            TrustUnverifiedEmail = session.TrustUnverifiedEmail,
            CanEnable = Setup.ChecksPassedForCurrentConnection(session) && Setup.TestPassedForCurrentConnection(session)
        };
    }
}

/// <summary>POST /api/settings/oidc/setup — starts "Add provider" (templateId) or a session on an existing provider.</summary>
[RequireSystemAdmin]
public class CreateSetupSessionEndpoint : SetupSessionEndpointBase<CreateSetupSessionRequest>
{
    public CreateSetupSessionEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<CreateSetupSessionRequest>>();
    }

    protected override async Task<object?> RunAsync(CreateSetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = !string.IsNullOrWhiteSpace(req.Provider)
            ? await Setup.CreateForProviderAsync(user.Id.Value, req.Provider, ct)
            : await Setup.CreateForTemplateAsync(user.Id.Value, req.TemplateId ?? string.Empty, ct);
        return await ToDtoAsync(session);
    }
}

/// <summary>GET /api/settings/oidc/setup/{id}</summary>
[RequireSystemAdmin]
public class GetSetupSessionEndpoint : SetupSessionEndpointBase<SetupSessionRequest>
{
    public GetSetupSessionEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Get("/api/settings/oidc/setup/{id}");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SetupSessionRequest>>();
    }

    protected override async Task<object?> RunAsync(SetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct) =>
        await ToDtoAsync(Setup.Get(req.Id, user.Id.Value));
}

/// <summary>PATCH /api/settings/oidc/setup/{id} — display name and "Trust unverified email addresses".</summary>
[RequireSystemAdmin]
public class UpdateSetupSessionEndpoint : SetupSessionEndpointBase<UpdateSetupSessionRequest>
{
    public UpdateSetupSessionEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Patch("/api/settings/oidc/setup/{id}");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<UpdateSetupSessionRequest>>();
    }

    protected override async Task<object?> RunAsync(UpdateSetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        Setup.Update(session, req.DisplayName, req.TrustUnverifiedEmail);
        return await ToDtoAsync(session);
    }
}

/// <summary>DELETE /api/settings/oidc/setup/{id} — "Cancel": drops the session without traces.</summary>
[RequireSystemAdmin]
public class CancelSetupSessionEndpoint : SetupSessionEndpointBase<SetupSessionRequest>
{
    public CancelSetupSessionEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Delete("/api/settings/oidc/setup/{id}");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SetupSessionRequest>>();
    }

    protected override Task<object?> RunAsync(SetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        Setup.Cancel(req.Id, user.Id.Value);
        return Task.FromResult<object?>(null);
    }
}

/// <summary>POST /api/settings/oidc/setup/{id}/discovery — step "Provider address" (checks 1–3).</summary>
[RequireSystemAdmin]
public class SetupDiscoveryEndpoint : SetupSessionEndpointBase<DiscoveryRequest>
{
    public SetupDiscoveryEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/discovery");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<DiscoveryRequest>>();
    }

    protected override async Task<object?> RunAsync(DiscoveryRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        await Setup.CheckDiscoveryAsync(session, req.Authority, ct);
        return await ToDtoAsync(session);
    }
}

/// <summary>POST /api/settings/oidc/setup/{id}/installation — step "This installation" (base URL, name).</summary>
[RequireSystemAdmin]
public class SetupInstallationEndpoint : SetupSessionEndpointBase<InstallationRequest>
{
    public SetupInstallationEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/installation");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<InstallationRequest>>();
    }

    protected override async Task<object?> RunAsync(InstallationRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        await Setup.SetInstallationAsync(session, req.BaseUrl, req.Name, ct);
        return await ToDtoAsync(session);
    }
}

/// <summary>
/// POST /api/settings/oidc/setup/{id}/registration — manual: stores client ID, secret and scopes;
/// pairing: starts the browser round trip (returns the form to post).
/// </summary>
[RequireSystemAdmin]
public class SetupRegistrationEndpoint : SetupSessionEndpointBase<RegistrationRequest>
{
    public SetupRegistrationEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/registration");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<RegistrationRequest>>();
    }

    protected override async Task<object?> RunAsync(RegistrationRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        if (Setup.Method(session.RegistrationKind).Interaction == RegistrationInteraction.ManualEntry)
        {
            Setup.SetManualCredentials(session, req.ClientId, req.ClientSecret, req.Scopes);
            return new { session = await ToDtoAsync(session) };
        }

        var start = await Setup.StartRegistrationAsync(session, HttpContext, ct);
        return new { session = await ToDtoAsync(session), start = RegistrationStartDto.From(start) };
    }
}

/// <summary>POST /api/settings/oidc/setup/{id}/registration/complete — redeems the pairing code.</summary>
[RequireSystemAdmin]
public class SetupRegistrationCompleteEndpoint : SetupSessionEndpointBase<SetupSessionRequest>
{
    public SetupRegistrationCompleteEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/registration/complete");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SetupSessionRequest>>();
    }

    protected override async Task<object?> RunAsync(SetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        await Setup.CompleteRegistrationAsync(session, ct);
        return await ToDtoAsync(session);
    }
}

/// <summary>POST /api/settings/oidc/setup/{id}/checks — all checks without sign-in.</summary>
[RequireSystemAdmin]
public class SetupChecksEndpoint : SetupSessionEndpointBase<SetupSessionRequest>
{
    public SetupChecksEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/checks");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SetupSessionRequest>>();
    }

    protected override async Task<object?> RunAsync(SetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        await Setup.RunChecksAsync(session, ct);
        return await ToDtoAsync(session);
    }
}

/// <summary>POST /api/settings/oidc/setup/{id}/test-sign-in — returns the provider URL for the test sign-in.</summary>
[RequireSystemAdmin]
public class SetupTestSignInEndpoint : SetupSessionEndpointBase<SetupSessionRequest>
{
    public SetupTestSignInEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/test-sign-in");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SetupSessionRequest>>();
    }

    protected override async Task<object?> RunAsync(SetupSessionRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        return new TestSignInStartDto { Url = await Setup.StartTestSignInAsync(session, HttpContext, ct) };
    }
}

/// <summary>POST /api/settings/oidc/setup/{id}/save — "Save provider" / "Save disabled" / "Save changes".</summary>
[RequireSystemAdmin]
public class SetupSaveEndpoint : SetupSessionEndpointBase<SaveSetupRequest>
{
    public SetupSaveEndpoint(SsoSetupService setup, IUserRepository users, ISystemConfigService systemConfig,
        IIdentityProviderTemplateCatalog templates, IEnumerable<IClientRegistrationMethod> methods)
        : base(setup, users, systemConfig, templates, methods) { }

    public override void Configure()
    {
        Post("/api/settings/oidc/setup/{id}/save");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SaveSetupRequest>>();
    }

    protected override async Task<object?> RunAsync(SaveSetupRequest req, Domain.IdentityAccess.Users.User user, CancellationToken ct)
    {
        var session = Setup.Get(req.Id, user.Id.Value);
        var saved = await Setup.SaveAsync(session, user, req.Enabled, ct);
        return new { name = saved.Name, enabled = saved.Enabled };
    }
}

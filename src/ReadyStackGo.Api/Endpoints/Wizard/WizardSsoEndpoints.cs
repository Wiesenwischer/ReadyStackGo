using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.API.Endpoints.Sso;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;

namespace ReadyStackGo.API.Endpoints.Wizard;

public class WizardSsoRunDto
{
    /// <summary>"started", "registered", "signedIn" or "failed".</summary>
    public string State { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? FailureDetail { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? ProviderName { get; set; }

    /// <summary>The pairing came back with a code that "continue" redeems.</summary>
    public bool RegistrationReturned { get; set; }
    public string? SignedInUsername { get; set; }
    public string? SignedInEmail { get; set; }
    public string? SignedInDisplayName { get; set; }

    public static WizardSsoRunDto From(WizardSsoRun run, IIdentityProviderTemplateCatalog templates)
    {
        var template = templates.GetTemplate(run.TemplateId);
        return new WizardSsoRunDto
        {
            State = run.State switch
            {
                WizardSsoRunState.Started => "started",
                WizardSsoRunState.Registered => "registered",
                WizardSsoRunState.SignedIn => "signedIn",
                _ => "failed"
            },
            FailureReason = run.FailureReason,
            FailureDetail = run.FailureDetail,
            StartedAt = run.StartedAt,
            ExpiresAt = run.ExpiresAt,
            TemplateId = run.TemplateId,
            TemplateName = template?.Name ?? run.TemplateId,
            IconUrl = template is { HasIcon: true } ? $"/api/identity-provider-templates/{template.Id}/icon" : null,
            BaseUrl = run.BaseUrl,
            ProviderName = run.ProviderName,
            RegistrationReturned = !string.IsNullOrEmpty(run.PendingRegistrationCode),
            SignedInUsername = run.SignedInUsername,
            SignedInEmail = run.SignedInEmail,
            SignedInDisplayName = run.SignedInDisplayName
        };
    }
}

public class WizardSsoStartRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
}

public class WizardSsoStartResponse
{
    public WizardSsoRunDto Run { get; set; } = new();
    public RegistrationStartDto? Start { get; set; }

    /// <summary>Retry continues with the sign-in: the browser navigates to /api/wizard/sso/sign-in.</summary>
    public bool SignIn { get; set; }
}

/// <summary>GET /api/wizard/sso/templates — sign-in methods offered in the first wizard step. Anonymous.</summary>
public class WizardSsoTemplatesEndpoint : EndpointWithoutRequest<List<IdentityProviderTemplateDto>>
{
    private readonly WizardSsoService _wizard;
    private readonly IEnumerable<IClientRegistrationMethod> _methods;

    public WizardSsoTemplatesEndpoint(WizardSsoService wizard, IEnumerable<IClientRegistrationMethod> methods)
    {
        _wizard = wizard;
        _methods = methods;
    }

    public override void Configure()
    {
        Get("/api/wizard/sso/templates");
        AllowAnonymous();
        Description(b => b.WithTags("Wizard"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response = _wizard.OfferedTemplates().Select(t => IdentityProviderTemplateDto.From(t, _methods)).ToList();
        return Task.CompletedTask;
    }
}

/// <summary>
/// POST /api/wizard/sso/start — starts a wizard run with a provider template (inside the setup
/// window, no system administrator yet) and returns the pairing form to post. Anonymous.
/// </summary>
public class WizardSsoStartEndpoint : Endpoint<WizardSsoStartRequest>
{
    private readonly WizardSsoService _wizard;
    private readonly IIdentityProviderTemplateCatalog _templates;

    public WizardSsoStartEndpoint(WizardSsoService wizard, IIdentityProviderTemplateCatalog templates)
    {
        _wizard = wizard;
        _templates = templates;
    }

    public override void Configure()
    {
        Post("/api/wizard/sso/start");
        AllowAnonymous();
        PreProcessor<WizardTimeoutPreProcessor<WizardSsoStartRequest>>();
        Description(b => b.WithTags("Wizard"));
    }

    public override async Task HandleAsync(WizardSsoStartRequest req, CancellationToken ct)
    {
        try
        {
            var (run, start) = await _wizard.StartAsync(HttpContext, req.TemplateId, req.BaseUrl, ct);
            await HttpContext.Response.SendAsync(new WizardSsoStartResponse
            {
                Run = WizardSsoRunDto.From(run, _templates),
                Start = RegistrationStartDto.From(start)
            }, StatusCodes.Status200OK, cancellation: ct);
        }
        catch (SsoSetupException ex)
        {
            await SsoResponses.SendErrorAsync(HttpContext, ex, ct);
        }
    }
}

/// <summary>Base of the endpoints that act on this browser's wizard run (bound by the flow cookie).</summary>
public abstract class WizardSsoRunEndpointBase : EndpointWithoutRequest
{
    protected WizardSsoRunEndpointBase(WizardSsoService wizard, IIdentityProviderTemplateCatalog templates)
    {
        Wizard = wizard;
        Templates = templates;
    }

    protected WizardSsoService Wizard { get; }
    protected IIdentityProviderTemplateCatalog Templates { get; }

    protected abstract Task<object?> RunAsync(WizardSsoRun run, CancellationToken ct);

    public override async Task HandleAsync(CancellationToken ct)
    {
        var run = Wizard.CurrentRun(HttpContext);
        if (run == null)
        {
            await SsoResponses.SendErrorAsync(HttpContext, "no_run",
                "There is no sign-in run for this browser. Start the setup again in this browser.",
                StatusCodes.Status404NotFound, ct);
            return;
        }

        try
        {
            var result = await RunAsync(run, ct);
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
}

/// <summary>GET /api/wizard/sso/status — state of this browser's run. Anonymous.</summary>
public class WizardSsoStatusEndpoint : WizardSsoRunEndpointBase
{
    public WizardSsoStatusEndpoint(WizardSsoService wizard, IIdentityProviderTemplateCatalog templates) : base(wizard, templates) { }

    public override void Configure()
    {
        Get("/api/wizard/sso/status");
        AllowAnonymous();
        Description(b => b.WithTags("Wizard"));
    }

    protected override Task<object?> RunAsync(WizardSsoRun run, CancellationToken ct) =>
        Task.FromResult<object?>(WizardSsoRunDto.From(run, Templates));
}

/// <summary>POST /api/wizard/sso/continue — redeems the pairing code and saves the provider (disabled). Anonymous.</summary>
public class WizardSsoContinueEndpoint : WizardSsoRunEndpointBase
{
    public WizardSsoContinueEndpoint(WizardSsoService wizard, IIdentityProviderTemplateCatalog templates) : base(wizard, templates) { }

    public override void Configure()
    {
        Post("/api/wizard/sso/continue");
        AllowAnonymous();
        Description(b => b.WithTags("Wizard"));
    }

    protected override async Task<object?> RunAsync(WizardSsoRun run, CancellationToken ct)
    {
        await Wizard.ContinueAsync(run, ct);
        return WizardSsoRunDto.From(run, Templates);
    }
}

/// <summary>
/// GET /api/wizard/sso/sign-in — browser navigation to the provider's sign-in for a registered
/// run (with PAR); back to the wizard if that is not possible. Anonymous.
/// </summary>
public class WizardSsoSignInEndpoint : EndpointWithoutRequest
{
    private readonly WizardSsoService _wizard;
    private readonly ISystemConfigService _systemConfig;

    public WizardSsoSignInEndpoint(WizardSsoService wizard, ISystemConfigService systemConfig)
    {
        _wizard = wizard;
        _systemConfig = systemConfig;
    }

    public override void Configure()
    {
        Get("/api/wizard/sso/sign-in");
        AllowAnonymous();
        Description(b => b.WithTags("Wizard"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var baseUrl = await _systemConfig.GetEffectiveBaseUrlAsync();
        var run = _wizard.CurrentRun(HttpContext);
        if (run == null)
        {
            await Send.RedirectAsync($"{baseUrl}/wizard?sso=foreign", isPermanent: false, allowRemoteRedirects: true);
            return;
        }

        var url = await _wizard.SignInUrlAsync(run, ct);
        await Send.RedirectAsync(url ?? $"{baseUrl}/wizard?sso=returned", isPermanent: false, allowRemoteRedirects: true);
    }
}

/// <summary>POST /api/wizard/sso/retry — "Try again" inside the run. Anonymous.</summary>
public class WizardSsoRetryEndpoint : WizardSsoRunEndpointBase
{
    public WizardSsoRetryEndpoint(WizardSsoService wizard, IIdentityProviderTemplateCatalog templates) : base(wizard, templates) { }

    public override void Configure()
    {
        Post("/api/wizard/sso/retry");
        AllowAnonymous();
        Description(b => b.WithTags("Wizard"));
    }

    protected override async Task<object?> RunAsync(WizardSsoRun run, CancellationToken ct)
    {
        var start = await Wizard.RetryAsync(run, ct);
        return new WizardSsoStartResponse
        {
            Run = WizardSsoRunDto.From(run, Templates),
            Start = start == null ? null : RegistrationStartDto.From(start),
            SignIn = start == null
        };
    }
}

/// <summary>DELETE /api/wizard/sso — "Use built-in sign-in instead": ends the run. Anonymous.</summary>
public class WizardSsoEndEndpoint : WizardSsoRunEndpointBase
{
    public WizardSsoEndEndpoint(WizardSsoService wizard, IIdentityProviderTemplateCatalog templates) : base(wizard, templates) { }

    public override void Configure()
    {
        Delete("/api/wizard/sso");
        AllowAnonymous();
        Description(b => b.WithTags("Wizard"));
    }

    protected override Task<object?> RunAsync(WizardSsoRun run, CancellationToken ct)
    {
        Wizard.End(run);
        return Task.FromResult<object?>(null);
    }
}

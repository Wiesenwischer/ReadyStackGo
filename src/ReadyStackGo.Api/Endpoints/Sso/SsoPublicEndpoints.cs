using System.Security.Cryptography;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.IdentityProviders;

namespace ReadyStackGo.API.Endpoints.Sso;

public class TemplateIconRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/identity-provider-templates/{id}/icon — the SVG icon of a template. Anonymous (the
/// sign-in page shows it). Served with a sandboxing CSP and nosniff; the UI only embeds it
/// through &lt;img&gt;, where scripts do not run.
/// </summary>
public class GetTemplateIconEndpoint : Endpoint<TemplateIconRequest>
{
    private readonly IIdentityProviderTemplateCatalog _templates;

    public GetTemplateIconEndpoint(IIdentityProviderTemplateCatalog templates)
    {
        _templates = templates;
    }

    public override void Configure()
    {
        Get("/api/identity-provider-templates/{id}/icon");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(TemplateIconRequest req, CancellationToken ct)
    {
        var icon = _templates.GetIcon(req.Id);
        if (icon == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(SHA256.HashData(icon))[..32]}\"");
        var response = HttpContext.Response;
        response.Headers.CacheControl = "no-cache";
        response.Headers.ETag = etag.ToString();
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";

        var ifNoneMatch = HttpContext.Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            await Send.ResultAsync(Results.StatusCode(StatusCodes.Status304NotModified));
            return;
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "image/svg+xml";
        response.ContentLength = icon.Length;
        await response.Body.WriteAsync(icon, ct);
    }
}

public class RegistrationCallbackRequest
{
    [QueryParam]
    public string? Code { get; set; }

    [QueryParam]
    public string? State { get; set; }

    [QueryParam]
    public string? Error { get; set; }

    [QueryParam, BindFrom("error_description")]
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// GET /api/sso/registration/callback — the provider returns here after a pairing (code and
/// state, or error and state). Only stores the code in its setup session or wizard run (bound
/// to the browser by the flow cookie) and redirects to the UI, which redeems it. Anonymous.
/// </summary>
public class RegistrationCallbackEndpoint : Endpoint<RegistrationCallbackRequest>
{
    private readonly SsoFlowStore _flows;
    private readonly SsoSetupSessionStore _sessions;
    private readonly SsoSetupService _setup;
    private readonly WizardSsoRunStore _runs;
    private readonly WizardSsoService _wizard;
    private readonly ISystemConfigService _systemConfig;

    public RegistrationCallbackEndpoint(
        SsoFlowStore flows,
        SsoSetupSessionStore sessions,
        SsoSetupService setup,
        WizardSsoRunStore runs,
        WizardSsoService wizard,
        ISystemConfigService systemConfig)
    {
        _flows = flows;
        _sessions = sessions;
        _setup = setup;
        _runs = runs;
        _wizard = wizard;
        _systemConfig = systemConfig;
    }

    public override void Configure()
    {
        Get(SsoSetupService.RegistrationCallbackPath);
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(RegistrationCallbackRequest req, CancellationToken ct)
    {
        var baseUrl = (await _systemConfig.GetBaseUrlAsync()).TrimEnd('/');
        var flow = string.IsNullOrEmpty(req.State) ? null : _flows.TakeRegistration(req.State);

        if (flow == null || !SsoFlowCookie.Matches(HttpContext, flow.FlowSecret))
        {
            // Unknown, expired or foreign: the browser that started the pairing must finish it.
            var target = flow?.Owner == RegistrationFlowOwner.WizardRun ? "/wizard?sso=foreign" : "/settings/oidc?error=registration_lost";
            await Redirect($"{baseUrl}{target}");
            return;
        }

        if (flow.Owner == RegistrationFlowOwner.WizardRun)
        {
            var run = _runs.Get(flow.ContextId);
            if (run != null)
            {
                _wizard.AcceptRegistrationReturn(run, req.State!, req.Code, req.Error);
            }
            await Redirect($"{baseUrl}/wizard?sso=returned");
            return;
        }

        var session = _sessions.GetForCallback(flow.ContextId);
        if (session == null)
        {
            await Redirect($"{baseUrl}/settings/oidc?error=registration_lost");
            return;
        }

        _setup.AcceptRegistrationReturn(session, req.State!, req.Code, req.Error, req.ErrorDescription);
        var page = session.IsNew
            ? $"/settings/oidc/add?session={Uri.EscapeDataString(session.Id)}"
            : $"/settings/oidc/providers/{Uri.EscapeDataString(session.ExistingProvider!)}?session={Uri.EscapeDataString(session.Id)}";
        await Redirect($"{baseUrl}{page}");
    }

    private Task Redirect(string url) => Send.RedirectAsync(url, isPermanent: false, allowRemoteRedirects: true);
}

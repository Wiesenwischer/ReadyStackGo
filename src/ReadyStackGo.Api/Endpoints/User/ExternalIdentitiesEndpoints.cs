using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.User;

public class ExternalIdentityDto
{
    public string Provider { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }

    /// <summary>Display name of the provider (falls back to the provider name if it was removed).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Icon of the provider's template, or null.</summary>
    public string? IconUrl { get; set; }
}

/// <summary>
/// GET /api/user/external-identities - lists the current user's linked OIDC identities.
/// </summary>
public class ListExternalIdentitiesEndpoint : EndpointWithoutRequest<List<ExternalIdentityDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IOidcSettingsService _settings;
    private readonly IIdentityProviderTemplateCatalog _templates;

    public ListExternalIdentitiesEndpoint(IUserRepository userRepository, IOidcSettingsService settings, IIdentityProviderTemplateCatalog templates)
    {
        _userRepository = userRepository;
        _settings = settings;
        _templates = templates;
    }

    public override void Configure()
    {
        Get("/api/user/external-identities");
        Description(b => b.WithTags("User"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = CurrentUser(_userRepository, HttpContext);
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var providers = await _settings.GetAllAsync(ct);
        Response = user.ExternalIdentities
            .Select(e =>
            {
                var provider = providers.FirstOrDefault(p => string.Equals(p.Name, e.Provider, StringComparison.OrdinalIgnoreCase));
                var template = provider == null ? null : _templates.GetTemplate(provider.Template);
                return new ExternalIdentityDto
                {
                    Provider = e.Provider,
                    LinkedAt = e.LinkedAt,
                    DisplayName = string.IsNullOrWhiteSpace(provider?.DisplayName) ? e.Provider : provider.DisplayName,
                    IconUrl = template is { HasIcon: true } ? $"/api/identity-provider-templates/{template.Id}/icon" : null
                };
            })
            .ToList();
    }

    internal static Domain.IdentityAccess.Users.User? CurrentUser(IUserRepository repo, HttpContext ctx)
    {
        var claim = ctx.User.FindFirst(RbacClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var guid))
            return null;
        return repo.Get(new UserId(guid));
    }
}

public class UnlinkExternalIdentityRequest
{
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// DELETE /api/user/external-identities/{provider} - unlinks an OIDC identity from the
/// current user. Refuses to remove the only sign-in method (409).
/// </summary>
public class UnlinkExternalIdentityEndpoint : Endpoint<UnlinkExternalIdentityRequest>
{
    private readonly IUserRepository _userRepository;

    public UnlinkExternalIdentityEndpoint(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public override void Configure()
    {
        Delete("/api/user/external-identities/{provider}");
        Description(b => b.WithTags("User"));
    }

    public override async Task HandleAsync(UnlinkExternalIdentityRequest req, CancellationToken ct)
    {
        var user = ListExternalIdentitiesEndpoint.CurrentUser(_userRepository, HttpContext);
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        try
        {
            user.UnlinkExternalIdentity(req.Provider);
            _userRepository.Update(user);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.SendAsync(new { message = ex.Message }, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

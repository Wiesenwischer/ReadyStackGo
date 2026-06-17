using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.User;

public class ExternalIdentityDto
{
    public string Provider { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
}

/// <summary>
/// GET /api/user/external-identities - lists the current user's linked OIDC identities.
/// </summary>
public class ListExternalIdentitiesEndpoint : EndpointWithoutRequest<List<ExternalIdentityDto>>
{
    private readonly IUserRepository _userRepository;

    public ListExternalIdentitiesEndpoint(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public override void Configure()
    {
        Get("/api/user/external-identities");
        Description(b => b.WithTags("User"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var user = CurrentUser(_userRepository, HttpContext);
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        Response = user.ExternalIdentities
            .Select(e => new ExternalIdentityDto { Provider = e.Provider, LinkedAt = e.LinkedAt })
            .ToList();
        return Task.CompletedTask;
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

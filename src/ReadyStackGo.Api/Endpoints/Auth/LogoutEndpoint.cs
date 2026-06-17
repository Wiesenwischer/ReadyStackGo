using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Auth;

public class LogoutEndpoint : EndpointWithoutRequest
{
    private readonly ITokenRevocationService _revocation;

    public LogoutEndpoint(ITokenRevocationService revocation)
    {
        _revocation = revocation;
    }

    public override void Configure()
    {
        Post("/api/auth/logout");
        AllowAnonymous(); // A present bearer token is still authenticated by the middleware.
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        // Revoke the current token server-side so it can no longer be used before its expiry.
        // The client also clears it locally. If no valid token was sent, this is a no-op.
        var jti = HttpContext.User.FindFirst("jti")?.Value;
        var expClaim = HttpContext.User.FindFirst("exp")?.Value;

        if (!string.IsNullOrEmpty(jti) && long.TryParse(expClaim, out var expUnix))
        {
            _revocation.Revoke(jti, DateTimeOffset.FromUnixTimeSeconds(expUnix));
        }

        return Task.CompletedTask;
    }
}

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace ReadyStackGo.API.Endpoints.Auth;

public class LogoutEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/auth/logout");
        AllowAnonymous(); // Logout doesn't require authentication
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        // For JWT, logout is handled client-side by removing the token
        // In the future, we could add token blacklisting if needed
        // FastEndpoints will automatically return 200 OK for successful completion
        return Task.CompletedTask;
    }
}

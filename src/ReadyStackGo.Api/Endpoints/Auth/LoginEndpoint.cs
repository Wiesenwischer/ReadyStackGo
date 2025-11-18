using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Auth;
using ReadyStackGo.Application.Auth.DTOs;

namespace ReadyStackGo.API.Endpoints.Auth;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    public IAuthService AuthService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var result = await AuthService.LoginAsync(req);

        if (result is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response = result;
    }
}

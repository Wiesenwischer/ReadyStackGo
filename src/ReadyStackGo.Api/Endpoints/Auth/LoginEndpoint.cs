using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.UseCases.Authentication.Login;

namespace ReadyStackGo.API.Endpoints.Auth;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IMediator _mediator;

    public LoginEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(req.Username, req.Password), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Invalid username or password", StatusCodes.Status401Unauthorized);
            return;
        }

        Response = new LoginResponse
        {
            Token = result.Token!,
            Username = result.Username!,
            Role = result.Role!
        };
    }
}

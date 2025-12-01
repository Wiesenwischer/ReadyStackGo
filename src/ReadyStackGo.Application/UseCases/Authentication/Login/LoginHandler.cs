using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Application.UseCases.Authentication.Login;

public class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly AuthenticationService _authenticationService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        AuthenticationService authenticationService,
        ITokenService tokenService,
        ILogger<LoginHandler> logger)
    {
        _authenticationService = authenticationService;
        _tokenService = tokenService;
        _logger = logger;
    }

    public Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = _authenticationService.Authenticate(request.Username, request.Password);

            if (user == null)
            {
                _logger.LogWarning("Login attempt with invalid credentials for user: {Username}", request.Username);
                return Task.FromResult(new LoginResult(false, null, null, null, "Invalid username or password"));
            }

            var token = _tokenService.GenerateToken(user);
            var role = user.HasRole(RoleId.SystemAdmin) ? "admin" : "user";

            _logger.LogInformation("User logged in successfully: {Username}", user.Username);

            return Task.FromResult(new LoginResult(true, token, user.Username, role, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
            return Task.FromResult(new LoginResult(false, null, null, null, "Login failed"));
        }
    }
}

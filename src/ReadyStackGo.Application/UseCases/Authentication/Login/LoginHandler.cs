using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Access.ValueObjects;
using ReadyStackGo.Domain.Auth;
using ReadyStackGo.Domain.Identity.Services;

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
            var domainUser = _authenticationService.Authenticate(request.Username, request.Password);

            if (domainUser == null)
            {
                _logger.LogWarning("Login attempt with invalid credentials for user: {Username}", request.Username);
                return Task.FromResult(new LoginResult(false, null, null, null, "Invalid username or password"));
            }

            var role = domainUser.HasRole(RoleId.SystemAdmin) ? UserRole.Admin : UserRole.User;

            var user = new User
            {
                Username = domainUser.Username,
                PasswordHash = domainUser.Password.Hash,
                Role = role,
                CreatedAt = domainUser.CreatedAt
            };

            var token = _tokenService.GenerateToken(user);

            _logger.LogInformation("User logged in successfully: {Username}", user.Username);

            return Task.FromResult(new LoginResult(true, token, user.Username, user.Role, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
            return Task.FromResult(new LoginResult(false, null, null, null, "Login failed"));
        }
    }
}

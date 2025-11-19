using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Auth;
using ReadyStackGo.Application.Auth.DTOs;
using ReadyStackGo.Domain.Auth;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly ITokenService _tokenService;
    private readonly IConfigStore _configStore;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ITokenService tokenService,
        IConfigStore configStore,
        ILogger<AuthService> logger)
    {
        _tokenService = tokenService;
        _configStore = configStore;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            // Load security config to get admin user
            var securityConfig = await _configStore.GetSecurityConfigAsync();

            if (securityConfig.LocalAdmin == null)
            {
                _logger.LogWarning("No admin user configured. Please complete the setup wizard.");
                return null;
            }

            // Check if username matches
            if (request.Username != securityConfig.LocalAdmin.Username)
            {
                _logger.LogWarning("Login attempt with invalid username: {Username}", request.Username);
                return null;
            }

            // Verify password using BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.Password, securityConfig.LocalAdmin.PasswordHash))
            {
                _logger.LogWarning("Login attempt with invalid password for user: {Username}", request.Username);
                return null;
            }

            // Create user object
            var user = new User
            {
                Username = securityConfig.LocalAdmin.Username,
                PasswordHash = securityConfig.LocalAdmin.PasswordHash,
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            };

            var token = _tokenService.GenerateToken(user);

            var response = new LoginResponse(
                Token: token,
                Username: user.Username,
                Role: user.Role
            );

            _logger.LogInformation("User logged in successfully: {Username}", user.Username);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
            return null;
        }
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        // Token validation is handled by ASP.NET Core JWT middleware
        return Task.FromResult(true);
    }
}

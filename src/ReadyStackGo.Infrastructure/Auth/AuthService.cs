using ReadyStackGo.Application.Auth;
using ReadyStackGo.Application.Auth.DTOs;
using ReadyStackGo.Domain.Auth;

namespace ReadyStackGo.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly ITokenService _tokenService;
    private readonly Dictionary<string, User> _users;

    public AuthService(ITokenService tokenService)
    {
        _tokenService = tokenService;

        // TODO: For v0.2, we use hardcoded admin user
        // Later versions will use database/configuration
        _users = new Dictionary<string, User>
        {
            ["admin"] = new User
            {
                Username = "admin",
                // Password: "admin" - BCrypt hash
                PasswordHash = "$2a$11$5pZhQ3PZkV9gJxqN5qT5Ru.kGZhKqXn5z8xF4lK9qZ5KqZ5KqZ5Kq",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            }
        };
    }

    public Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        if (!_users.TryGetValue(request.Username, out var user))
        {
            return Task.FromResult<LoginResponse?>(null);
        }

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Task.FromResult<LoginResponse?>(null);
        }

        var token = _tokenService.GenerateToken(user);

        var response = new LoginResponse(
            Token: token,
            Username: user.Username,
            Role: user.Role
        );

        return Task.FromResult<LoginResponse?>(response);
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        // Token validation is handled by ASP.NET Core JWT middleware
        return Task.FromResult(true);
    }
}

using ReadyStackGo.Application.Auth.DTOs;

namespace ReadyStackGo.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<bool> ValidateTokenAsync(string token);
}

using ReadyStackGo.Domain.Auth;

namespace ReadyStackGo.Application.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}

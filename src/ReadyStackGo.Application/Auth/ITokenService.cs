using ReadyStackGo.Domain.Auth;

namespace ReadyStackGo.Application.Auth;

public interface ITokenService
{
    string GenerateToken(User user);
}

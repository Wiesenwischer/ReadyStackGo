using ReadyStackGo.Domain.IdentityAccess.Aggregates;

namespace ReadyStackGo.Application.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}

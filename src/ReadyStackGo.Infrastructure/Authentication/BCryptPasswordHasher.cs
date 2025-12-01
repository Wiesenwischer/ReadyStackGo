namespace ReadyStackGo.Infrastructure.Authentication;

using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// BCrypt-based password hasher for production use.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plainTextPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainTextPassword, WorkFactor);
    }

    public bool Verify(string plainTextPassword, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(plainTextPassword, hash);
    }
}

namespace ReadyStackGo.Domain.IdentityAccess.Services;

/// <summary>
/// Service interface for password hashing.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plainTextPassword);
    bool Verify(string plainTextPassword, string hash);
}

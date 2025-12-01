namespace ReadyStackGo.DomainTests.Support;

using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// Simple password hasher for testing purposes.
/// In production, use BCrypt or similar.
/// </summary>
public class TestPasswordHasher : IPasswordHasher
{
    private const string HashPrefix = "TEST_HASH:";

    public string Hash(string plainTextPassword)
    {
        return HashPrefix + plainTextPassword;
    }

    public bool Verify(string plainTextPassword, string hash)
    {
        return hash == HashPrefix + plainTextPassword;
    }
}

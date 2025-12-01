namespace ReadyStackGo.Domain.IdentityAccess.Services;

using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.Repositories;

/// <summary>
/// Domain service for user authentication.
/// </summary>
public class AuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <returns>The authenticated user, or null if authentication failed.</returns>
    public User? Authenticate(string username, string password)
    {
        var user = _userRepository.FindByUsername(username);
        if (user == null) return null;

        if (!user.Enablement.IsEnabled) return null;

        if (!user.Password.Verify(password, _passwordHasher)) return null;

        return user;
    }
}

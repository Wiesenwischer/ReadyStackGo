namespace ReadyStackGo.Domain.IdentityAccess.Users;




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
    /// Authenticates a user with an identifier (email address or username) and password.
    /// </summary>
    /// <returns>The authenticated user, or null if authentication failed.</returns>
    public User? Authenticate(string identifier, string password)
    {
        var user = FindByIdentifier(identifier);
        if (user == null) return null;

        if (!user.Enablement.IsEnabled) return null;

        // Users without a local password authenticate only via an external provider (OIDC).
        if (user.Password is null) return null;

        if (!user.Password.Verify(password, _passwordHasher)) return null;

        return user;
    }

    /// <summary>
    /// Resolves a login identifier to a user. An identifier containing '@' is treated as an
    /// email address (with a fallback to username lookup); otherwise it is treated as a
    /// username.
    /// </summary>
    private User? FindByIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        if (identifier.Contains('@'))
        {
            var email = TryParseEmail(identifier);
            if (email != null)
            {
                var byEmail = _userRepository.FindByEmail(email);
                if (byEmail != null) return byEmail;
            }
        }

        return _userRepository.FindByUsername(identifier);
    }

    private static EmailAddress? TryParseEmail(string value)
    {
        try
        {
            return new EmailAddress(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

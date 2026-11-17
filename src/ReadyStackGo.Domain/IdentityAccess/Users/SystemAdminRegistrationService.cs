namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.IdentityAccess.Roles;





/// <summary>
/// Domain service for registering the initial system administrator.
/// </summary>
public class SystemAdminRegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public SystemAdminRegistrationService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    /// <summary>
    /// Serializes the creation of the first system administrator across all setup paths
    /// (built-in sign-in and sign-in through an identity provider) so that exactly one wins.
    /// </summary>
    private static readonly object FirstAdminGate = new();

    /// <summary>
    /// Registers the initial system administrator with a real email address.
    /// The email is intentionally NOT marked verified: there is no SMTP server during
    /// initial setup, so verification happens later through a real ownership proof. The
    /// bootstrap admin can still log in (trust is placed in the setup process, not the
    /// email), see <see cref="AuthenticationService"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a system admin already exists.</exception>
    public User RegisterSystemAdmin(string username, string email, string password)
    {
        lock (FirstAdminGate)
        {
            EnsureNoSystemAdmin();

            var userId = _userRepository.NextIdentity();
            var emailAddress = new EmailAddress(email);
            var hashedPassword = HashedPassword.Create(password, _passwordHasher);

            var user = User.Register(userId, username, emailAddress, hashedPassword);
            user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

            _userRepository.Add(user);

            return user;
        }
    }

    /// <summary>
    /// Registers the initial system administrator from a sign-in at an external identity
    /// provider: no local password, linked to (provider, subject), email verified because the
    /// provider confirmed it (the setup path requires a verified email).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a system admin already exists.</exception>
    public User RegisterExternalSystemAdmin(string username, EmailAddress email, string provider, string subject)
    {
        lock (FirstAdminGate)
        {
            EnsureNoSystemAdmin();

            var user = User.RegisterExternal(_userRepository.NextIdentity(), username, email, provider, subject, emailVerified: true);
            user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

            _userRepository.Add(user);

            return user;
        }
    }

    /// <summary>True if a system administrator exists.</summary>
    public bool SystemAdminExists() =>
        _userRepository.GetAll().Any(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

    private void EnsureNoSystemAdmin()
    {
        if (SystemAdminExists())
        {
            throw new InvalidOperationException("System administrator already exists.");
        }
    }
}

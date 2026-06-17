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
    /// Registers the initial system administrator with a real email address.
    /// The email is intentionally NOT marked verified: there is no SMTP server during
    /// initial setup, so verification happens later through a real ownership proof. The
    /// bootstrap admin can still log in (trust is placed in the setup process, not the
    /// email), see <see cref="AuthenticationService"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a system admin already exists.</exception>
    public User RegisterSystemAdmin(string username, string email, string password)
    {
        // Check if system admin already exists
        var existingAdmins = _userRepository.GetAll()
            .Where(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

        if (existingAdmins.Any())
        {
            throw new InvalidOperationException("System administrator already exists.");
        }

        var userId = _userRepository.NextIdentity();
        var emailAddress = new EmailAddress(email);
        var hashedPassword = HashedPassword.Create(password, _passwordHasher);

        var user = User.Register(userId, username, emailAddress, hashedPassword);
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

        _userRepository.Add(user);

        return user;
    }
}

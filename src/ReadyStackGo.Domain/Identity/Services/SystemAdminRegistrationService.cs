namespace ReadyStackGo.Domain.Identity.Services;

using ReadyStackGo.Domain.Access.ValueObjects;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.ValueObjects;

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
    /// Registers the initial system administrator.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a system admin already exists.</exception>
    public User RegisterSystemAdmin(string username, string password)
    {
        // Check if system admin already exists
        var existingAdmins = _userRepository.GetAll()
            .Where(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

        if (existingAdmins.Any())
        {
            throw new InvalidOperationException("System administrator already exists.");
        }

        var userId = _userRepository.NextIdentity();
        var email = new EmailAddress($"{username}@system.local");
        var hashedPassword = HashedPassword.Create(password, _passwordHasher);

        var user = User.Register(userId, username, email, hashedPassword);
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

        _userRepository.Add(user);

        return user;
    }
}

namespace ReadyStackGo.Domain.Identity.Services;

using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.Repositories;

/// <summary>
/// Domain service for user authentication.
/// </summary>
public class AuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthenticationService(
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
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

        var tenant = _tenantRepository.Get(user.TenantId);
        if (tenant == null || !tenant.Active) return null;

        if (!user.Enablement.IsEnabled) return null;

        if (!user.Password.Verify(password, _passwordHasher)) return null;

        return user;
    }
}

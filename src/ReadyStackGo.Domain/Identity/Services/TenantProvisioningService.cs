namespace ReadyStackGo.Domain.Identity.Services;

using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.Access.ValueObjects;

/// <summary>
/// Domain service for provisioning new tenants with their admin users.
/// </summary>
public class TenantProvisioningService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public TenantProvisioningService(
        ITenantRepository tenantRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    /// <summary>
    /// Provisions a new tenant with an administrator user.
    /// The admin user receives SystemAdmin (global) and OrganizationOwner (org-scoped) roles.
    /// </summary>
    public (Tenant Tenant, User AdminUser) ProvisionTenant(
        string tenantName,
        string description,
        string adminUsername,
        string adminEmail,
        string adminPassword)
    {
        // Check for duplicate tenant name
        var existingTenant = _tenantRepository.GetByName(tenantName);
        if (existingTenant != null)
        {
            throw new InvalidOperationException("Organization name already exists.");
        }

        // 1. Create tenant
        var tenantId = _tenantRepository.NextIdentity();
        var tenant = Tenant.Provision(tenantId, tenantName, description);
        tenant.Activate();

        // 2. Create admin user
        var userId = _userRepository.NextIdentity();
        var email = new EmailAddress(adminEmail);
        var password = HashedPassword.Create(adminPassword, _passwordHasher);
        var user = User.Register(userId, tenantId, adminUsername, email, password);

        // 3. Assign roles
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, tenantId.Value.ToString()));

        // 4. Persist
        _tenantRepository.Add(tenant);
        _userRepository.Add(user);

        return (tenant, user);
    }
}

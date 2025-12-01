namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;





/// <summary>
/// Domain service for provisioning new organizations.
/// </summary>
public class OrganizationProvisioningService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;

    public OrganizationProvisioningService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <summary>
    /// Provisions a new organization and assigns the specified user as OrganizationOwner.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if organization name already exists.</exception>
    public Organization ProvisionOrganization(string name, string description, User owner)
    {
        // Check for duplicate organization name
        var existingOrganization = _organizationRepository.GetByName(name);
        if (existingOrganization != null)
        {
            throw new InvalidOperationException("Organization name already exists.");
        }

        // Create and activate organization
        var organizationId = _organizationRepository.NextIdentity();
        var organization = Organization.Provision(organizationId, name, description);
        organization.Activate();

        // Assign owner role to the user
        owner.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, organizationId.Value.ToString()));

        // Persist
        _organizationRepository.Add(organization);
        _userRepository.Update(owner);

        return organization;
    }
}

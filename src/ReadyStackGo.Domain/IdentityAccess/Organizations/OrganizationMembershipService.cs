namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Roles;

/// <summary>
/// Domain service for managing organization memberships.
/// Handles invitations, joins, leaves, and membership status changes.
/// </summary>
public class OrganizationMembershipService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;

    public OrganizationMembershipService(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <summary>
    /// Invites a user to join an organization.
    /// </summary>
    public OrganizationMembership InviteUser(
        OrganizationId organizationId,
        UserId userId,
        UserId invitedBy,
        RoleId roleToAssign,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(invitedBy);
        ArgumentNullException.ThrowIfNull(roleToAssign);

        var organization = _organizationRepository.Get(organizationId)
            ?? throw new InvalidOperationException($"Organization '{organizationId}' not found.");

        if (!organization.Active)
            throw new InvalidOperationException("Cannot invite users to an inactive organization.");

        var user = _userRepository.Get(userId)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        var inviter = _userRepository.Get(invitedBy)
            ?? throw new InvalidOperationException($"Inviting user '{invitedBy}' not found.");

        // Check if user is already a member
        if (user.IsMemberOfOrganization(organizationId.Value.ToString()))
            throw new InvalidOperationException("User is already a member of this organization.");

        // Validate inviter has permission to invite
        if (!inviter.IsSystemAdmin() &&
            !inviter.HasRoleWithScope(RoleId.OrganizationOwner, ScopeType.Organization, organizationId.Value.ToString()))
        {
            throw new InvalidOperationException("You do not have permission to invite users to this organization.");
        }

        // Validate the role can be assigned at organization scope
        var role = Role.GetById(roleToAssign)
            ?? throw new InvalidOperationException($"Role '{roleToAssign}' not found.");

        if (!role.CanBeAssignedToScope(ScopeType.Organization))
            throw new InvalidOperationException($"Role '{role.Name}' cannot be assigned at organization scope.");

        var membership = OrganizationMembership.CreatePendingInvitation(
            userId,
            organizationId,
            invitedBy,
            note);

        return membership;
    }

    /// <summary>
    /// Adds a user directly to an organization (bypassing invitation).
    /// Used for initial organization setup or system admin actions.
    /// </summary>
    public OrganizationMembership AddMember(
        OrganizationId organizationId,
        UserId userId,
        RoleId roleToAssign,
        UserId? addedBy = null)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roleToAssign);

        var organization = _organizationRepository.Get(organizationId)
            ?? throw new InvalidOperationException($"Organization '{organizationId}' not found.");

        var user = _userRepository.Get(userId)
            ?? throw new InvalidOperationException($"User '{userId}' not found.");

        // Check if user is already a member
        if (user.IsMemberOfOrganization(organizationId.Value.ToString()))
            throw new InvalidOperationException("User is already a member of this organization.");

        // Validate the role can be assigned at organization scope
        var role = Role.GetById(roleToAssign)
            ?? throw new InvalidOperationException($"Role '{roleToAssign}' not found.");

        if (!role.CanBeAssignedToScope(ScopeType.Organization))
            throw new InvalidOperationException($"Role '{role.Name}' cannot be assigned at organization scope.");

        // Assign the role to the user
        user.AssignRole(RoleAssignment.ForOrganization(roleToAssign, organizationId.Value.ToString()));

        var membership = OrganizationMembership.Create(
            userId,
            organizationId,
            addedBy);

        return membership;
    }

    /// <summary>
    /// Accepts an invitation and activates membership.
    /// </summary>
    public OrganizationMembership AcceptInvitation(
        OrganizationMembership membership,
        RoleId roleToAssign)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(roleToAssign);

        var user = _userRepository.Get(membership.UserId)
            ?? throw new InvalidOperationException($"User '{membership.UserId}' not found.");

        var acceptedMembership = membership.Accept();

        // Assign the role to the user
        user.AssignRole(RoleAssignment.ForOrganization(roleToAssign, membership.OrganizationId.Value.ToString()));

        return acceptedMembership;
    }

    /// <summary>
    /// Removes a user from an organization.
    /// </summary>
    public OrganizationMembership RemoveMember(
        OrganizationMembership membership,
        UserId removedBy,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(removedBy);

        var user = _userRepository.Get(membership.UserId)
            ?? throw new InvalidOperationException($"User '{membership.UserId}' not found.");

        var remover = _userRepository.Get(removedBy)
            ?? throw new InvalidOperationException($"Removing user '{removedBy}' not found.");

        // Validate remover has permission
        if (!remover.IsSystemAdmin() &&
            !remover.HasRoleWithScope(RoleId.OrganizationOwner, ScopeType.Organization, membership.OrganizationId.Value.ToString()))
        {
            throw new InvalidOperationException("You do not have permission to remove members from this organization.");
        }

        // Cannot remove yourself if you're the last owner
        if (membership.UserId == removedBy)
        {
            var owners = GetOrganizationOwners(membership.OrganizationId);
            if (owners.Count() == 1 && owners.First() == membership.UserId)
            {
                throw new InvalidOperationException("Cannot remove yourself as you are the last owner of this organization.");
            }
        }

        // Revoke all roles for this organization
        user.RevokeAllRolesForOrganization(membership.OrganizationId.Value.ToString());

        return membership.Leave();
    }

    /// <summary>
    /// Gets all organization owners.
    /// </summary>
    public IEnumerable<UserId> GetOrganizationOwners(OrganizationId organizationId)
    {
        var users = _userRepository.GetAll();
        return users
            .Where(u => u.HasRoleWithScope(RoleId.OrganizationOwner, ScopeType.Organization, organizationId.Value.ToString()))
            .Select(u => u.Id);
    }

    /// <summary>
    /// Gets member count for an organization.
    /// </summary>
    public int GetMemberCount(OrganizationId organizationId)
    {
        var users = _userRepository.GetAll();
        return users.Count(u => u.IsMemberOfOrganization(organizationId.Value.ToString()));
    }

    /// <summary>
    /// Validates that a user can perform actions in an organization.
    /// </summary>
    public bool CanAccessOrganization(UserId userId, OrganizationId organizationId)
    {
        var user = _userRepository.Get(userId);
        if (user == null) return false;

        if (!user.Enablement.IsEnabled) return false;

        if (user.IsSystemAdmin()) return true;

        return user.IsMemberOfOrganization(organizationId.Value.ToString());
    }
}

namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// Aggregate root representing an organization in the system.
/// Rich domain model with membership tracking, activation lifecycle, and business rules.
/// </summary>
public class Organization : AggregateRoot<OrganizationId>
{
    private readonly List<OrganizationMembership> _memberships = new();

    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId? OwnerId { get; private set; }

    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    // For EF Core
    protected Organization() { }

    private Organization(OrganizationId id, string name, string description, UserId? ownerId = null)
    {
        SelfAssertArgumentNotNull(id, "OrganizationId is required.");
        SelfAssertArgumentNotEmpty(name, "Organization name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Organization name must be 100 characters or less.");
        SelfAssertArgumentNotEmpty(description, "Organization description is required.");
        SelfAssertArgumentLength(description, 1, 500, "Organization description must be 500 characters or less.");

        Id = id;
        Name = name;
        Description = description;
        OwnerId = ownerId;
        Active = false;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrganizationProvisioned(Id, Name));
    }

    #region Factory Methods

    public static Organization Provision(OrganizationId id, string name, string description)
    {
        return new Organization(id, name, description);
    }

    public static Organization ProvisionWithOwner(OrganizationId id, string name, string description, UserId ownerId)
    {
        var org = new Organization(id, name, description, ownerId);
        // Owner is automatically an active member
        org._memberships.Add(OrganizationMembership.Create(ownerId, id));
        return org;
    }

    #endregion

    #region Lifecycle

    public void Activate()
    {
        if (!Active)
        {
            Active = true;
            AddDomainEvent(new OrganizationActivated(Id));
        }
    }

    public void Deactivate()
    {
        if (Active)
        {
            Active = false;
            AddDomainEvent(new OrganizationDeactivated(Id));
        }
    }

    public void UpdateDescription(string description)
    {
        SelfAssertArgumentNotEmpty(description, "Organization description is required.");
        SelfAssertArgumentLength(description, 1, 500, "Organization description must be 500 characters or less.");

        Description = description;
    }

    public void UpdateName(string name)
    {
        SelfAssertArgumentNotEmpty(name, "Organization name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Organization name must be 100 characters or less.");

        Name = name;
    }

    #endregion

    #region Membership Management

    /// <summary>
    /// Invites a user to join the organization.
    /// </summary>
    public void InviteMember(UserId userId, UserId invitedBy, string? note = null)
    {
        SelfAssertArgumentNotNull(userId, "User ID is required.");
        SelfAssertArgumentNotNull(invitedBy, "Inviter ID is required.");
        SelfAssertArgumentTrue(Active, "Cannot invite members to an inactive organization.");

        // Check if user already has a membership (active or pending)
        var existingMembership = _memberships.FirstOrDefault(m => m.UserId == userId);
        if (existingMembership != null)
        {
            if (existingMembership.Status == MembershipStatus.Active)
                throw new InvalidOperationException("User is already a member of this organization.");
            if (existingMembership.Status == MembershipStatus.PendingInvitation)
                throw new InvalidOperationException("User already has a pending invitation.");
            if (existingMembership.Status == MembershipStatus.Suspended)
                throw new InvalidOperationException("User has a suspended membership. Reactivate instead.");

            // For Declined or Left, remove old membership and create new invitation
            _memberships.Remove(existingMembership);
        }

        var membership = OrganizationMembership.CreatePendingInvitation(userId, Id, invitedBy, note);
        _memberships.Add(membership);

        AddDomainEvent(new MemberInvited(Id, userId, invitedBy, note));
    }

    /// <summary>
    /// Accepts a pending invitation.
    /// </summary>
    public void AcceptInvitation(UserId userId)
    {
        var membership = GetMembershipOrThrow(userId);
        SelfAssertArgumentTrue(membership.Status == MembershipStatus.PendingInvitation,
            "No pending invitation found for this user.");

        var updated = membership.Accept();
        ReplaceMembership(membership, updated);

        AddDomainEvent(new MemberInvitationAccepted(Id, userId));
        AddDomainEvent(new MemberJoined(Id, userId));
    }

    /// <summary>
    /// Declines a pending invitation.
    /// </summary>
    public void DeclineInvitation(UserId userId)
    {
        var membership = GetMembershipOrThrow(userId);
        SelfAssertArgumentTrue(membership.Status == MembershipStatus.PendingInvitation,
            "No pending invitation found for this user.");

        var updated = membership.Decline();
        ReplaceMembership(membership, updated);

        AddDomainEvent(new MemberInvitationDeclined(Id, userId));
    }

    /// <summary>
    /// Adds a user directly as a member (without invitation).
    /// </summary>
    public void AddMember(UserId userId, UserId? addedBy = null)
    {
        SelfAssertArgumentNotNull(userId, "User ID is required.");
        SelfAssertArgumentTrue(Active, "Cannot add members to an inactive organization.");

        var existingMembership = _memberships.FirstOrDefault(m => m.UserId == userId);
        if (existingMembership != null && existingMembership.Status == MembershipStatus.Active)
        {
            throw new InvalidOperationException("User is already a member of this organization.");
        }

        // Remove any old memberships
        if (existingMembership != null)
        {
            _memberships.Remove(existingMembership);
        }

        var membership = OrganizationMembership.Create(userId, Id, addedBy);
        _memberships.Add(membership);

        AddDomainEvent(new MemberJoined(Id, userId));
    }

    /// <summary>
    /// Suspends a member's access.
    /// </summary>
    public void SuspendMember(UserId userId, string reason)
    {
        SelfAssertArgumentNotNull(userId, "User ID is required.");
        SelfAssertArgumentNotEmpty(reason, "Suspension reason is required.");

        // Cannot suspend the owner
        if (OwnerId != null && OwnerId == userId)
        {
            throw new InvalidOperationException("Cannot suspend the organization owner.");
        }

        var membership = GetMembershipOrThrow(userId);
        SelfAssertArgumentTrue(membership.Status == MembershipStatus.Active,
            "Can only suspend active members.");

        var updated = membership.Suspend();
        ReplaceMembership(membership, updated);

        AddDomainEvent(new MemberSuspended(Id, userId, reason));
    }

    /// <summary>
    /// Reactivates a suspended member.
    /// </summary>
    public void ReactivateMember(UserId userId)
    {
        var membership = GetMembershipOrThrow(userId);
        SelfAssertArgumentTrue(membership.Status == MembershipStatus.Suspended,
            "Can only reactivate suspended members.");

        var updated = membership.Reactivate();
        ReplaceMembership(membership, updated);

        AddDomainEvent(new MemberReactivated(Id, userId));
    }

    /// <summary>
    /// Member leaves the organization voluntarily.
    /// </summary>
    public void MemberLeave(UserId userId)
    {
        SelfAssertArgumentNotNull(userId, "User ID is required.");

        // Owner cannot leave
        if (OwnerId != null && OwnerId == userId)
        {
            throw new InvalidOperationException("Organization owner cannot leave. Transfer ownership first.");
        }

        var membership = GetMembershipOrThrow(userId);
        SelfAssertArgumentTrue(
            membership.Status == MembershipStatus.Active || membership.Status == MembershipStatus.Suspended,
            "Can only leave as an active or suspended member.");

        var updated = membership.Leave();
        ReplaceMembership(membership, updated);

        AddDomainEvent(new MemberLeft(Id, userId));
    }

    /// <summary>
    /// Removes a member from the organization (by admin).
    /// </summary>
    public void RemoveMember(UserId userId, UserId removedBy, string? reason = null)
    {
        SelfAssertArgumentNotNull(userId, "User ID is required.");
        SelfAssertArgumentNotNull(removedBy, "Remover ID is required.");

        // Cannot remove the owner
        if (OwnerId != null && OwnerId == userId)
        {
            throw new InvalidOperationException("Cannot remove the organization owner.");
        }

        var membership = GetMembershipOrThrow(userId);
        SelfAssertArgumentTrue(
            membership.Status != MembershipStatus.Left && membership.Status != MembershipStatus.Declined,
            "Member has already left or declined.");

        _memberships.Remove(membership);

        AddDomainEvent(new MemberRemoved(Id, userId, removedBy, reason));
    }

    /// <summary>
    /// Transfers ownership to another member.
    /// </summary>
    public void TransferOwnership(UserId newOwnerId, UserId currentOwnerId)
    {
        SelfAssertArgumentNotNull(newOwnerId, "New owner ID is required.");
        SelfAssertArgumentTrue(OwnerId == currentOwnerId, "Only the current owner can transfer ownership.");

        var newOwnerMembership = _memberships.FirstOrDefault(m => m.UserId == newOwnerId);
        SelfAssertArgumentNotNull(newOwnerMembership, "New owner must be a member of the organization.");
        SelfAssertArgumentTrue(newOwnerMembership!.Status == MembershipStatus.Active,
            "New owner must be an active member.");

        OwnerId = newOwnerId;
        AddDomainEvent(new OrganizationOwnershipTransferred(Id, currentOwnerId, newOwnerId));
    }

    #endregion

    #region Membership Queries

    /// <summary>
    /// Checks if a user is an active member.
    /// </summary>
    public bool IsMember(UserId userId)
    {
        return _memberships.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active);
    }

    /// <summary>
    /// Checks if a user has any membership (including pending, suspended).
    /// </summary>
    public bool HasMembership(UserId userId)
    {
        return _memberships.Any(m => m.UserId == userId);
    }

    /// <summary>
    /// Gets the membership for a user, or null if not found.
    /// </summary>
    public OrganizationMembership? GetMembership(UserId userId)
    {
        return _memberships.FirstOrDefault(m => m.UserId == userId);
    }

    /// <summary>
    /// Gets all active members.
    /// </summary>
    public IEnumerable<OrganizationMembership> GetActiveMembers()
    {
        return _memberships.Where(m => m.Status == MembershipStatus.Active);
    }

    /// <summary>
    /// Gets all pending invitations.
    /// </summary>
    public IEnumerable<OrganizationMembership> GetPendingInvitations()
    {
        return _memberships.Where(m => m.Status == MembershipStatus.PendingInvitation);
    }

    /// <summary>
    /// Gets the count of active members.
    /// </summary>
    public int GetActiveMemberCount()
    {
        return _memberships.Count(m => m.Status == MembershipStatus.Active);
    }

    /// <summary>
    /// Checks if user is the owner.
    /// </summary>
    public bool IsOwner(UserId userId)
    {
        return OwnerId != null && OwnerId == userId;
    }

    #endregion

    #region Private Helpers

    private OrganizationMembership GetMembershipOrThrow(UserId userId)
    {
        var membership = _memberships.FirstOrDefault(m => m.UserId == userId);
        if (membership == null)
            throw new InvalidOperationException("User is not a member of this organization.");
        return membership;
    }

    private void ReplaceMembership(OrganizationMembership oldMembership, OrganizationMembership newMembership)
    {
        var index = _memberships.IndexOf(oldMembership);
        _memberships[index] = newMembership;
    }

    #endregion

    public override string ToString() =>
        $"Organization [id={Id}, name={Name}, active={Active}, members={GetActiveMemberCount()}]";
}

/// <summary>
/// Domain event when organization ownership is transferred.
/// </summary>
public class OrganizationOwnershipTransferred : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId PreviousOwnerId { get; }
    public UserId NewOwnerId { get; }

    public OrganizationOwnershipTransferred(
        OrganizationId organizationId,
        UserId previousOwnerId,
        UserId newOwnerId)
    {
        OrganizationId = organizationId;
        PreviousOwnerId = previousOwnerId;
        NewOwnerId = newOwnerId;
    }
}

namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Roles;

/// <summary>
/// Value object representing a user's membership in an organization.
/// Tracks when they joined, their status, and membership metadata.
/// </summary>
public sealed class OrganizationMembership : ValueObject
{
    public UserId UserId { get; }
    public OrganizationId OrganizationId { get; }
    public MembershipStatus Status { get; private set; }
    public DateTime JoinedAt { get; }
    public DateTime? LeftAt { get; private set; }
    public UserId? InvitedBy { get; }
    public string? InvitationNote { get; }

    private OrganizationMembership(
        UserId userId,
        OrganizationId organizationId,
        MembershipStatus status,
        DateTime joinedAt,
        UserId? invitedBy,
        string? invitationNote)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        OrganizationId = organizationId ?? throw new ArgumentNullException(nameof(organizationId));
        Status = status;
        JoinedAt = joinedAt;
        InvitedBy = invitedBy;
        InvitationNote = invitationNote;
    }

    /// <summary>
    /// Creates a new active membership.
    /// </summary>
    public static OrganizationMembership Create(
        UserId userId,
        OrganizationId organizationId,
        UserId? invitedBy = null,
        string? invitationNote = null)
    {
        return new OrganizationMembership(
            userId,
            organizationId,
            MembershipStatus.Active,
            DateTime.UtcNow,
            invitedBy,
            invitationNote);
    }

    /// <summary>
    /// Creates a pending invitation membership.
    /// </summary>
    public static OrganizationMembership CreatePendingInvitation(
        UserId userId,
        OrganizationId organizationId,
        UserId invitedBy,
        string? invitationNote = null)
    {
        ArgumentNullException.ThrowIfNull(invitedBy);

        return new OrganizationMembership(
            userId,
            organizationId,
            MembershipStatus.PendingInvitation,
            DateTime.UtcNow,
            invitedBy,
            invitationNote);
    }

    /// <summary>
    /// Accepts a pending invitation.
    /// </summary>
    public OrganizationMembership Accept()
    {
        if (Status != MembershipStatus.PendingInvitation)
            throw new InvalidOperationException("Can only accept a pending invitation.");

        return new OrganizationMembership(
            UserId,
            OrganizationId,
            MembershipStatus.Active,
            JoinedAt,
            InvitedBy,
            InvitationNote);
    }

    /// <summary>
    /// Declines a pending invitation.
    /// </summary>
    public OrganizationMembership Decline()
    {
        if (Status != MembershipStatus.PendingInvitation)
            throw new InvalidOperationException("Can only decline a pending invitation.");

        var declined = new OrganizationMembership(
            UserId,
            OrganizationId,
            MembershipStatus.Declined,
            JoinedAt,
            InvitedBy,
            InvitationNote);
        declined.LeftAt = DateTime.UtcNow;
        return declined;
    }

    /// <summary>
    /// Suspends an active membership.
    /// </summary>
    public OrganizationMembership Suspend()
    {
        if (Status != MembershipStatus.Active)
            throw new InvalidOperationException("Can only suspend an active membership.");

        return new OrganizationMembership(
            UserId,
            OrganizationId,
            MembershipStatus.Suspended,
            JoinedAt,
            InvitedBy,
            InvitationNote);
    }

    /// <summary>
    /// Reactivates a suspended membership.
    /// </summary>
    public OrganizationMembership Reactivate()
    {
        if (Status != MembershipStatus.Suspended)
            throw new InvalidOperationException("Can only reactivate a suspended membership.");

        return new OrganizationMembership(
            UserId,
            OrganizationId,
            MembershipStatus.Active,
            JoinedAt,
            InvitedBy,
            InvitationNote);
    }

    /// <summary>
    /// Leaves the organization.
    /// </summary>
    public OrganizationMembership Leave()
    {
        if (Status != MembershipStatus.Active && Status != MembershipStatus.Suspended)
            throw new InvalidOperationException("Can only leave an active or suspended membership.");

        var left = new OrganizationMembership(
            UserId,
            OrganizationId,
            MembershipStatus.Left,
            JoinedAt,
            InvitedBy,
            InvitationNote);
        left.LeftAt = DateTime.UtcNow;
        return left;
    }

    /// <summary>
    /// Checks if this membership allows the user to access the organization.
    /// </summary>
    public bool AllowsAccess => Status == MembershipStatus.Active;

    /// <summary>
    /// Gets the duration of membership (from join to now or leave).
    /// </summary>
    public TimeSpan GetMembershipDuration()
    {
        var endTime = LeftAt ?? DateTime.UtcNow;
        return endTime - JoinedAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return OrganizationId;
        yield return Status;
        yield return JoinedAt;
    }
}

/// <summary>
/// Status of an organization membership.
/// </summary>
public enum MembershipStatus
{
    /// <summary>
    /// Invitation sent but not yet accepted.
    /// </summary>
    PendingInvitation,

    /// <summary>
    /// Active member with full access.
    /// </summary>
    Active,

    /// <summary>
    /// Membership suspended (no access).
    /// </summary>
    Suspended,

    /// <summary>
    /// User declined the invitation.
    /// </summary>
    Declined,

    /// <summary>
    /// User left the organization.
    /// </summary>
    Left
}

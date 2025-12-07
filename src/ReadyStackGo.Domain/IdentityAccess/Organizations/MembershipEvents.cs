namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// Event raised when a user is invited to join an organization.
/// </summary>
public sealed class MemberInvited : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }
    public UserId InvitedBy { get; }
    public string? Note { get; }

    public MemberInvited(
        OrganizationId organizationId,
        UserId userId,
        UserId invitedBy,
        string? note = null)
    {
        OrganizationId = organizationId;
        UserId = userId;
        InvitedBy = invitedBy;
        Note = note;
    }
}

/// <summary>
/// Event raised when a user accepts an invitation.
/// </summary>
public sealed class MemberInvitationAccepted : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }

    public MemberInvitationAccepted(OrganizationId organizationId, UserId userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }
}

/// <summary>
/// Event raised when a user declines an invitation.
/// </summary>
public sealed class MemberInvitationDeclined : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }

    public MemberInvitationDeclined(OrganizationId organizationId, UserId userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }
}

/// <summary>
/// Event raised when a user joins an organization directly (without invitation).
/// </summary>
public sealed class MemberJoined : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }

    public MemberJoined(OrganizationId organizationId, UserId userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }
}

/// <summary>
/// Event raised when a member's access is suspended.
/// </summary>
public sealed class MemberSuspended : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }
    public string? Reason { get; }

    public MemberSuspended(OrganizationId organizationId, UserId userId, string? reason = null)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Reason = reason;
    }
}

/// <summary>
/// Event raised when a suspended member is reactivated.
/// </summary>
public sealed class MemberReactivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }

    public MemberReactivated(OrganizationId organizationId, UserId userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }
}

/// <summary>
/// Event raised when a member leaves the organization.
/// </summary>
public sealed class MemberLeft : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }

    public MemberLeft(OrganizationId organizationId, UserId userId)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }
}

/// <summary>
/// Event raised when a member is removed from the organization.
/// </summary>
public sealed class MemberRemoved : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public UserId UserId { get; }
    public UserId RemovedBy { get; }
    public string? Reason { get; }

    public MemberRemoved(
        OrganizationId organizationId,
        UserId userId,
        UserId removedBy,
        string? reason = null)
    {
        OrganizationId = organizationId;
        UserId = userId;
        RemovedBy = removedBy;
        Reason = reason;
    }
}

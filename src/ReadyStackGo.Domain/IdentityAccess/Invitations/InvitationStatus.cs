namespace ReadyStackGo.Domain.IdentityAccess.Invitations;

/// <summary>
/// Lifecycle status of an invitation.
/// </summary>
public enum InvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Revoked = 3,
    Expired = 4
}

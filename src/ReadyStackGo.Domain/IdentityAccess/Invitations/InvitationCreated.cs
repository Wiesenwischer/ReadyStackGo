namespace ReadyStackGo.Domain.IdentityAccess.Invitations;

using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Raised when an invitation is created. Carries the plaintext token transiently so the
/// email handler can build the acceptance link; the token is never persisted (only its
/// hash is stored on the aggregate).
/// </summary>
public sealed class InvitationCreated : DomainEvent
{
    public InvitationId InvitationId { get; }
    public EmailAddress Email { get; }
    public string PlainToken { get; }

    public InvitationCreated(InvitationId invitationId, EmailAddress email, string plainToken)
    {
        InvitationId = invitationId;
        Email = email;
        PlainToken = plainToken;
    }
}

namespace ReadyStackGo.Domain.IdentityAccess.Invitations;

using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// Repository for the Invitation aggregate.
/// </summary>
public interface IInvitationRepository
{
    InvitationId NextIdentity();
    void Add(Invitation invitation);
    void Update(Invitation invitation);
    Invitation? Get(InvitationId id);

    /// <summary>Finds a pending, non-expired invitation by its token hash.</summary>
    Invitation? FindPendingByTokenHash(string tokenHash);

    /// <summary>Finds a pending, non-expired invitation for the given email address.</summary>
    Invitation? FindPendingByEmail(EmailAddress email);

    IEnumerable<Invitation> GetAll();
    void Remove(Invitation invitation);
}

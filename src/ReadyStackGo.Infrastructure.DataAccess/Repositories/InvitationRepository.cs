namespace ReadyStackGo.Infrastructure.DataAccess.Repositories;

using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// SQLite-backed implementation of IInvitationRepository.
/// </summary>
public class InvitationRepository : IInvitationRepository
{
    private readonly ReadyStackGoDbContext _context;

    public InvitationRepository(ReadyStackGoDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public InvitationId NextIdentity() => InvitationId.Create();

    public void Add(Invitation invitation)
    {
        _context.Invitations.Add(invitation);
        _context.SaveChanges();
    }

    public void Update(Invitation invitation)
    {
        _context.Invitations.Update(invitation);
        _context.SaveChanges();
    }

    public Invitation? Get(InvitationId id)
    {
        return _context.Invitations.FirstOrDefault(i => i.Id == id);
    }

    public Invitation? FindPendingByTokenHash(string tokenHash)
    {
        var now = DateTime.UtcNow;
        return _context.Invitations
            .FirstOrDefault(i =>
                i.TokenHash == tokenHash &&
                i.Status == InvitationStatus.Pending &&
                i.ExpiresAt > now);
    }

    public Invitation? FindPendingByEmail(EmailAddress email)
    {
        var emailValue = email.Value;
        var now = DateTime.UtcNow;
        return _context.Invitations
            .FirstOrDefault(i =>
                i.Email.Value == emailValue &&
                i.Status == InvitationStatus.Pending &&
                i.ExpiresAt > now);
    }

    public IEnumerable<Invitation> GetAll()
    {
        return _context.Invitations.ToList();
    }

    public void Remove(Invitation invitation)
    {
        _context.Invitations.Remove(invitation);
        _context.SaveChanges();
    }
}

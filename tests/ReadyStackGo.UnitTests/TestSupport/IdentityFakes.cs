using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.TestSupport;

/// <summary>In-memory user repository with the lookup semantics of the real one.</summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<UserId, User> _users = new();

    public int UpdateCount { get; private set; }

    public UserId NextIdentity() => UserId.Create();

    public void Add(User user) => _users[user.Id] = user;

    public void Update(User user)
    {
        UpdateCount++;
        _users[user.Id] = user;
    }

    public User? Get(UserId id) => _users.GetValueOrDefault(id);

    public User? FindByUsername(string username) =>
        _users.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

    public User? FindByEmail(EmailAddress email) => _users.Values.FirstOrDefault(u => u.Email == email);

    public User? FindByExternalIdentity(string provider, string subject)
    {
        var normalizedProvider = provider.ToLowerInvariant();
        return _users.Values.FirstOrDefault(u =>
            u.ExternalIdentities.Any(e => e.Provider == normalizedProvider && e.Subject == subject));
    }

    public int RemoveExternalIdentitiesOfProvider(string provider) =>
        _users.Values.Count(u => u.RemoveExternalIdentityOfRemovedProvider(provider));

    public IEnumerable<User> GetAll() => _users.Values;

    public void Remove(User user) => _users.Remove(user.Id);
}

/// <summary>In-memory invitation repository; FindPendingByEmail returns pending invitations regardless of expiry.</summary>
internal sealed class InMemoryInvitationRepository : IInvitationRepository
{
    private readonly Dictionary<InvitationId, Invitation> _invitations = new();

    public int UpdateCount { get; private set; }

    public InvitationId NextIdentity() => InvitationId.NewId();

    public void Add(Invitation invitation) => _invitations[invitation.Id] = invitation;

    public void Update(Invitation invitation)
    {
        UpdateCount++;
        _invitations[invitation.Id] = invitation;
    }

    public Invitation? Get(InvitationId id) => _invitations.GetValueOrDefault(id);

    public Invitation? FindPendingByTokenHash(string tokenHash) =>
        _invitations.Values.FirstOrDefault(i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending);

    public Invitation? FindPendingByEmail(EmailAddress email) =>
        _invitations.Values.FirstOrDefault(i => i.Email == email && i.Status == InvitationStatus.Pending);

    public IEnumerable<Invitation> GetAll() => _invitations.Values;

    public void Remove(Invitation invitation) => _invitations.Remove(invitation.Id);
}

/// <summary>Deterministic, fast password hasher for tests ("hash:" + password).</summary>
internal sealed class PrefixPasswordHasher : IPasswordHasher
{
    public string Hash(string plainTextPassword) => "hash:" + plainTextPassword;

    public bool Verify(string plainTextPassword, string hash) => hash == "hash:" + plainTextPassword;
}

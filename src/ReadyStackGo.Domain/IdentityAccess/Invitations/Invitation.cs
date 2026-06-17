namespace ReadyStackGo.Domain.IdentityAccess.Invitations;

using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Aggregate root representing an admin invitation for an email address to join the system
/// with a specific role at a specific scope. Accepting the invitation (via link or via an
/// OIDC login with the matching email) creates the user and proves email ownership.
///
/// Only the SHA-256 hash of the invitation token is stored; the plaintext token lives only
/// in the email link.
/// </summary>
public class Invitation : AggregateRoot<InvitationId>
{
    public EmailAddress Email { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public InvitationStatus Status { get; private set; }

    // Target role assignment granted on acceptance.
    public RoleId RoleId { get; private set; } = null!;
    public ScopeType ScopeType { get; private set; }
    public string? ScopeId { get; private set; }

    public UserId InvitedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }

    // For EF Core
    protected Invitation() { }

    private Invitation(
        InvitationId id,
        EmailAddress email,
        string plainToken,
        string tokenHash,
        RoleId roleId,
        ScopeType scopeType,
        string? scopeId,
        UserId invitedBy,
        DateTime createdAt,
        DateTime expiresAt)
    {
        SelfAssertArgumentNotNull(id, "InvitationId is required.");
        SelfAssertArgumentNotNull(email, "Email is required.");
        SelfAssertArgumentNotEmpty(plainToken, "Token is required.");
        SelfAssertArgumentNotEmpty(tokenHash, "Token hash is required.");
        SelfAssertArgumentNotNull(roleId, "RoleId is required.");
        SelfAssertArgumentNotNull(invitedBy, "InvitedBy is required.");
        SelfAssertArgumentTrue(expiresAt > createdAt, "Expiry must be after creation.");

        if (scopeType == ScopeType.Global && scopeId != null)
            throw new ArgumentException("Global scope should not have a ScopeId.");
        if (scopeType != ScopeType.Global && string.IsNullOrEmpty(scopeId))
            throw new ArgumentException("Non-global scope requires a ScopeId.");

        Id = id;
        Email = email;
        TokenHash = tokenHash;
        Status = InvitationStatus.Pending;
        RoleId = roleId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        InvitedBy = invitedBy;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;

        AddDomainEvent(new InvitationCreated(Id, Email, plainToken));
    }

    public static Invitation Create(
        InvitationId id,
        EmailAddress email,
        string plainToken,
        string tokenHash,
        RoleId roleId,
        ScopeType scopeType,
        string? scopeId,
        UserId invitedBy,
        DateTime createdAt,
        DateTime expiresAt)
    {
        return new Invitation(id, email, plainToken, tokenHash, roleId, scopeType, scopeId, invitedBy, createdAt, expiresAt);
    }

    /// <summary>The role assignment this invitation grants on acceptance.</summary>
    public RoleAssignment ToRoleAssignment() =>
        new(RoleId, ScopeType, ScopeId, CreatedAt);

    public bool IsExpired(DateTime now) => now >= ExpiresAt;

    /// <summary>
    /// Accepts the invitation. Throws if it is not pending. If it has expired, the status
    /// is updated to Expired and an exception is thrown.
    /// </summary>
    public void Accept(DateTime now)
    {
        EnsurePending();

        if (IsExpired(now))
        {
            Status = InvitationStatus.Expired;
            throw new InvalidOperationException("Invitation has expired.");
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
    }

    /// <summary>Revokes a pending invitation.</summary>
    public void Revoke()
    {
        EnsurePending();
        Status = InvitationStatus.Revoked;
    }

    private void EnsurePending()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Invitation is not pending (status: {Status}).");
    }
}

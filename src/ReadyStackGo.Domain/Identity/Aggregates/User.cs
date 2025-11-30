namespace ReadyStackGo.Domain.Identity.Aggregates;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.Identity.Events;
using ReadyStackGo.Domain.Identity.Services;
using ReadyStackGo.Domain.Access.ValueObjects;

/// <summary>
/// Aggregate root representing a user in the system.
/// </summary>
public class User : AggregateRoot<UserId>
{
    private readonly List<RoleAssignment> _roleAssignments = new();

    public TenantId TenantId { get; private set; } = null!;
    public string Username { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;
    public HashedPassword Password { get; private set; } = null!;
    public Enablement Enablement { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyCollection<RoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    // For EF Core
    protected User() { }

    private User(
        UserId id,
        TenantId tenantId,
        string username,
        EmailAddress email,
        HashedPassword password)
    {
        SelfAssertArgumentNotNull(id, "UserId is required.");
        SelfAssertArgumentNotNull(tenantId, "TenantId is required.");
        SelfAssertArgumentNotEmpty(username, "Username is required.");
        SelfAssertArgumentLength(username, 3, 50, "Username must be 3 to 50 characters.");
        SelfAssertArgumentNotNull(email, "Email is required.");
        SelfAssertArgumentNotNull(password, "Password is required.");

        Id = id;
        TenantId = tenantId;
        Username = username;
        Email = email;
        Password = password;
        Enablement = Enablement.IndefiniteEnablement();
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new UserRegistered(Id, TenantId, Username, Email));
    }

    public static User Register(
        UserId id,
        TenantId tenantId,
        string username,
        EmailAddress email,
        HashedPassword password)
    {
        return new User(id, tenantId, username, email, password);
    }

    public void AssignRole(RoleAssignment assignment)
    {
        SelfAssertArgumentNotNull(assignment, "Role assignment is required.");

        // Check if same role with same scope already exists
        var existing = _roleAssignments.FirstOrDefault(ra =>
            ra.RoleId == assignment.RoleId &&
            ra.ScopeType == assignment.ScopeType &&
            ra.ScopeId == assignment.ScopeId);

        if (existing != null)
        {
            return; // Already has this role assignment
        }

        _roleAssignments.Add(assignment);
        AddDomainEvent(new UserRoleAssigned(Id, assignment.RoleId, assignment.ScopeType, assignment.ScopeId));
    }

    public void RevokeRole(RoleId roleId, ScopeType scopeType, string? scopeId)
    {
        var assignment = _roleAssignments.FirstOrDefault(ra =>
            ra.RoleId == roleId &&
            ra.ScopeType == scopeType &&
            ra.ScopeId == scopeId);

        if (assignment != null)
        {
            _roleAssignments.Remove(assignment);
            AddDomainEvent(new UserRoleRevoked(Id, roleId, scopeType, scopeId));
        }
    }

    public void ChangePassword(HashedPassword newPassword)
    {
        SelfAssertArgumentNotNull(newPassword, "New password is required.");
        Password = newPassword;
        AddDomainEvent(new UserPasswordChanged(Id));
    }

    public void Enable()
    {
        if (!Enablement.IsEnabled)
        {
            Enablement = Enablement.IndefiniteEnablement();
            AddDomainEvent(new UserEnablementChanged(Id, true));
        }
    }

    public void Disable()
    {
        if (Enablement.IsEnabled)
        {
            Enablement = Enablement.Disabled();
            AddDomainEvent(new UserEnablementChanged(Id, false));
        }
    }

    public bool HasRole(RoleId roleId) =>
        _roleAssignments.Any(ra => ra.RoleId == roleId);

    public bool HasRoleWithScope(RoleId roleId, ScopeType scopeType, string? scopeId) =>
        _roleAssignments.Any(ra =>
            ra.RoleId == roleId &&
            ra.ScopeType == scopeType &&
            ra.ScopeId == scopeId);

    public override string ToString() =>
        $"User [id={Id}, username={Username}, tenantId={TenantId}]";
}

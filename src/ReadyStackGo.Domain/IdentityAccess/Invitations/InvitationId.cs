namespace ReadyStackGo.Domain.IdentityAccess.Invitations;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying an Invitation.
/// </summary>
public sealed class InvitationId : ValueObject
{
    public Guid Value { get; }

    public InvitationId()
    {
        Value = Guid.NewGuid();
    }

    public InvitationId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "InvitationId cannot be empty.");
        Value = value;
    }

    public static InvitationId Create() => new();
    public static InvitationId NewId() => new();
    public static InvitationId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}

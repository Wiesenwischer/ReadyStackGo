namespace ReadyStackGo.Domain.IdentityAccess.ApiKeys;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object identifying an API Key.
/// </summary>
public sealed class ApiKeyId : ValueObject
{
    public Guid Value { get; }

    public ApiKeyId()
    {
        Value = Guid.NewGuid();
    }

    public ApiKeyId(Guid value)
    {
        SelfAssertArgumentTrue(value != Guid.Empty, "ApiKeyId cannot be empty.");
        Value = value;
    }

    public static ApiKeyId Create() => new();
    public static ApiKeyId NewId() => new();
    public static ApiKeyId FromGuid(Guid value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}

namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object linking a user to an external identity provider (OIDC).
/// Identity is defined by the provider name and the provider's stable subject ("sub").
/// </summary>
public sealed class ExternalIdentity : ValueObject
{
    public string Provider { get; private set; }
    public string Subject { get; private set; }
    public DateTime LinkedAt { get; private set; }

    // For EF Core
    private ExternalIdentity()
    {
        Provider = null!;
        Subject = null!;
    }

    public ExternalIdentity(string provider, string subject, DateTime linkedAt)
    {
        SelfAssertArgumentNotEmpty(provider, "Provider is required.");
        SelfAssertArgumentNotEmpty(subject, "Subject is required.");

        Provider = provider.ToLowerInvariant();
        Subject = subject;
        LinkedAt = linkedAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Provider;
        yield return Subject;
    }

    public override string ToString() =>
        $"ExternalIdentity [provider={Provider}, subject={Subject}]";
}

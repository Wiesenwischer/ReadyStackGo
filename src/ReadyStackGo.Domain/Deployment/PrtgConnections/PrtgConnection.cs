namespace ReadyStackGo.Domain.Deployment.PrtgConnections;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Aggregate root representing a reusable connection to a PRTG Network Monitor
/// instance. ProductDeployments can optionally reference one to enable
/// auto-register / auto-deregister on lifecycle events.
///
/// Mirrors the <see cref="Registries.Registry"/> pattern: org-scoped, named,
/// credentials are encrypted and never round-tripped through the read API.
/// </summary>
public class PrtgConnection : AggregateRoot<PrtgConnectionId>
{
    public OrganizationId OrganizationId { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    /// <summary>PRTG base URL, e.g. <c>https://prtg.example.local</c>.</summary>
    public string Url { get; private set; } = null!;

    /// <summary>Encrypted PRTG passhash or API token. Never exposed in read DTOs.</summary>
    public string EncryptedApiToken { get; private set; } = null!;

    /// <summary>
    /// ID of a "template device" pre-configured in PRTG that lifecycle handlers
    /// duplicate per ProductDeployment. Optional — when null the handler creates
    /// devices without a template (only the device, no sensors).
    /// </summary>
    public int? TemplateDeviceId { get; private set; }

    /// <summary>
    /// If false the HTTP client skips certificate validation. Useful for PRTG
    /// installations with self-signed certificates (very common).
    /// </summary>
    public bool VerifyTls { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    // For EF Core
    protected PrtgConnection() { }

    private PrtgConnection(
        PrtgConnectionId id,
        OrganizationId organizationId,
        string name,
        string url,
        string encryptedApiToken,
        int? templateDeviceId,
        bool verifyTls)
    {
        SelfAssertArgumentNotNull(id, "PrtgConnectionId is required.");
        SelfAssertArgumentNotNull(organizationId, "OrganizationId is required.");
        SelfAssertArgumentNotEmpty(name, "Connection name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Connection name must be 100 characters or less.");
        SelfAssertArgumentNotEmpty(url, "PRTG URL is required.");
        SelfAssertArgumentNotEmpty(encryptedApiToken, "Encrypted API token is required.");

        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Url = NormalizeUrl(url);
        EncryptedApiToken = encryptedApiToken;
        TemplateDeviceId = templateDeviceId;
        VerifyTls = verifyTls;
        CreatedAt = SystemClock.UtcNow;

        AddDomainEvent(new PrtgConnectionCreated(Id, Name, Url));
    }

    public static PrtgConnection Create(
        PrtgConnectionId id,
        OrganizationId organizationId,
        string name,
        string url,
        string encryptedApiToken,
        int? templateDeviceId = null,
        bool verifyTls = true)
    {
        return new PrtgConnection(id, organizationId, name, url, encryptedApiToken, templateDeviceId, verifyTls);
    }

    public void Rename(string name)
    {
        SelfAssertArgumentNotEmpty(name, "Connection name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Connection name must be 100 characters or less.");

        if (Name == name) return;
        Name = name;
        UpdatedAt = SystemClock.UtcNow;
        AddDomainEvent(new PrtgConnectionUpdated(Id, Name));
    }

    public void UpdateUrl(string url)
    {
        SelfAssertArgumentNotEmpty(url, "PRTG URL is required.");
        var normalized = NormalizeUrl(url);
        if (Url == normalized) return;
        Url = normalized;
        UpdatedAt = SystemClock.UtcNow;
        AddDomainEvent(new PrtgConnectionUpdated(Id, Name));
    }

    public void UpdateApiToken(string encryptedApiToken)
    {
        SelfAssertArgumentNotEmpty(encryptedApiToken, "Encrypted API token is required.");
        EncryptedApiToken = encryptedApiToken;
        UpdatedAt = SystemClock.UtcNow;
        AddDomainEvent(new PrtgConnectionUpdated(Id, Name));
    }

    public void UpdateTemplateDeviceId(int? templateDeviceId)
    {
        TemplateDeviceId = templateDeviceId;
        UpdatedAt = SystemClock.UtcNow;
        AddDomainEvent(new PrtgConnectionUpdated(Id, Name));
    }

    public void UpdateVerifyTls(bool verifyTls)
    {
        if (VerifyTls == verifyTls) return;
        VerifyTls = verifyTls;
        UpdatedAt = SystemClock.UtcNow;
        AddDomainEvent(new PrtgConnectionUpdated(Id, Name));
    }

    public void RecordUsage()
    {
        LastUsedAt = SystemClock.UtcNow;
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }
        return url;
    }

    public override string ToString() =>
        $"PrtgConnection [id={Id}, name={Name}, url={Url}]";
}

namespace ReadyStackGo.Domain.Deployment.Environments;

using System.Text.Json.Serialization;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object representing an SSH credential (private key or password).
/// The secret is stored AES-encrypted and must be decrypted at runtime for tunnel creation.
/// </summary>
public sealed class SshCredential : ValueObject
{
    /// <summary>
    /// The AES-encrypted secret (private key content or password).
    /// </summary>
    public string EncryptedSecret { get; }

    /// <summary>
    /// The authentication method this credential is for.
    /// </summary>
    public SshAuthMethod AuthMethod { get; }

    // For EF Core / JSON deserialization
    private SshCredential()
    {
        EncryptedSecret = string.Empty;
    }

    [JsonConstructor]
    private SshCredential(string encryptedSecret, SshAuthMethod authMethod)
    {
        SelfAssertArgumentNotEmpty(encryptedSecret, "Encrypted secret is required.");
        EncryptedSecret = encryptedSecret;
        AuthMethod = authMethod;
    }

    public static SshCredential Create(string encryptedSecret, SshAuthMethod authMethod)
    {
        return new SshCredential(encryptedSecret, authMethod);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EncryptedSecret;
        yield return AuthMethod;
    }
}

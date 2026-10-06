using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ReadyStackGo.TestIdentityProvider;

/// <summary>
/// Settings of the test identity provider. Bound from configuration, for example the
/// environment variables <c>Issuer</c>, <c>Clients__0__ClientId</c>,
/// <c>Clients__0__ClientSecret</c> and <c>Clients__0__RedirectUris__0</c>.
/// </summary>
public class TestIdentityProviderOptions
{
    public const string DefaultIssuer = "http://localhost:9090/";

    /// <summary>Issuer and base address of all endpoints. Discovery lives at {Issuer}.well-known/openid-configuration.</summary>
    public string Issuer { get; set; } = DefaultIssuer;

    /// <summary>Statically configured clients (manual registration).</summary>
    public List<StaticClientOptions> Clients { get; set; } = new();
}

public class StaticClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public List<string> RedirectUris { get; set; } = new();
}

/// <summary>A user the authorize page offers.</summary>
public sealed record TestUser(
    string Id,
    string Subject,
    string Name,
    string Email,
    bool EmailVerified,
    string? PreferredUsername,
    string Nickname);

public static class TestUsers
{
    /// <summary>Verified email, all claims present.</summary>
    public static readonly TestUser AlexVerified =
        new("alex", "sub-alex-verified", "Alex Verified", "alex@example.com", true, "alex", "alex");

    /// <summary>Email not verified by the provider.</summary>
    public static readonly TestUser UmaUnverified =
        new("uma", "sub-uma-unverified", "Uma Unverified", "uma@example.com", false, "uma", "uma");

    /// <summary>Verified email, no preferred_username claim.</summary>
    public static readonly TestUser NoahNoUsername =
        new("noah", "sub-noah-no-username", "Noah No Username", "noah.no-username@example.com", true, null, "noah");

    /// <summary>Verified email with a plus sign (rejected by ReadyStackGo's email rules).</summary>
    public static readonly TestUser PatPlus =
        new("pat", "sub-pat-plus", "Pat Plus", "pat+test@example.com", true, "pat", "pat");

    public static readonly IReadOnlyList<TestUser> All = [AlexVerified, UmaUnverified, NoahNoUsername, PatPlus];

    public static TestUser? Find(string? id) => All.FirstOrDefault(u => string.Equals(u.Id, id, StringComparison.Ordinal));
}

public sealed class RegisteredClient
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required IReadOnlyList<string> RedirectUris { get; init; }
    public bool Paired { get; init; }
    public volatile bool Revoked;
}

public sealed record PushedRequest(
    string ClientId,
    string RedirectUri,
    string Scope,
    string? State,
    string? Nonce,
    string CodeChallenge,
    DateTimeOffset ExpiresAt);

public sealed record AuthorizationCode(
    string ClientId,
    string RedirectUri,
    string? Nonce,
    string CodeChallenge,
    TestUser User,
    DateTimeOffset ExpiresAt);

public sealed record PendingPairing(
    string Kind,
    string Name,
    string Url,
    string RedirectUri,
    string ReturnUri,
    string State,
    DateTimeOffset ExpiresAt);

public sealed record PairingCode(string State, string ClientId, string ClientSecret, DateTimeOffset ExpiresAt);

/// <summary>In-memory state of the provider: clients, pushed requests, codes, pairings and the signing key.</summary>
public sealed class ProviderState : IDisposable
{
    public static readonly TimeSpan PushedRequestLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(10);

    private readonly RSA _rsa = RSA.Create(2048);

    public ProviderState(TestIdentityProviderOptions options)
    {
        Issuer = NormalizeIssuer(options.Issuer);
        KeyId = Convert.ToHexString(SHA256.HashData(_rsa.ExportRSAPublicKey()))[..16].ToLowerInvariant();
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };

        foreach (var client in options.Clients.Where(c => !string.IsNullOrWhiteSpace(c.ClientId)))
        {
            Clients[client.ClientId] = new RegisteredClient
            {
                ClientId = client.ClientId,
                ClientSecret = client.ClientSecret,
                RedirectUris = client.RedirectUris.Where(u => !string.IsNullOrWhiteSpace(u)).ToList()
            };
        }
    }

    /// <summary>The issuer, always with a trailing slash.</summary>
    public string Issuer { get; }

    /// <summary>The issuer without trailing slash, the base of all endpoint addresses.</summary>
    public string BaseAddress => Issuer.TrimEnd('/');

    public string KeyId { get; }

    public RsaSecurityKey SigningKey { get; }

    public ConcurrentDictionary<string, RegisteredClient> Clients { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, PushedRequest> PushedRequests { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, AuthorizationCode> Codes { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, PendingPairing> Pairings { get; } = new(StringComparer.Ordinal);

    public ConcurrentDictionary<string, PairingCode> PairingCodes { get; } = new(StringComparer.Ordinal);

    public RSAParameters PublicKey => _rsa.ExportParameters(false);

    /// <summary>The client if it exists, is not revoked and the secret matches; otherwise null.</summary>
    public RegisteredClient? Authenticate(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || !Clients.TryGetValue(clientId, out var client) || client.Revoked)
        {
            return null;
        }

        var expected = Encoding.UTF8.GetBytes(client.ClientSecret);
        var presented = Encoding.UTF8.GetBytes(clientSecret ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(expected, presented) ? client : null;
    }

    public static string NewToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));

    public static string PkceChallenge(string verifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string NormalizeIssuer(string? issuer)
    {
        var value = string.IsNullOrWhiteSpace(issuer) ? TestIdentityProviderOptions.DefaultIssuer : issuer.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Issuer '{value}' is not an absolute http or https address.");
        }
        return value.EndsWith('/') ? value : value + "/";
    }

    public void Dispose() => _rsa.Dispose();
}

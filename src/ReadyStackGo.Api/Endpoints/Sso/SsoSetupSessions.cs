using Microsoft.Extensions.Caching.Memory;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.API.Endpoints.Sso;

/// <summary>
/// Server-side state of one "Add provider" run or of the changes on a provider's page (test,
/// reconnect, changed connection). Lives in memory, 60 minutes sliding, bound to the admin
/// who started it. The client secret is kept encrypted and never returned. "Cancel" drops
/// the session without traces in rsgo.oidc.json.
/// </summary>
public class SsoSetupSession
{
    public required string Id { get; init; }
    public required Guid OwnerUserId { get; init; }
    public required string TemplateId { get; init; }

    /// <summary>Name of the existing provider this session changes; null when adding a provider.</summary>
    public string? ExistingProvider { get; init; }

    public string? Authority { get; set; }
    public required string Name { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public required string Scopes { get; set; }
    public required string RegistrationKind { get; init; }
    public bool RequirePar { get; init; }
    public bool RequireHttps { get; init; }
    public required OidcClaimNames Claims { get; init; }
    public bool TrustUnverifiedEmail { get; set; }

    public string? BaseUrl { get; set; }
    public string? ClientId { get; set; }
    public string? EncryptedClientSecret { get; set; }
    public DateTime? PairedAt { get; set; }
    public string? PairedBy { get; set; }

    /// <summary>Result of the discovery checks of step "Provider address".</summary>
    public OidcCheckReport? Discovery { get; set; }

    /// <summary>Result of the full checks of step "Test".</summary>
    public OidcCheckReport? Checks { get; set; }

    /// <summary>Fingerprint of the connection the checks ran against.</summary>
    public string? ChecksFingerprint { get; set; }

    public TestSignInResult? TestSignIn { get; set; }

    /// <summary>Code the pairing brought back, redeemed by "registration/complete".</summary>
    public string? PendingRegistrationCode { get; set; }

    /// <summary>State of the pairing in flight (needed to redeem the code).</summary>
    public string? PendingRegistrationState { get; set; }

    /// <summary>Error of the last registration attempt ("access_denied", "limit_reached", "unreachable", …).</summary>
    public string? RegistrationError { get; set; }

    public string? RegistrationErrorDescription { get; set; }

    public bool IsNew => ExistingProvider == null;

    public string? RedirectUri => BaseUrl == null ? null : BaseUrlRules.ProviderRedirectUri(BaseUrl, Name);

    public bool HasCredentials => !string.IsNullOrEmpty(ClientId);
}

/// <summary>Result of a test sign-in: the claims that arrived and whether they suffice.</summary>
public record TestSignInResult(
    bool Passed,
    DateTime At,
    string Fingerprint,
    IReadOnlyList<TestSignInClaim> Claims,
    string? SignedInAs,
    string? WarningTitle,
    string? WarningBody,
    string? Error,
    string? ErrorDetail)
{
    public static TestSignInResult FromUserInfo(OidcUserInfo info, OidcClaimNames claimNames, string fingerprint, DateTime at)
    {
        var claims = new List<TestSignInClaim>
        {
            new("Subject", "sub", info.Subject, TestSignInClaimStatus.Received),
            new("Email", claimNames.Email, info.Email,
                string.IsNullOrEmpty(info.Email) ? TestSignInClaimStatus.Missing
                : info.EmailVerified ? TestSignInClaimStatus.Verified : TestSignInClaimStatus.NotVerified),
            new("Username", claimNames.Username, info.Username,
                string.IsNullOrEmpty(info.Username) ? TestSignInClaimStatus.Missing : TestSignInClaimStatus.Received),
            new("Display name", claimNames.DisplayName, info.DisplayName,
                string.IsNullOrEmpty(info.DisplayName) ? TestSignInClaimStatus.Missing : TestSignInClaimStatus.Received)
        };

        if (string.IsNullOrEmpty(info.Email))
        {
            return new TestSignInResult(false, at, fingerprint, claims, info.Subject,
                null, null,
                "The provider sends no email address",
                "ReadyStackGo needs the email claim to match accounts and invitations. Request the scope email or set the claim in the template.");
        }

        string? warningTitle = null;
        string? warningBody = null;
        if (!info.EmailVerified)
        {
            warningTitle = "The email address is not confirmed";
            warningBody = "The provider sends email_verified = false. ReadyStackGo will not match this sign-in to an existing user by email. " +
                          "Turn on “Trust unverified email addresses” in the next step only if your provider checks email addresses itself.";
        }

        return new TestSignInResult(true, at, fingerprint, claims, info.Email, warningTitle, warningBody, null, null);
    }

    public static TestSignInResult Failed(string fingerprint, DateTime at, string error, string? detail) =>
        new(false, at, fingerprint, [], null, null, null, error, detail);
}

public record TestSignInClaim(string Detail, string Claim, string? Value, string Status);

public static class TestSignInClaimStatus
{
    public const string Received = "received";
    public const string Verified = "verified";
    public const string NotVerified = "notVerified";
    public const string Missing = "missing";
}

/// <summary>In-memory store of setup sessions (60 minutes sliding).</summary>
public class SsoSetupSessionStore
{
    public static readonly TimeSpan SlidingLifetime = TimeSpan.FromMinutes(60);

    private readonly IMemoryCache _cache;

    public SsoSetupSessionStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Put(SsoSetupSession session) =>
        _cache.Set(Key(session.Id), session, new MemoryCacheEntryOptions { SlidingExpiration = SlidingLifetime });

    /// <summary>The session if it exists and belongs to <paramref name="userId"/>; otherwise null (also for foreign sessions).</summary>
    public SsoSetupSession? Get(string id, Guid userId)
    {
        if (string.IsNullOrEmpty(id) || !_cache.TryGetValue(Key(id), out SsoSetupSession? session) || session == null)
        {
            return null;
        }
        return session.OwnerUserId == userId ? session : null;
    }

    /// <summary>Looks a session up without the owner check (browser callbacks verify the flow cookie instead).</summary>
    public SsoSetupSession? GetForCallback(string id) =>
        !string.IsNullOrEmpty(id) && _cache.TryGetValue(Key(id), out SsoSetupSession? session) ? session : null;

    public void Remove(string id) => _cache.Remove(Key(id));

    private static string Key(string id) => $"sso_setup_session:{id}";
}

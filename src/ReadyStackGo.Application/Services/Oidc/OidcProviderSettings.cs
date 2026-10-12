using System.Security.Cryptography;
using System.Text;

namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>OIDC provider settings as used at runtime (client secret decrypted).</summary>
public class OidcProviderSettings
{
    /// <summary>Template id of providers created before templates existed.</summary>
    public const string GenericTemplateId = "generic-oidc";

    public const string DefaultScopes = "openid email profile";

    /// <summary>URL-safe identifier used in routes and as the key of account links. Fixed once created.</summary>
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string Scopes { get; set; } = DefaultScopes;
    public bool Enabled { get; set; }

    /// <summary>Id of the template the provider was created from.</summary>
    public string Template { get; set; } = GenericTemplateId;

    /// <summary>Registration kind copied from the template ("manual" or "pairing").</summary>
    public string Registration { get; set; } = RegistrationKinds.Manual;

    /// <summary>Pushed authorization requests are always used, even if the discovery does not require them.</summary>
    public bool RequirePar { get; set; }

    public OidcClaimNames Claims { get; set; } = new();

    /// <summary>When the client credentials were obtained through pairing; null for manual providers.</summary>
    public DateTime? PairedAt { get; set; }

    /// <summary>The provider account that confirmed the pairing, if the protocol reported it.</summary>
    public string? PairedBy { get; set; }

    /// <summary>
    /// Match sign-ins to existing users and invitations by email even if the provider did not
    /// confirm the address. On for providers created before this setting existed, off for new ones.
    /// </summary>
    public bool TrustUnverifiedEmail { get; set; }

    /// <summary>The last passed test sign-in and the connection it was made with.</summary>
    public OidcTestedSignIn? TestedSignIn { get; set; }

    /// <summary>The last check, test sign-in or sign-in result, shown in the provider list.</summary>
    public OidcLastResult? LastResult { get; set; }

    /// <summary>The provider rejected the client (invalid_client) of a paired provider.</summary>
    public bool ReconnectNeeded { get; set; }

    public bool IsPaired => string.Equals(Registration, RegistrationKinds.Pairing, StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the last passed test sign-in was made with the current connection.</summary>
    public bool HasPassedTestSignInForCurrentConnection =>
        TestedSignIn != null && TestedSignIn.Fingerprint == OidcConnectionFingerprint.Compute(this);

    public OidcProviderSettings Clone() => new()
    {
        Name = Name,
        DisplayName = DisplayName,
        Authority = Authority,
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        Scopes = Scopes,
        Enabled = Enabled,
        Template = Template,
        Registration = Registration,
        RequirePar = RequirePar,
        Claims = Claims with { },
        PairedAt = PairedAt,
        PairedBy = PairedBy,
        TrustUnverifiedEmail = TrustUnverifiedEmail,
        TestedSignIn = TestedSignIn,
        LastResult = LastResult,
        ReconnectNeeded = ReconnectNeeded
    };
}

/// <summary>Known client registration kinds.</summary>
public static class RegistrationKinds
{
    public const string Manual = "manual";
    public const string Pairing = "pairing";
}

/// <summary>Claim names a provider uses for username, display name and email.</summary>
public record OidcClaimNames
{
    public string Username { get; init; } = "preferred_username";
    public string DisplayName { get; init; } = "name";
    public string Email { get; init; } = "email";
}

/// <summary>A passed test sign-in, bound to the connection it was made with.</summary>
public record OidcTestedSignIn(DateTime At, string Fingerprint);

public static class OidcResultKinds
{
    public const string Checks = "checks";
    public const string TestSignIn = "testSignIn";
    public const string SignIn = "signIn";
}

/// <summary>The latest result of checks, a test sign-in or a sign-in at a provider.</summary>
public record OidcLastResult(DateTime At, string Kind, bool Passed, string? Message);

/// <summary>
/// SHA-256 fingerprint of the connection fields (authority, client id, secret, scopes). A test
/// sign-in only counts for the connection it was made with.
/// </summary>
public static class OidcConnectionFingerprint
{
    public static string Compute(OidcProviderSettings provider) =>
        Compute(provider.Authority, provider.ClientId, provider.ClientSecret, provider.Scopes);

    public static string Compute(string authority, string clientId, string? clientSecret, string scopes)
    {
        var material = string.Join('\n',
            authority.Trim().TrimEnd('/'),
            clientId.Trim(),
            clientSecret ?? string.Empty,
            NormalizeScopes(scopes));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string NormalizeScopes(string scopes) =>
        string.Join(' ', scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal));
}

/// <summary>The identity claims extracted from a validated OIDC id token.</summary>
public record OidcUserInfo(
    string Subject,
    string? Email,
    bool EmailVerified,
    string? Username = null,
    string? DisplayName = null);

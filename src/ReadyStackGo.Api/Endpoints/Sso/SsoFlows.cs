using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.API.Endpoints.Sso;

/// <summary>What an OIDC browser round trip is for.</summary>
public enum OidcFlowPurpose
{
    /// <summary>Normal sign-in from the login page.</summary>
    SignIn,

    /// <summary>Test sign-in of the setup flow: stores the received claims, signs nobody in.</summary>
    TestSignIn,

    /// <summary>Sign-in of the setup wizard: creates the first system administrator.</summary>
    WizardAdmin
}

/// <summary>Transient state for an in-flight OIDC authorization-code flow, keyed by its state.</summary>
/// <param name="Snapshot">Provider settings to use instead of rsgo.oidc.json (test sign-in of a setup session).</param>
/// <param name="ContextId">Setup session id (test sign-in) or wizard run id (wizard sign-in).</param>
/// <param name="FlowSecret">Value of the flow cookie the callback must present (null for <see cref="OidcFlowPurpose.SignIn"/>).</param>
public record OidcFlowState(
    string Provider,
    string Nonce,
    string CodeVerifier,
    string RedirectUri,
    OidcFlowPurpose Purpose = OidcFlowPurpose.SignIn,
    string? ContextId = null,
    string? FlowSecret = null,
    OidcProviderSettings? Snapshot = null);

/// <summary>Where a client registration (pairing) returns to.</summary>
public enum RegistrationFlowOwner
{
    SetupSession,
    WizardRun
}

/// <summary>A pairing in flight, keyed by its state.</summary>
public record RegistrationFlowState(RegistrationFlowOwner Owner, string ContextId, string FlowSecret);

/// <summary>
/// Server-side store of the one-time states of browser round trips (10 minutes each,
/// consumed on first use).
/// </summary>
public class SsoFlowStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public SsoFlowStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void PutOidc(string state, OidcFlowState flow) => _cache.Set(OidcKey(state), flow, Lifetime);

    public OidcFlowState? TakeOidc(string state)
    {
        if (!_cache.TryGetValue(OidcKey(state), out OidcFlowState? flow))
        {
            return null;
        }
        _cache.Remove(OidcKey(state));
        return flow;
    }

    public void PutRegistration(string state, RegistrationFlowState flow) => _cache.Set(RegistrationKey(state), flow, Lifetime);

    public RegistrationFlowState? TakeRegistration(string state)
    {
        if (!_cache.TryGetValue(RegistrationKey(state), out RegistrationFlowState? flow))
        {
            return null;
        }
        _cache.Remove(RegistrationKey(state));
        return flow;
    }

    private static string OidcKey(string state) => $"oidc_state:{state}";

    private static string RegistrationKey(string state) => $"sso_registration_state:{state}";
}

/// <summary>
/// The cookie that binds a browser round trip (pairing, test sign-in, wizard) to the browser
/// that started it. HttpOnly, SameSite=Lax (sent on the top-level return from the provider),
/// path /api, Secure on HTTPS.
/// </summary>
public static class SsoFlowCookie
{
    public const string Name = "rsgo_sso_flow";

    /// <summary>Returns the current flow secret of this browser, issuing a new one if there is none.</summary>
    public static string Ensure(HttpContext context, TimeSpan lifetime)
    {
        var existing = Read(context);
        if (!string.IsNullOrEmpty(existing))
        {
            // Refresh the expiry so a long wizard run keeps its cookie.
            Write(context, existing, lifetime);
            return existing;
        }

        var secret = SsoTokens.NewToken();
        Write(context, secret, lifetime);
        return secret;
    }

    public static string? Read(HttpContext context) =>
        context.Request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>Constant-time comparison of the presented cookie with the expected secret.</summary>
    public static bool Matches(HttpContext context, string? expected)
    {
        var presented = Read(context);
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(expected))
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));
    }

    private static void Write(HttpContext context, string value, TimeSpan lifetime)
    {
        context.Response.Cookies.Append(Name, value, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api",
            Secure = context.Request.IsHttps,
            MaxAge = lifetime
        });
    }
}

internal static class SsoTokens
{
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string PkceChallenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

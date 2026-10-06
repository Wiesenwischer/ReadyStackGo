using System.Net;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Rules for the public base URL of the installation (the address users and identity
/// providers reach ReadyStackGo under).
/// </summary>
public static class BaseUrlRules
{
    /// <summary>The value rsgo.system.json carries before anyone set a base URL.</summary>
    public const string UnsetDefault = "http://localhost:5000";

    /// <summary>True if the stored value means "not set" (empty or the built-in default).</summary>
    public static bool IsUnset(string? storedValue) =>
        string.IsNullOrWhiteSpace(storedValue) ||
        string.Equals(storedValue.Trim().TrimEnd('/'), UnsetDefault, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates and normalizes a base URL: absolute http(s), no query, fragment or user
    /// info, no trailing slash. Returns null if the value is not a valid base URL.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    /// <summary>
    /// The HTTPS rule of templates that require it (WYSCH): https, or http on loopback
    /// (localhost, 127.0.0.0/8, [::1]).
    /// </summary>
    public static bool SatisfiesHttpsRequirement(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        if (uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }

    /// <summary>The redirect URI of a provider: &lt;base URL&gt;/api/auth/oidc/&lt;name&gt;/callback.</summary>
    public static string ProviderRedirectUri(string baseUrl, string providerName) =>
        $"{baseUrl.TrimEnd('/')}/api/auth/oidc/{providerName}/callback";
}

public static class SystemConfigBaseUrlExtensions
{
    /// <summary>
    /// The base URL in the form every sign-in path uses (normalized like the setup does), so
    /// "Add provider", the test sign-in and the real sign-in build the same redirect URI.
    /// </summary>
    public static async Task<string> GetEffectiveBaseUrlAsync(this ISystemConfigService systemConfig)
    {
        var raw = await systemConfig.GetBaseUrlAsync();
        return BaseUrlRules.Normalize(raw) ?? raw.TrimEnd('/');
    }
}

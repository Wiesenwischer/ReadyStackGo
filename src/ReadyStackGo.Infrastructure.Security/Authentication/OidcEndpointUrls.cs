using System.Diagnostics.CodeAnalysis;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Endpoints from a provider's discovery document are used as navigation and request targets,
/// so only absolute http and https URLs are accepted (no javascript:, data: or relative URLs).
/// </summary>
internal static class OidcEndpointUrls
{
    public static bool IsHttp([NotNullWhen(true)] string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

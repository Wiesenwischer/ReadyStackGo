using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Maps a manifest <see cref="RsgoEdge"/> block (StackManagement domain) to a resolved
/// <see cref="EdgeConfig"/> (Deployment domain), applying defaults and resolving
/// <c>${VAR}</c> placeholders against the deployment's variable dictionary.
///
/// Returns <c>null</c> when the edge block is absent, disabled, or cannot be resolved —
/// in which case the edge feature stays completely inert for the product.
/// </summary>
public static class EdgeConfigMapper
{
    /// <param name="bundleHtml">
    /// Pre-resolved maintenance-page HTML for <c>bundle</c> mode (read from the manifest bundle
    /// at deploy time by the caller, since the mapper is pure and has no file access).
    /// </param>
    public static EdgeConfig? Map(
        RsgoEdge? source,
        IReadOnlyDictionary<string, string> variables,
        string? bundleHtml = null)
    {
        if (source is null || !source.Enabled)
            return null;

        var publicHostname = Resolve(source.PublicHostname, variables);
        if (string.IsNullOrWhiteSpace(publicHostname))
            return null; // hostname is mandatory and could not be resolved

        var upstreamService = Resolve(source.Upstream?.Service, variables);
        if (string.IsNullOrWhiteSpace(upstreamService))
            return null; // upstream service is mandatory

        var network = Resolve(source.Network, variables);
        if (string.IsNullOrWhiteSpace(network))
            return null; // shared edge<->upstream network is mandatory

        var image = Resolve(source.Image, variables);
        if (string.IsNullOrWhiteSpace(image))
            image = EdgeConstants.DefaultCaddyImage;

        var publicPort = source.PublicPort ?? EdgeConstants.DefaultPublicPort;
        var upstreamPort = source.Upstream?.Port ?? EdgeConstants.DefaultUpstreamPort;

        var tlsMode = ParseTlsMode(source.Tls?.Mode);
        var tlsCertRef = Resolve(source.Tls?.CertRef, variables);
        var letsEncryptEmail = Resolve(source.Tls?.LetsEncrypt?.Email, variables);
        var letsEncryptDnsChallenge = Resolve(source.Tls?.LetsEncrypt?.DnsChallenge, variables);

        var pageMode = ParsePageMode(source.MaintenancePage?.Mode);
        var bundlePath = Resolve(source.MaintenancePage?.BundlePath, variables);
        var maintenanceContainerService = Resolve(source.MaintenancePage?.Container?.Service, variables);
        var maintenanceContainerPort = source.MaintenancePage?.Container?.Port ?? 80;

        var branding = MapBranding(source.MaintenancePage?.Branding, variables);

        try
        {
            return EdgeConfig.Create(
                publicHostname,
                publicPort,
                upstreamService,
                upstreamPort,
                network,
                image,
                tlsMode,
                tlsCertRef,
                letsEncryptEmail,
                letsEncryptDnsChallenge,
                pageMode,
                bundlePath,
                maintenanceContainerService,
                maintenanceContainerPort,
                bundleHtml,
                branding);
        }
        catch (ArgumentException)
        {
            // Invalid manifest values (e.g. out-of-range port) → feature stays inert.
            return null;
        }
    }

    private static EdgeBranding MapBranding(
        RsgoEdgeBranding? source,
        IReadOnlyDictionary<string, string> variables)
    {
        if (source is null)
            return EdgeBranding.Empty;

        return new EdgeBranding(
            Resolve(source.ProductName, variables),
            Resolve(source.LogoUrl, variables),
            Resolve(source.SupportContact, variables),
            source.Locales);
    }

    private static EdgeTlsMode ParseTlsMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "reuse" => EdgeTlsMode.Reuse,
        "selfsigned" => EdgeTlsMode.SelfSigned,
        "custom" => EdgeTlsMode.Custom,
        "letsencrypt" => EdgeTlsMode.LetsEncrypt,
        _ => EdgeTlsMode.None
    };

    private static EdgeMaintenancePageMode ParsePageMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "bundle" => EdgeMaintenancePageMode.Bundle,
        "container" => EdgeMaintenancePageMode.Container,
        _ => EdgeMaintenancePageMode.Default
    };

    /// <summary>
    /// Resolves <c>${VAR}</c> placeholders. Returns null when a placeholder remains
    /// unresolved (mandatory fields treat that as "could not configure").
    /// </summary>
    private static string? Resolve(string? template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;
        foreach (var kvp in variables)
            result = result.Replace($"${{{kvp.Key}}}", kvp.Value);

        return result.Contains("${") ? null : result;
    }
}

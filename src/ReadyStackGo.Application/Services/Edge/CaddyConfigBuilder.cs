using System.Text;
using System.Text.Json;
using ReadyStackGo.Domain.Deployment.Edge;

namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Pure builder that turns an <see cref="EdgeConfig"/> plus a computed
/// <see cref="EdgeDesiredState"/> into a complete Caddy JSON configuration document,
/// ready to be pushed to the Caddy admin API (<c>POST /load</c>).
///
/// No IO, no Docker — fully unit-testable. RSGO is the single writer of the edge config
/// (locked decision §6). Phase 1 terminates plain HTTP on the public port; TLS termination
/// is layered on in Phase 2.
///
/// Routing contract:
/// <list type="bullet">
/// <item><c>proxy</c> → everything is <c>reverse_proxy</c>'d to the upstream; <c>/__status</c>
/// reports <c>running</c>.</item>
/// <item><c>maintenance</c> → <c>/hc</c> and <c>/liveness</c> always pass through to the
/// upstream; <c>/__status</c> returns the machine-readable status; everything else returns
/// a controlled 503 maintenance page.</item>
/// </list>
/// </summary>
public static class CaddyConfigBuilder
{
    /// <summary>Health/liveness paths that always pass through to the upstream.</summary>
    public static readonly string[] HealthPassthroughPaths = { "/hc", "/hc/*", "/liveness", "/liveness/*" };

    /// <summary>Stable status endpoint path.</summary>
    public const string StatusPath = "/__status";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    /// <summary>
    /// Builds the full Caddy JSON config for the given edge + desired state.
    /// </summary>
    /// <param name="tls">
    /// Optional TLS material. When provided (and <see cref="EdgeConfig.TlsMode"/> is not
    /// <see cref="EdgeTlsMode.None"/>) the edge terminates HTTPS on the public port using this
    /// RSGO-managed certificate, injected inline (no ACME in Caddy). When null the edge serves
    /// plain HTTP (Phase 1 behaviour).
    /// </param>
    public static string Build(EdgeConfig config, EdgeDesiredState desired, EdgeCertMaterial? tls = null)
    {
        var routes = desired.Mode == EdgeMode.Proxy
            ? BuildProxyRoutes(config, desired)
            : BuildMaintenanceRoutes(config, desired);

        var terminatesTls = tls != null && config.TlsMode != EdgeTlsMode.None;

        var edgeServer = new Dictionary<string, object?>
        {
            ["listen"] = new[] { $":{config.PublicPort}" },
            ["routes"] = routes
        };

        var apps = new Dictionary<string, object?>
        {
            ["http"] = new Dictionary<string, object?>
            {
                ["servers"] = new Dictionary<string, object?> { ["edge"] = edgeServer }
            }
        };

        if (terminatesTls)
        {
            // Terminate TLS with the RSGO-managed cert; never let Caddy run ACME itself.
            // default_sni ensures our cert is selected even when a client connects by IP / sends
            // no (or a non-matching) SNI.
            edgeServer["automatic_https"] = new Dictionary<string, object?> { ["disable"] = true };
            edgeServer["tls_connection_policies"] = new[]
            {
                new Dictionary<string, object?> { ["default_sni"] = config.PublicHostname }
            };

            apps["tls"] = new Dictionary<string, object?>
            {
                ["certificates"] = new Dictionary<string, object?>
                {
                    ["load_pem"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["certificate"] = tls!.CertificatePem,
                            ["key"] = tls.PrivateKeyPem
                        }
                    }
                }
            };
        }

        var root = new Dictionary<string, object?>
        {
            ["admin"] = new Dictionary<string, object?>
            {
                ["listen"] = $"0.0.0.0:{EdgeConstants.CaddyAdminPort}"
            },
            ["apps"] = apps
        };

        return JsonSerializer.Serialize(root, SerializerOptions);
    }

    private static List<object> BuildProxyRoutes(EdgeConfig config, EdgeDesiredState desired)
    {
        return new List<object>
        {
            StatusRoute(desired),
            ReverseProxyRoute(config, matchPaths: null)
        };
    }

    private static List<object> BuildMaintenanceRoutes(EdgeConfig config, EdgeDesiredState desired)
    {
        return new List<object>
        {
            // Health endpoints always pass through to the upstream (even during maintenance).
            ReverseProxyRoute(config, matchPaths: HealthPassthroughPaths),
            StatusRoute(desired),
            MaintenancePageRoute(config, desired)
        };
    }

    private static object ReverseProxyRoute(EdgeConfig config, string[]? matchPaths)
    {
        var route = new Dictionary<string, object?>
        {
            ["handle"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["handler"] = "reverse_proxy",
                    ["upstreams"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["dial"] = $"{config.UpstreamService}:{config.UpstreamPort}"
                        }
                    }
                }
            }
        };

        if (matchPaths is { Length: > 0 })
        {
            route["match"] = new[]
            {
                new Dictionary<string, object?> { ["path"] = matchPaths }
            };
        }

        return route;
    }

    private static object StatusRoute(EdgeDesiredState desired)
    {
        return new Dictionary<string, object?>
        {
            ["match"] = new[]
            {
                new Dictionary<string, object?> { ["path"] = new[] { StatusPath } }
            },
            ["handle"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["handler"] = "static_response",
                    ["status_code"] = 200,
                    ["headers"] = new Dictionary<string, object?>
                    {
                        ["Content-Type"] = new[] { "application/json" }
                    },
                    ["body"] = BuildStatusJson(desired)
                }
            }
        };
    }

    private static object MaintenancePageRoute(EdgeConfig config, EdgeDesiredState desired)
    {
        return new Dictionary<string, object?>
        {
            ["handle"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["handler"] = "static_response",
                    ["status_code"] = 503,
                    ["headers"] = new Dictionary<string, object?>
                    {
                        ["Content-Type"] = new[] { "text/html; charset=utf-8" },
                        ["Retry-After"] = new[] { "120" }
                    },
                    ["body"] = EdgeMaintenancePage.RenderDefault(config, desired)
                }
            }
        };
    }

    /// <summary>
    /// Builds the stable, versioned status JSON (schema 1). Identical regardless of branding
    /// stage so a client/launcher can parse it robustly.
    /// </summary>
    public static string BuildStatusJson(EdgeDesiredState desired)
    {
        var payload = new Dictionary<string, object?>
        {
            ["schema"] = 1,
            ["state"] = desired.StatusStateToken,
            ["reason"] = desired.Reason,
            ["until"] = desired.Until,
            ["productVersion"] = desired.ProductVersion
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}

/// <summary>
/// Renders the default (RSGO standard) maintenance page. Themeable through
/// <see cref="EdgeBranding"/>; the wording is driven purely by
/// <see cref="EdgeDesiredState.PlannedMaintenance"/> (planned vs. temporarily unavailable).
/// </summary>
public static class EdgeMaintenancePage
{
    public static string RenderDefault(EdgeConfig config, EdgeDesiredState desired)
    {
        var productName = string.IsNullOrWhiteSpace(config.Branding.ProductName)
            ? config.PublicHostname
            : config.Branding.ProductName!;

        var (titleEn, titleDe, bodyEn, bodyDe) = desired.PlannedMaintenance
            ? ("Scheduled maintenance",
               "Geplante Wartung",
               "This service is temporarily down for scheduled maintenance. Please try again shortly.",
               "Dieser Dienst ist vorübergehend wegen geplanter Wartungsarbeiten nicht erreichbar. Bitte versuchen Sie es in Kürze erneut.")
            : ("Temporarily unavailable",
               "Vorübergehend nicht erreichbar",
               "This service is temporarily unavailable and will be back shortly.",
               "Dieser Dienst ist vorübergehend nicht erreichbar und ist in Kürze wieder verfügbar.");

        var logo = string.IsNullOrWhiteSpace(config.Branding.LogoUrl)
            ? string.Empty
            : $"<img class=\"logo\" src=\"{Escape(config.Branding.LogoUrl!)}\" alt=\"{Escape(productName)}\" />";

        var support = string.IsNullOrWhiteSpace(config.Branding.SupportContact)
            ? string.Empty
            : $"<p class=\"support\">Support: {Escape(config.Branding.SupportContact!)}</p>";

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append($"<title>{Escape(productName)} — {Escape(titleEn)}</title>");
        sb.Append("<style>");
        sb.Append("body{font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#0f172a;color:#e2e8f0;margin:0;display:flex;min-height:100vh;align-items:center;justify-content:center;text-align:center;padding:2rem}");
        sb.Append(".card{max-width:32rem}");
        sb.Append(".logo{max-width:160px;margin-bottom:1.5rem}");
        sb.Append("h1{font-size:1.6rem;margin:.5rem 0}");
        sb.Append("p{color:#94a3b8;line-height:1.5}");
        sb.Append(".de{margin-top:1rem;opacity:.75;font-size:.95rem}");
        sb.Append(".support{margin-top:1.5rem;font-size:.85rem}");
        sb.Append("</style></head><body><div class=\"card\">");
        sb.Append(logo);
        sb.Append($"<h1>{Escape(titleEn)}</h1>");
        sb.Append($"<p>{Escape(bodyEn)}</p>");
        sb.Append($"<p class=\"de\"><strong>{Escape(titleDe)}.</strong> {Escape(bodyDe)}</p>");
        sb.Append(support);
        sb.Append("</div></body></html>");

        return sb.ToString();
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}

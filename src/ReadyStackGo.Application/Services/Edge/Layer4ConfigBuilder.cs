using System.Text.Json;

namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// One SNI route: TLS connections with <see cref="Hostname"/> as SNI are passed through (L4,
/// no termination) to <see cref="DialTarget"/> (e.g. <c>my-edge:443</c>).
/// </summary>
public sealed record SniRoute(string Hostname, string DialTarget);

/// <summary>
/// Pure builder for the shared SNI passthrough router's Caddy Layer-4 config. The router peeks
/// the TLS ClientHello SNI and proxies the raw TCP stream to the matching edge — it never
/// terminates TLS, so every product edge keeps its own certificate.
///
/// Requires the Caddy <c>layer4</c> module in the router image.
/// </summary>
public static class Layer4ConfigBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static string Build(IReadOnlyList<SniRoute> routes, int listenPort, int adminPort)
    {
        var l4Routes = routes.Select(r => (object)new Dictionary<string, object?>
        {
            ["match"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["tls"] = new Dictionary<string, object?>
                    {
                        ["sni"] = new[] { r.Hostname }
                    }
                }
            },
            ["handle"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["handler"] = "proxy",
                    ["upstreams"] = new[]
                    {
                        new Dictionary<string, object?> { ["dial"] = new[] { r.DialTarget } }
                    }
                }
            }
        }).ToList();

        var root = new Dictionary<string, object?>
        {
            ["admin"] = new Dictionary<string, object?> { ["listen"] = $"0.0.0.0:{adminPort}" },
            ["apps"] = new Dictionary<string, object?>
            {
                ["layer4"] = new Dictionary<string, object?>
                {
                    ["servers"] = new Dictionary<string, object?>
                    {
                        ["sni"] = new Dictionary<string, object?>
                        {
                            ["listen"] = new[] { $":{listenPort}" },
                            ["routes"] = l4Routes
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(root, SerializerOptions);
    }
}

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
            // /__status is always edge-served and identical across all branding modes.
            StatusRoute(desired),
            // Catch-all maintenance experience, resolved by branding mode (container → bundle → default).
            MaintenanceCatchAllRoute(config, desired)
        };
    }

    /// <summary>
    /// Builds the catch-all maintenance route honouring the branding resolution order
    /// (container → bundle → default), falling back to the next stage when a stage is not
    /// configured. The status contract (<see cref="StatusRoute"/>) is unaffected.
    /// </summary>
    private static object MaintenanceCatchAllRoute(EdgeConfig config, EdgeDesiredState desired)
    {
        // 1. Container: proxy to the product-contributed maintenance-page container.
        if (config.MaintenancePageMode == EdgeMaintenancePageMode.Container &&
            !string.IsNullOrWhiteSpace(config.MaintenanceContainerService))
        {
            return new Dictionary<string, object?>
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
                                ["dial"] = $"{config.MaintenanceContainerService}:{config.MaintenanceContainerPort}"
                            }
                        }
                    }
                }
            };
        }

        // 2. Bundle: serve the manifest-provided HTML inline.
        if (config.MaintenancePageMode == EdgeMaintenancePageMode.Bundle &&
            !string.IsNullOrWhiteSpace(config.BundleHtml))
        {
            return StaticPageRoute(config.BundleHtml!);
        }

        // 3. Default: RSGO standard page (also the fallback when container/bundle is misconfigured).
        return StaticPageRoute(EdgeMaintenancePage.RenderDefault(config, desired));
    }

    private static object StaticPageRoute(string html)
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
                    ["body"] = html
                }
            }
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

    /// <summary>
    /// Builds the stable, versioned status JSON (schema 1). Identical regardless of branding
    /// stage so a client/launcher can parse it robustly.
    /// </summary>
    public static string BuildStatusJson(EdgeDesiredState desired)
    {
        var payload = new Dictionary<string, object?>
        {
            ["schema"] = EdgeStatusContract.SchemaVersion,
            ["state"] = desired.StatusStateToken,
            ["reason"] = desired.Reason,
            ["until"] = desired.Until,
            ["productVersion"] = desired.ProductVersion
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}

/// <summary>
/// Renders the default (RSGO standard) maintenance page — an "instrument panel" that
/// surfaces the live <c>/__status</c> contract in RSGO brand colours. Themeable through
/// <see cref="EdgeBranding"/> (product name, logo, support contact, locales); wording and
/// accent colour follow <see cref="EdgeDesiredState.StatusStateToken"/>
/// (maintenance → amber, deploying → brand-blue).
///
/// The page is server-rendered once with the current state for a correct no-JS fallback,
/// then keeps itself live in the browser: it polls <c>/__status</c> every 5s, re-renders
/// the panel, ticks the <c>until</c> countdown, and reloads into the real app the moment the
/// product reports <c>running</c>. <c>reason</c>/<c>until</c> rows auto-hide while their data
/// is absent (today <c>until</c> is always null until edge issue #436 populates it).
/// </summary>
public static class EdgeMaintenancePage
{
    /// <summary>Locales we ship copy for. Branding locales are filtered to this set.</summary>
    private static readonly string[] SupportedLocales = { "de", "en" };

    /// <summary>Brand accent per status token (RSGO design system).</summary>
    private static string Accent(string state) => state switch
    {
        "deploying" => "#7592FF",   // brand-400
        "running" => "#32D583",     // success-400
        _ => "#FDB022"              // warning-400 → maintenance
    };

    public static string RenderDefault(EdgeConfig config, EdgeDesiredState desired)
    {
        var productName = string.IsNullOrWhiteSpace(config.Branding.ProductName)
            ? config.PublicHostname
            : config.Branding.ProductName!;

        // Locale resolution: keep manifest order, drop unsupported, de-dupe; default to en+de.
        var locales = config.Branding.Locales
            .Select(l => l?.Trim().ToLowerInvariant())
            .Where(l => !string.IsNullOrEmpty(l) && SupportedLocales.Contains(l))
            .Distinct()
            .Cast<string>()
            .ToList();
        if (locales.Count == 0)
            locales = new List<string> { "en", "de" };

        var primary = locales[0];
        var state = desired.StatusStateToken;              // "maintenance" | "deploying"
        var copy = Copy(primary, state);

        // Live model consumed by the in-page renderer (System.Text.Json escapes <,>,& → safe in <script>).
        var modelJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["productName"] = productName,
            ["state"] = state,
            ["reason"] = desired.Reason,
            ["until"] = desired.Until,
            ["productVersion"] = desired.ProductVersion,
            ["support"] = string.IsNullOrWhiteSpace(config.Branding.SupportContact) ? null : config.Branding.SupportContact,
            ["logoUrl"] = string.IsNullOrWhiteSpace(config.Branding.LogoUrl) ? null : config.Branding.LogoUrl
        });
        var localesJson = JsonSerializer.Serialize(locales);

        // Language toggle buttons (first = primary, pressed); hidden when only one locale.
        var langButtons = string.Concat(locales.Select((l, i) =>
            $"<button data-lang=\"{l}\" aria-pressed=\"{(i == 0 ? "true" : "false")}\">{l.ToUpperInvariant()}</button>"));
        var langGroupStyle = locales.Count > 1 ? string.Empty : "display:none";

        var reasonStyle = string.IsNullOrWhiteSpace(desired.Reason) ? "display:none" : string.Empty;
        var untilStyle = string.IsNullOrWhiteSpace(desired.Until) ? "display:none" : string.Empty;
        var supportHtml = string.IsNullOrWhiteSpace(config.Branding.SupportContact)
            ? string.Empty
            : $"{Escape(copy.support)}: <a href=\"mailto:{Escape(config.Branding.SupportContact!)}\">{Escape(config.Branding.SupportContact!)}</a>";

        var headWord = state == "deploying" ? "Deploying" : "Maintenance";

        return Template
            .Replace("{{LANG}}", Escape(primary))
            .Replace("{{TITLE}}", $"{Escape(headWord)} — {Escape(productName)}")
            .Replace("{{ACCENT}}", Accent(state))
            .Replace("{{BRANDNAME}}", Escape(productName))
            .Replace("{{CHIP}}", Escape(copy.chip))
            .Replace("{{EYEBROW}}", Escape(copy.eyebrow))
            .Replace("{{H1}}", Escape(copy.title))
            .Replace("{{LEDE}}", Escape(copy.lede))
            .Replace("{{KSTATE}}", Escape(copy.kState))
            .Replace("{{KREASON}}", Escape(copy.kReason))
            .Replace("{{KUNTIL}}", Escape(copy.kUntil))
            .Replace("{{KVERSION}}", Escape(copy.kVersion))
            .Replace("{{VSTATE}}", Escape(state))
            .Replace("{{VREASON}}", Escape(desired.Reason ?? "—"))
            .Replace("{{VVERSION}}", Escape(desired.ProductVersion ?? "—"))
            .Replace("{{ROWREASONSTYLE}}", reasonStyle)
            .Replace("{{ROWUNTILSTYLE}}", untilStyle)
            .Replace("{{SUPPORTHTML}}", supportHtml)
            .Replace("{{REFRESHTEXT}}", Escape(copy.refresh))
            .Replace("{{LANGBUTTONS}}", langButtons)
            .Replace("{{LANGGROUPSTYLE}}", langGroupStyle)
            .Replace("{{MODEL}}", modelJson)
            .Replace("{{LOCALES}}", localesJson);
    }

    private sealed record CopyStrings(
        string chip, string eyebrow, string title, string lede,
        string kState, string kReason, string kUntil, string kVersion,
        string refresh, string support);

    /// <summary>
    /// Server-side copy for the no-JS fallback, mirroring the in-page <c>COPY</c> table for the
    /// primary locale and current state. The browser keeps the live copy table for toggling.
    /// </summary>
    private static CopyStrings Copy(string lang, string state)
    {
        var deploying = state == "deploying";
        return lang == "de"
            ? new CopyStrings(
                chip: deploying ? "Update läuft" : "Wartung",
                eyebrow: deploying ? "Rollout · läuft" : "Geplant · Scheduled",
                title: deploying ? "Update wird ausgerollt" : "Geplante Wartung",
                lede: deploying
                    ? "Eine neue Version geht gerade live. Das dauert meist nur einen Moment."
                    : "Wir spielen ein geplantes Update ein. Der Dienst ist in Kürze wieder erreichbar.",
                kState: "Zustand", kReason: "Grund", kUntil: "Voraussichtlich bis", kVersion: "Version",
                refresh: "prüft automatisch", support: "Support")
            : new CopyStrings(
                chip: deploying ? "Deploying" : "Maintenance",
                eyebrow: deploying ? "Rollout in progress" : "Scheduled",
                title: deploying ? "Rolling out an update" : "Scheduled maintenance",
                lede: deploying
                    ? "A new version is going live. This usually takes just a moment."
                    : "We're applying a scheduled update. The service will be back shortly.",
                kState: "state", kReason: "reason", kUntil: "until", kVersion: "version",
                refresh: "auto-checking", support: "Support");
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    // Self-contained document. Placeholders ({{X}}) are replaced above; CSS/JS use single braces
    // only, so they never collide with the {{...}} tokens. A 4-quote raw literal allows the many
    // double quotes in the markup/script without escaping.
    private const string Template = """"
<!doctype html><html lang="{{LANG}}"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{TITLE}}</title>
<style>
  :root {
    --ground:#0B1020; --ground-2:#131A2B; --panel:#182238;
    --text:#ECF1F8; --muted:#98A2B3; --line:rgba(255,255,255,.08);
    --brand:#7592FF; --amber:#FDB022; --blue:#7592FF; --mint:#32D583;
    --accent:{{ACCENT}};
    --font-sans:system-ui,-apple-system,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
    --font-mono:ui-monospace,"Cascadia Code","JetBrains Mono","SF Mono",Menlo,Consolas,monospace;
  }
  *{box-sizing:border-box;}
  html,body{height:100%;}
  body{margin:0;background:var(--ground);color:var(--text);font-family:var(--font-sans);
    -webkit-font-smoothing:antialiased;text-rendering:optimizeLegibility;min-height:100dvh;
    display:grid;place-items:center;padding:clamp(1.25rem,4vw,3rem);overflow:hidden;}
  #bg{position:fixed;inset:0;width:100%;height:100%;z-index:0;opacity:.55;}
  .vignette{position:fixed;inset:0;z-index:1;pointer-events:none;
    background:radial-gradient(120% 90% at 50% -10%,rgba(117,146,255,.12),transparent 55%),
    radial-gradient(100% 80% at 50% 120%,rgba(11,16,32,.9),transparent 60%);}
  .card{position:relative;z-index:2;width:min(40rem,100%);display:flex;flex-direction:column;
    gap:clamp(1.5rem,3.5vw,2.5rem);}
  .topbar{display:flex;align-items:center;justify-content:space-between;gap:1rem;}
  .brand{display:flex;align-items:center;gap:.7rem;min-width:0;}
  .brand .mark{width:34px;height:34px;flex:none;border-radius:9px;display:grid;place-items:center;
    overflow:hidden;background:linear-gradient(160deg,rgba(117,146,255,.22),rgba(117,146,255,.06));
    border:1px solid var(--line);}
  .brand .mark svg{width:19px;height:19px;display:block;}
  .brand .mark img{width:100%;height:100%;object-fit:contain;}
  .brand .name{font-weight:560;letter-spacing:-.01em;font-size:1rem;white-space:nowrap;
    overflow:hidden;text-overflow:ellipsis;}
  .chip{font-family:var(--font-mono);font-size:.72rem;letter-spacing:.12em;text-transform:uppercase;
    display:inline-flex;align-items:center;gap:.55rem;flex:none;padding:.42rem .7rem;border-radius:999px;
    color:var(--accent);background:color-mix(in srgb,var(--accent) 12%,transparent);
    border:1px solid color-mix(in srgb,var(--accent) 35%,transparent);}
  .chip .dot{width:7px;height:7px;border-radius:50%;background:var(--accent);
    animation:pulse 2.4s ease-out infinite;}
  @keyframes pulse{0%{box-shadow:0 0 0 0 color-mix(in srgb,var(--accent) 55%,transparent);}
    70%{box-shadow:0 0 0 9px transparent;}100%{box-shadow:0 0 0 0 transparent;}}
  .eyebrow{font-family:var(--font-mono);font-size:.74rem;letter-spacing:.16em;text-transform:uppercase;
    color:var(--accent);margin:0 0 .85rem;}
  h1{margin:0;font-weight:600;letter-spacing:-.025em;line-height:1.04;font-size:clamp(2.1rem,6vw,3.35rem);}
  .lede{margin:1rem 0 0;color:var(--muted);font-size:clamp(1rem,2.2vw,1.12rem);line-height:1.5;max-width:34ch;}
  .panel{border:1px solid var(--line);border-radius:14px;overflow:hidden;
    background:linear-gradient(180deg,var(--ground-2),color-mix(in srgb,var(--ground-2) 60%,var(--ground)));}
  .panel-head{display:flex;align-items:center;justify-content:space-between;padding:.7rem 1rem;
    border-bottom:1px solid var(--line);font-family:var(--font-mono);font-size:.68rem;letter-spacing:.14em;
    text-transform:uppercase;color:var(--muted);}
  .panel-head .src{color:color-mix(in srgb,var(--muted) 75%,transparent);}
  .rows{display:grid;}
  .row{display:grid;grid-template-columns:7.5rem 1fr;gap:1rem;align-items:baseline;padding:.68rem 1rem;
    font-family:var(--font-mono);font-size:.9rem;}
  .row+.row{border-top:1px solid var(--line);}
  .row .k{color:var(--muted);font-size:.76rem;letter-spacing:.06em;text-transform:uppercase;}
  .row .v{color:var(--text);overflow-wrap:anywhere;}
  .row .v.accent{color:var(--accent);}
  .row .v .sub{color:var(--muted);font-size:.82em;}
  .foot{display:flex;align-items:center;justify-content:space-between;gap:1rem;flex-wrap:wrap;
    color:var(--muted);font-size:.85rem;}
  .foot a{color:var(--text);text-decoration:none;border-bottom:1px solid var(--line);padding-bottom:1px;}
  .foot a:hover{border-color:var(--accent);}
  .refresh{display:inline-flex;align-items:center;gap:.5rem;font-family:var(--font-mono);font-size:.76rem;
    letter-spacing:.04em;}
  .refresh .spin{width:12px;height:12px;border-radius:50%;border:1.5px solid var(--line);
    border-top-color:var(--accent);animation:spin 1.5s linear infinite;}
  @keyframes spin{to{transform:rotate(360deg);}}
  .lang{display:inline-flex;gap:.15rem;font-family:var(--font-mono);font-size:.78rem;}
  .lang button{background:none;border:none;color:var(--muted);cursor:pointer;padding:.1rem .3rem;
    border-radius:5px;font:inherit;}
  .lang button[aria-pressed="true"]{color:var(--text);background:var(--line);}
  .lang button:focus-visible,.foot a:focus-visible{outline:2px solid var(--accent);outline-offset:2px;
    border-radius:4px;}
  @media (max-width:460px){.row{grid-template-columns:1fr;gap:.15rem;}
    .topbar{flex-direction:column;align-items:flex-start;}}
  @media (prefers-reduced-motion:reduce){.chip .dot,.refresh .spin{animation:none;}}
</style></head>
<body>
<canvas id="bg" aria-hidden="true"></canvas>
<div class="vignette" aria-hidden="true"></div>
<main class="card" role="main" aria-live="polite">
  <div class="topbar">
    <div class="brand">
      <span class="mark" id="markBox" aria-hidden="true">
        <svg id="markSvg" viewBox="0 0 24 24" fill="none" stroke="#A9BBFF" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
          <path d="M5 14c-1.5 1-2 5-2 5s4-.5 5-2"/>
          <path d="M12 15l-3-3c1-5 5-9 10-9 0 5-4 9-9 10z"/>
          <circle cx="14.5" cy="9.5" r="1.4" fill="#A9BBFF" stroke="none"/>
        </svg>
      </span>
      <span class="name" id="brandName">{{BRANDNAME}}</span>
    </div>
    <span class="chip" id="chip"><span class="dot"></span><span id="chipText">{{CHIP}}</span></span>
  </div>
  <div>
    <p class="eyebrow" id="eyebrow">{{EYEBROW}}</p>
    <h1 id="title">{{H1}}</h1>
    <p class="lede" id="lede">{{LEDE}}</p>
  </div>
  <section class="panel" aria-label="Status">
    <div class="panel-head"><span>Status</span><span class="src">GET /__status</span></div>
    <div class="rows">
      <div class="row"><span class="k" id="kState">{{KSTATE}}</span><span class="v accent" id="vState">{{VSTATE}}</span></div>
      <div class="row" id="rowReason" style="{{ROWREASONSTYLE}}"><span class="k" id="kReason">{{KREASON}}</span><span class="v" id="vReason">{{VREASON}}</span></div>
      <div class="row" id="rowUntil" style="{{ROWUNTILSTYLE}}"><span class="k" id="kUntil">{{KUNTIL}}</span><span class="v" id="vUntil">—</span></div>
      <div class="row"><span class="k" id="kVersion">{{KVERSION}}</span><span class="v" id="vVersion">{{VVERSION}}</span></div>
    </div>
  </section>
  <div class="foot">
    <span id="support">{{SUPPORTHTML}}</span>
    <span style="display:inline-flex;gap:1rem;align-items:center;">
      <span class="refresh"><span class="spin"></span><span id="refreshText">{{REFRESHTEXT}}</span></span>
      <span class="lang" role="group" aria-label="Language" style="{{LANGGROUPSTYLE}}">{{LANGBUTTONS}}</span>
    </span>
  </div>
</main>
<script>
(() => {
  const ACCENT = { maintenance:"#FDB022", deploying:"#7592FF", running:"#32D583" };
  const COPY = {
    de: {
      chip:{maintenance:"Wartung",deploying:"Update läuft",running:"Online"},
      eyebrow:{maintenance:"Geplant · Scheduled",deploying:"Rollout · läuft",running:"Wieder erreichbar"},
      title:{maintenance:"Geplante Wartung",deploying:"Update wird ausgerollt",running:"Wieder online"},
      lede:{maintenance:"Wir spielen ein geplantes Update ein. Der Dienst ist in Kürze wieder erreichbar.",
        deploying:"Eine neue Version geht gerade live. Das dauert meist nur einen Moment.",
        running:"Der Dienst läuft wieder. Wir leiten dich gleich weiter …"},
      k:{state:"Zustand",reason:"Grund",until:"Voraussichtlich bis",version:"Version"},
      refresh:"prüft automatisch",support:"Support",relIn:(s)=>"noch "+s,soon:"gleich",
    },
    en: {
      chip:{maintenance:"Maintenance",deploying:"Deploying",running:"Online"},
      eyebrow:{maintenance:"Scheduled",deploying:"Rollout in progress",running:"Back online"},
      title:{maintenance:"Scheduled maintenance",deploying:"Rolling out an update",running:"Back online"},
      lede:{maintenance:"We're applying a scheduled update. The service will be back shortly.",
        deploying:"A new version is going live. This usually takes just a moment.",
        running:"The service is back. Taking you there …"},
      k:{state:"state",reason:"reason",until:"until",version:"version"},
      refresh:"auto-checking",support:"Support",relIn:(s)=>s+" left",soon:"any moment",
    },
  };
  const model = {{MODEL}};
  const LOCALES = {{LOCALES}};
  let lang = COPY[LOCALES[0]] ? LOCALES[0] : "en";

  const $ = (id) => document.getElementById(id);
  const setVar = (v) => document.documentElement.style.setProperty("--accent", v);

  function fmtUntil() {
    if (!model.until) return "—";
    const d = new Date(model.until); if (isNaN(d)) return "—";
    const t = d.toLocaleTimeString(lang === "de" ? "de-DE" : "en-GB", { hour:"2-digit", minute:"2-digit" });
    const ms = d - Date.now();
    if (ms <= 0) return t;
    const m = Math.floor(ms/60000), s = Math.floor((ms%60000)/1000);
    const rel = m > 0 ? (m + ":" + String(s).padStart(2,"0") + " min") : COPY[lang].soon;
    return t + "  <span class=\"sub\">(" + COPY[lang].relIn(rel) + ")</span>";
  }

  function render() {
    const c = COPY[lang] || COPY.en, st = model.state in ACCENT ? model.state : "maintenance";
    setVar(ACCENT[st]);
    $("brandName").textContent = model.productName;
    if (model.logoUrl) {
      $("markSvg").style.display = "none";
      let img = $("markImg");
      if (!img) { img = document.createElement("img"); img.id = "markImg"; img.alt = ""; $("markBox").appendChild(img); }
      img.src = model.logoUrl;
    }
    $("chipText").textContent = c.chip[st];
    $("eyebrow").textContent = c.eyebrow[st];
    $("title").textContent = c.title[st];
    $("lede").textContent = c.lede[st];
    $("vState").textContent = st;
    $("kState").textContent = c.k.state; $("kReason").textContent = c.k.reason;
    $("kUntil").textContent = c.k.until; $("kVersion").textContent = c.k.version;
    $("rowReason").style.display = model.reason ? "" : "none";
    $("vReason").textContent = model.reason || "—";
    $("rowUntil").style.display = model.until ? "" : "none";
    $("vUntil").innerHTML = fmtUntil();
    $("vVersion").textContent = model.productVersion || "—";
    $("refreshText").textContent = c.refresh;
    $("support").innerHTML = model.support
      ? (c.support + ": <a href=\"mailto:" + model.support + "\">" + model.support + "</a>")
      : "";
    document.documentElement.lang = lang;
    document.title = c.chip[st] + " — " + model.productName;
  }

  Array.from(document.querySelectorAll(".lang button")).forEach((b) => b.addEventListener("click", () => {
    lang = b.dataset.lang;
    document.querySelectorAll(".lang button").forEach((x) => x.setAttribute("aria-pressed", String(x.dataset.lang === lang)));
    render();
  }));

  setInterval(() => { if (model.until) $("vUntil").innerHTML = fmtUntil(); }, 1000);

  async function poll() {
    try {
      const r = await fetch("/__status", { cache:"no-store" });
      if (!r.ok) return;
      const s = await r.json();
      if (s.state === "running") { location.reload(); return; }
      Object.assign(model, {
        state: s.state || model.state, reason: s.reason ?? null,
        until: s.until ?? null, productVersion: s.productVersion ?? model.productVersion,
      });
      render();
    } catch (_) { /* keep current view */ }
  }
  setInterval(poll, 5000);

  render();

  const canvas = $("bg"), ctx = canvas.getContext("2d");
  const reduce = matchMedia("(prefers-reduced-motion: reduce)").matches;
  let W, H, dpr;
  function resize() {
    dpr = Math.min(devicePixelRatio || 1, 2);
    W = canvas.width = innerWidth * dpr; H = canvas.height = innerHeight * dpr;
  }
  resize(); addEventListener("resize", resize);
  const LANES = 7, SEG = 26;
  function accentRGB() {
    const c = getComputedStyle(document.documentElement).getPropertyValue("--accent").trim() || "#FDB022";
    const n = parseInt(c.slice(1), 16); return [n >> 16 & 255, n >> 8 & 255, n & 255];
  }
  function draw(t) {
    ctx.clearRect(0, 0, W, H);
    const [r, g, b] = accentRGB();
    const laneH = H / LANES, segW = W / SEG;
    const head = reduce ? SEG * 0.6 : (t / 2600) % (SEG + 10);
    for (let y = 0; y < LANES; y++) {
      const phase = y * 1.6;
      for (let x = 0; x < SEG; x++) {
        const d = (head - x - phase % SEG);
        let a = 0;
        const dist = ((d % (SEG + 10)) + (SEG + 10)) % (SEG + 10);
        if (dist < 10) a = Math.pow(1 - dist / 10, 2) * 0.32;
        const base = 0.03;
        ctx.fillStyle = "rgba(" + r + "," + g + "," + b + "," + (base + a).toFixed(3) + ")";
        const px = x * segW, py = y * laneH;
        const w = segW - 3 * dpr, h = laneH - 10 * dpr;
        const rr = 3 * dpr;
        ctx.beginPath();
        ctx.roundRect(px + 1.5 * dpr, py + 5 * dpr, Math.max(w, 1), Math.max(h, 1), rr);
        ctx.fill();
      }
    }
    if (!reduce) requestAnimationFrame(draw);
  }
  if (reduce) draw(0); else requestAnimationFrame(draw);
})();
</script>
</body></html>
"""";
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace ReadyStackGo.TestIdentityProvider;

/// <summary>
/// A small OpenID Connect provider for ReadyStackGo's integration and browser tests. It mimics
/// WYSCH ID: discovery with required pushed authorization requests (PAR) and no userinfo
/// endpoint, authorization code flow with PKCE S256 and client_secret_post, and the WYSCH client
/// pairing protocol. Users are picked on a plain HTML page. Not for production use.
/// </summary>
public class Program
{
    public static void Main(string[] args) => Build(args).Run();

    /// <summary>
    /// Builds the application. <paramref name="configure"/> runs before the services are built
    /// (tests use it to add configuration or a test server).
    /// </summary>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        var options = new TestIdentityProviderOptions();
        builder.Configuration.Bind(options);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ProviderState>();

        var app = builder.Build();
        var state = app.Services.GetRequiredService<ProviderState>();

        var issuerPath = new Uri(state.Issuer).AbsolutePath.TrimEnd('/');
        if (issuerPath.Length > 0)
        {
            app.UsePathBase(issuerPath);
        }

        MapEndpoints(app, state);
        return app;
    }

    private static void MapEndpoints(WebApplication app, ProviderState state)
    {
        app.MapGet("/health", () => Results.Text("ok"));

        app.MapGet("/.well-known/openid-configuration", () => Results.Json(Discovery(state, state.Issuer)));

        // Same document under another path: its issuer differs from the address it is fetched from.
        app.MapGet("/wrong-issuer/.well-known/openid-configuration", () => Results.Json(Discovery(state, state.Issuer)));

        app.MapGet("/jwks", () =>
        {
            var key = state.PublicKey;
            return Results.Json(new
            {
                keys = new[]
                {
                    new Dictionary<string, string>
                    {
                        ["kty"] = "RSA",
                        ["use"] = "sig",
                        ["alg"] = SecurityAlgorithms.RsaSha256,
                        ["kid"] = state.KeyId,
                        ["n"] = Base64UrlEncoder.Encode(key.Modulus!),
                        ["e"] = Base64UrlEncoder.Encode(key.Exponent!)
                    }
                }
            });
        });

        app.MapPost("/par", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var client = state.Authenticate(form["client_id"], form["client_secret"]);
            if (client == null)
            {
                return OAuthError("invalid_client", "Client authentication failed.", StatusCodes.Status401Unauthorized);
            }

            if (form["response_type"] != "code")
            {
                return OAuthError("unsupported_response_type", "Only response_type=code is supported.");
            }

            string redirectUri = form["redirect_uri"].ToString();
            if (string.IsNullOrEmpty(redirectUri) || !client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
            {
                return OAuthError("invalid_request", $"The redirect_uri {redirectUri} is not registered for this client.");
            }

            string scope = form["scope"].ToString();
            if (!scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.Ordinal))
            {
                return OAuthError("invalid_scope", "The scope must contain openid.");
            }

            string challenge = form["code_challenge"].ToString();
            if (string.IsNullOrEmpty(challenge) || form["code_challenge_method"] != "S256")
            {
                return OAuthError("invalid_request", "PKCE with code_challenge_method S256 is required.");
            }

            var requestUri = "urn:ietf:params:oauth:request_uri:" + ProviderState.NewToken();
            state.PushedRequests[requestUri] = new PushedRequest(
                client.ClientId, redirectUri, scope, NullIfEmpty(form["state"]), NullIfEmpty(form["nonce"]), challenge,
                DateTimeOffset.UtcNow + ProviderState.PushedRequestLifetime);

            return Results.Json(new { request_uri = requestUri, expires_in = (int)ProviderState.PushedRequestLifetime.TotalSeconds },
                statusCode: StatusCodes.Status201Created);
        });

        app.MapGet("/authorize", (HttpRequest request) =>
        {
            var unexpected = request.Query.Keys.Where(k => k is not ("client_id" or "request_uri")).ToList();
            if (unexpected.Count > 0)
            {
                return ErrorPage($"Pushed authorization requests are required: only client_id and request_uri are accepted (got {string.Join(", ", unexpected)}).");
            }

            string clientId = request.Query["client_id"].ToString();
            string requestUri = request.Query["request_uri"].ToString();
            if (!state.PushedRequests.TryGetValue(requestUri, out var pushed) ||
                pushed.ExpiresAt < DateTimeOffset.UtcNow ||
                !string.Equals(pushed.ClientId, clientId, StringComparison.Ordinal))
            {
                return ErrorPage("The request_uri is unknown, expired or belongs to another client.");
            }

            var body = new StringBuilder();
            body.Append("<h1>Sign in to the test identity provider</h1>");
            body.Append($"<p>Client <code>{Encode(clientId)}</code> asks you to sign in.</p>");
            foreach (var user in TestUsers.All)
            {
                body.Append($"<form method=\"post\" action=\"{Encode(state.BaseAddress)}/authorize/decision\">");
                body.Append(Hidden("request_uri", requestUri));
                body.Append(Hidden("user", user.Id));
                body.Append($"<button type=\"submit\">Sign in as {Encode(user.Name)}</button> ");
                body.Append($"<small>{Encode(user.Email)}{(user.EmailVerified ? "" : " (not verified)")}{(user.PreferredUsername == null ? ", no preferred_username" : "")}</small>");
                body.Append("</form>");
            }
            body.Append($"<form method=\"post\" action=\"{Encode(state.BaseAddress)}/authorize/decision\">");
            body.Append(Hidden("request_uri", requestUri));
            body.Append(Hidden("cancel", "true"));
            body.Append("<button type=\"submit\">Cancel</button>");
            body.Append("</form>");
            return Page("Sign in", body.ToString());
        });

        app.MapPost("/authorize/decision", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            string requestUri = form["request_uri"].ToString();
            if (!state.PushedRequests.TryRemove(requestUri, out var pushed) || pushed.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return ErrorPage("The request_uri is unknown, expired or was already used.");
            }

            var parameters = new Dictionary<string, string?>();
            if (form["cancel"] == "true")
            {
                parameters["error"] = "access_denied";
            }
            else
            {
                var user = TestUsers.Find(form["user"]);
                if (user == null)
                {
                    return ErrorPage("Unknown test user.");
                }

                var code = ProviderState.NewToken();
                state.Codes[code] = new AuthorizationCode(pushed.ClientId, pushed.RedirectUri, pushed.Nonce, pushed.CodeChallenge, user,
                    DateTimeOffset.UtcNow + ProviderState.CodeLifetime);
                parameters["code"] = code;
            }
            parameters["state"] = pushed.State;
            parameters["iss"] = state.Issuer;
            return Results.Redirect(AppendQuery(pushed.RedirectUri, parameters));
        });

        app.MapPost("/token", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            var client = state.Authenticate(form["client_id"], form["client_secret"]);
            if (client == null)
            {
                return OAuthError("invalid_client", "Client authentication failed.", StatusCodes.Status401Unauthorized);
            }

            if (form["grant_type"] != "authorization_code")
            {
                return OAuthError("unsupported_grant_type", "Only authorization_code is supported.");
            }

            string code = form["code"].ToString();
            if (string.IsNullOrEmpty(code) || !state.Codes.TryRemove(code, out var grant) || grant.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return OAuthError("invalid_grant", "The code is unknown, expired or was already used.");
            }

            if (!string.Equals(grant.ClientId, client.ClientId, StringComparison.Ordinal) ||
                !string.Equals(grant.RedirectUri, form["redirect_uri"].ToString(), StringComparison.Ordinal))
            {
                return OAuthError("invalid_grant", "The code was issued to another client or redirect_uri.");
            }

            string verifier = form["code_verifier"].ToString();
            if (string.IsNullOrEmpty(verifier) || ProviderState.PkceChallenge(verifier) != grant.CodeChallenge)
            {
                return OAuthError("invalid_grant", "The code_verifier does not match the code_challenge.");
            }

            return Results.Json(new
            {
                access_token = ProviderState.NewToken(),
                token_type = "Bearer",
                expires_in = 300,
                id_token = CreateIdToken(state, client.ClientId, grant)
            });
        });

        // WYSCH client pairing: the browser posts the manifest, the user confirms, the provider
        // returns to return_uri with a code that the server redeems at the pairing token endpoint.
        app.MapPost("/pairing", async (HttpRequest request) =>
        {
            if (!request.HasFormContentType)
            {
                return ErrorPage("Post the form fields manifest and state.");
            }

            var form = await request.ReadFormAsync();
            string pairingState = form["state"].ToString();
            var manifest = ParseManifest(form["manifest"].ToString(), out var manifestError);
            if (manifest == null || string.IsNullOrEmpty(pairingState))
            {
                return ErrorPage(manifestError ?? "The state is missing.");
            }

            var pairingId = ProviderState.NewToken();
            state.Pairings[pairingId] = manifest with
            {
                State = pairingState,
                ExpiresAt = DateTimeOffset.UtcNow + ProviderState.PairingLifetime
            };

            var body = new StringBuilder();
            body.Append("<h1>Connect an application</h1>");
            body.Append($"<p><strong>{Encode(manifest.Name)}</strong> ({Encode(manifest.Kind)}) at <code>{Encode(manifest.Url)}</code> wants to use this provider for sign-in.</p>");
            body.Append($"<p>Redirect URI: <code>{Encode(manifest.RedirectUri)}</code></p>");
            body.Append($"<form method=\"post\" action=\"{Encode(state.BaseAddress)}/pairing/decision\">");
            body.Append(Hidden("pairing", pairingId));
            body.Append(Hidden("decision", "connect"));
            body.Append("<button type=\"submit\">Connect</button>");
            body.Append("</form>");
            body.Append($"<form method=\"post\" action=\"{Encode(state.BaseAddress)}/pairing/decision\">");
            body.Append(Hidden("pairing", pairingId));
            body.Append(Hidden("decision", "cancel"));
            body.Append("<button type=\"submit\">Cancel</button>");
            body.Append("</form>");
            return Page("Connect", body.ToString());
        });

        app.MapPost("/pairing/decision", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            if (!state.Pairings.TryRemove(form["pairing"].ToString(), out var pairing) || pairing.ExpiresAt < DateTimeOffset.UtcNow)
            {
                return ErrorPage("This connection request is unknown, expired or was already answered.");
            }

            if (form["decision"] != "connect")
            {
                return Results.Redirect(AppendQuery(pairing.ReturnUri, new Dictionary<string, string?>
                {
                    ["error"] = "access_denied",
                    ["state"] = pairing.State
                }));
            }

            var client = new RegisteredClient
            {
                ClientId = "paired-" + ProviderState.NewToken()[..12],
                ClientSecret = ProviderState.NewToken(),
                RedirectUris = [pairing.RedirectUri],
                Paired = true
            };
            state.Clients[client.ClientId] = client;

            var code = ProviderState.NewToken();
            state.PairingCodes[code] = new PairingCode(pairing.State, client.ClientId, client.ClientSecret,
                DateTimeOffset.UtcNow + ProviderState.PairingLifetime);

            return Results.Redirect(AppendQuery(pairing.ReturnUri, new Dictionary<string, string?>
            {
                ["code"] = code,
                ["state"] = pairing.State
            }));
        });

        app.MapPost("/pairing/token", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            string code = form["code"].ToString();
            if (string.IsNullOrEmpty(code) || !state.PairingCodes.TryGetValue(code, out var pairingCode))
            {
                return OAuthError("invalid_grant", "The code is unknown or was already used.");
            }

            if (pairingCode.ExpiresAt < DateTimeOffset.UtcNow || !string.Equals(pairingCode.State, form["state"].ToString(), StringComparison.Ordinal))
            {
                return OAuthError("invalid_grant", "The code expired or the state does not match.");
            }

            if (!state.PairingCodes.TryRemove(code, out _))
            {
                return OAuthError("invalid_grant", "The code was already used.");
            }

            return Results.Json(new
            {
                client_id = pairingCode.ClientId,
                client_secret = pairingCode.ClientSecret,
                issuer = state.Issuer,
                confirmed_by = TestUsers.AlexVerified.Email
            });
        });

        // Test control: revoke a client (the next PAR or token request answers invalid_client).
        app.MapPost("/test/clients/{clientId}/revoke", (string clientId) =>
        {
            if (!state.Clients.TryGetValue(clientId, out var client))
            {
                return Results.NotFound();
            }
            client.Revoked = true;
            return Results.NoContent();
        });
    }

    private static Dictionary<string, object> Discovery(ProviderState state, string issuer)
    {
        var baseAddress = state.BaseAddress;
        return new Dictionary<string, object>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = $"{baseAddress}/authorize",
            ["token_endpoint"] = $"{baseAddress}/token",
            ["jwks_uri"] = $"{baseAddress}/jwks",
            ["pushed_authorization_request_endpoint"] = $"{baseAddress}/par",
            ["require_pushed_authorization_requests"] = true,
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = new[] { "authorization_code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { SecurityAlgorithms.RsaSha256 },
            ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["scopes_supported"] = new[] { "openid", "profile", "email" },
            ["claims_supported"] = new[] { "sub", "email", "email_verified", "preferred_username", "nickname", "name" },
            ["wysch_pairing_endpoint"] = $"{baseAddress}/pairing",
            ["wysch_pairing_token_endpoint"] = $"{baseAddress}/pairing/token"
        };
    }

    private static string CreateIdToken(ProviderState state, string clientId, AuthorizationCode grant)
    {
        var user = grant.User;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Subject,
            ["email"] = user.Email,
            ["email_verified"] = user.EmailVerified,
            ["name"] = user.Name,
            ["nickname"] = user.Nickname
        };
        if (user.PreferredUsername != null)
        {
            claims["preferred_username"] = user.PreferredUsername;
        }
        if (grant.Nonce != null)
        {
            claims["nonce"] = grant.Nonce;
        }

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = state.Issuer,
            Audience = clientId,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Claims = claims,
            SigningCredentials = new SigningCredentials(state.SigningKey, SecurityAlgorithms.RsaSha256)
        };
        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private static PendingPairing? ParseManifest(string json, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(json) ? "null" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "The manifest is missing or not a JSON object.";
                return null;
            }

            string? Str(string name) =>
                doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            var kind = Str("kind");
            var name = Str("name");
            var url = Str("url");
            var redirectUri = Str("redirect_uri");
            var returnUri = Str("return_uri");
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(name))
            {
                error = "The manifest needs kind and name.";
                return null;
            }
            if (!IsAbsoluteHttp(url) || !IsAbsoluteHttp(redirectUri) || !IsAbsoluteHttp(returnUri))
            {
                error = "The manifest needs url, redirect_uri and return_uri as absolute http or https addresses.";
                return null;
            }

            return new PendingPairing(kind, name, url!, redirectUri!, returnUri!, string.Empty, DateTimeOffset.MinValue);
        }
        catch (JsonException)
        {
            error = "The manifest is not valid JSON.";
            return null;
        }
    }

    private static bool IsAbsoluteHttp(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string AppendQuery(string address, Dictionary<string, string?> parameters)
    {
        var query = string.Join("&", parameters
            .Where(p => p.Value != null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
        return address + (address.Contains('?') ? "&" : "?") + query;
    }

    private static IResult OAuthError(string error, string description, int status = StatusCodes.Status400BadRequest) =>
        Results.Json(new { error, error_description = description }, statusCode: status);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Hidden(string name, string value) =>
        $"<input type=\"hidden\" name=\"{Encode(name)}\" value=\"{Encode(value)}\">";

    private static IResult Page(string title, string body, int status = StatusCodes.Status200OK) =>
        TypedResults.Content(
            $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>{Encode(title)} · Test identity provider</title></head><body>{body}</body></html>",
            "text/html; charset=utf-8", Encoding.UTF8, status);

    private static IResult ErrorPage(string message) =>
        Page("Error", $"<h1>Error</h1><p role=\"alert\">{Encode(message)}</p>", StatusCodes.Status400BadRequest);
}

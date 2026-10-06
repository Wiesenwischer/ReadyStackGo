using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IdpProgram = ReadyStackGo.TestIdentityProvider.Program;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// The test identity provider (tests/ReadyStackGo.TestIdentityProvider) hosted in-process on a
/// TestServer under the issuer <see cref="Issuer"/>.
/// </summary>
public sealed class TestIdentityProviderHost : IAsyncDisposable
{
    public const string Issuer = "http://localhost:9090/";
    public const string Authority = "localhost:9090";

    /// <summary>Base address of ReadyStackGo used by the tests (the default base URL of a fresh installation).</summary>
    public const string RsgoBaseUrl = "http://localhost:5000";

    public const string ManualClientId = "rsgo-manual";
    public const string ManualClientSecret = "manual-secret-1";
    public const string SecondClientId = "rsgo-manual-2";
    public const string SecondClientSecret = "manual-secret-2";
    public const string PairedStaticClientId = "rsgo-paired";
    public const string PairedStaticClientSecret = "paired-secret-1";

    /// <summary>Provider names whose redirect URIs the static clients accept.</summary>
    public static readonly string[] RegisteredProviderNames = ["oidc", "oidc-2", "Legacy", "wysch"];

    private readonly WebApplication _app;
    private readonly TestServer _server;

    private TestIdentityProviderHost(WebApplication app)
    {
        _app = app;
        _server = app.GetTestServer();
    }

    /// <summary>
    /// When true, ReadyStackGo's server-side calls fail as if the provider were stopped. The
    /// "browser" still reaches it.
    /// </summary>
    public bool Unreachable { get; set; }

    /// <summary>Delay of ReadyStackGo's calls to the token endpoint (a slow provider).</summary>
    public TimeSpan TokenDelay { get; set; }

    public static async Task<TestIdentityProviderHost> StartAsync()
    {
        var settings = new Dictionary<string, string?> { ["Issuer"] = Issuer };
        AddClient(settings, 0, ManualClientId, ManualClientSecret);
        AddClient(settings, 1, SecondClientId, SecondClientSecret);
        AddClient(settings, 2, PairedStaticClientId, PairedStaticClientSecret);

        var app = IdpProgram.Build([], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Logging.ClearProviders();
        });
        await app.StartAsync();
        return new TestIdentityProviderHost(app);
    }

    public static string RedirectUri(string providerName) => $"{RsgoBaseUrl}/api/auth/oidc/{providerName}/callback";

    /// <summary>Handler for ReadyStackGo's named client "Oidc": reaches only the provider's authority.</summary>
    public HttpMessageHandler CreateServerHandler() => new ProviderNetwork(this) { InnerHandler = _server.CreateHandler() };

    /// <summary>A client acting as the user's browser at the provider (never "unreachable").</summary>
    public HttpClient CreateBrowserClient() => new(_server.CreateHandler()) { BaseAddress = new Uri(Issuer) };

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static void AddClient(Dictionary<string, string?> settings, int index, string clientId, string secret)
    {
        settings[$"Clients:{index}:ClientId"] = clientId;
        settings[$"Clients:{index}:ClientSecret"] = secret;
        for (var i = 0; i < RegisteredProviderNames.Length; i++)
        {
            settings[$"Clients:{index}:RedirectUris:{i}"] = RedirectUri(RegisteredProviderNames[i]);
        }
    }

    /// <summary>
    /// Simulates the network between ReadyStackGo and the provider: other hosts do not resolve,
    /// and the provider can be switched off.
    /// </summary>
    private sealed class ProviderNetwork(TestIdentityProviderHost host) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.RequestUri?.Authority, Authority, StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException(HttpRequestError.NameResolutionError,
                    $"No such host is known ({request.RequestUri?.Host})");
            }
            if (host.Unreachable)
            {
                throw new HttpRequestException(HttpRequestError.ConnectionError,
                    "Connection refused (test identity provider switched off)");
            }
            if (host.TokenDelay > TimeSpan.Zero && request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal))
            {
                await Task.Delay(host.TokenDelay, cancellationToken);
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

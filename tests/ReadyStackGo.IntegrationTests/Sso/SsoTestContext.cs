using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// ReadyStackGo plus the in-process test identity provider. ReadyStackGo's named client "Oidc"
/// talks to the provider's TestServer; <see cref="Browser"/> simulates the user's browser.
/// </summary>
public sealed class SsoTestContext : IAsyncDisposable
{
    public const string AdminPassword = "TestPassword123!";

    private readonly SsoWebApplicationFactory _factory;
    private readonly List<SsoBrowser> _browsers = [];

    private SsoTestContext(TestIdentityProviderHost idp, SsoWebApplicationFactory factory, string templateDirectory)
    {
        Idp = idp;
        _factory = factory;
        TemplateDirectory = templateDirectory;
        Browser = NewBrowser();
    }

    public TestIdentityProviderHost Idp { get; }

    /// <summary>The browser of the main test user (cookies, optional bearer token).</summary>
    public SsoBrowser Browser { get; }

    /// <summary>Operator directory of identity provider templates (IdentityProviderTemplates:Path).</summary>
    public string TemplateDirectory { get; }

    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Starts provider and ReadyStackGo. <paramref name="writeTemplates"/> fills the template
    /// directory before the start; by default it holds a "wysch" template that points at the
    /// test provider (replacing the built-in one).
    /// </summary>
    public static async Task<SsoTestContext> StartAsync(
        Dictionary<string, string?>? settings = null,
        Action<string>? writeTemplates = null)
    {
        var templateDirectory = Path.Combine(Path.GetTempPath(), "rsgo-idp-templates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(templateDirectory);
        (writeTemplates ?? WriteTestWyschTemplate)(templateDirectory);

        var allSettings = new Dictionary<string, string?>
        {
            ["IdentityProviderTemplates:Path"] = templateDirectory,
            ["IdentityProviderTemplates:Enabled"] = ""
        };
        foreach (var (key, value) in settings ?? [])
        {
            allSettings[key] = value;
        }

        var idp = await TestIdentityProviderHost.StartAsync();
        var factory = new SsoWebApplicationFactory(idp, allSettings);
        // Starts the server.
        _ = factory.Server;
        return new SsoTestContext(idp, factory, templateDirectory);
    }

    public SsoBrowser NewBrowser()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri(TestIdentityProviderHost.RsgoBaseUrl)
        });
        var browser = new SsoBrowser(client, Idp.CreateBrowserClient());
        _browsers.Add(browser);
        return browser;
    }

    /// <summary>Writes a template "wysch" pointing at the test provider, pairing, PAR and HTTPS required.</summary>
    public static void WriteTestWyschTemplate(string directory) =>
        WriteTemplate(directory, "wysch", $$"""
            {
              "id": "wysch",
              "name": "WYSCH",
              "description": "Test WYSCH",
              "order": 10,
              "provider": { "name": "wysch", "displayName": "WYSCH" },
              "authority": { "url": "{{TestIdentityProviderHost.Issuer}}" },
              "registration": { "kind": "pairing" },
              "requirePar": true,
              "requireHttps": true,
              "scopes": "openid profile email",
              "claims": { "username": "preferred_username", "displayName": "nickname", "email": "email" },
              "offerInSetup": true
            }
            """, TestIcon);

    public const string TestIcon = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/></svg>""";

    public static void WriteTemplate(string directory, string id, string json, string? icon = null)
    {
        var folder = Path.Combine(directory, id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "template.json"), json);
        if (icon != null)
        {
            File.WriteAllText(Path.Combine(folder, "icon.svg"), icon);
        }
    }

    // ------------------------------------------------------------------ users

    /// <summary>Creates the first admin with a password through the wizard and returns a token.</summary>
    public async Task<string> CreatePasswordAdminAsync(string username = "admin", string email = "admin@example.com")
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/wizard/admin", new { username, email, password = AdminPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await LoginAsync(username, AdminPassword);
    }

    public async Task<string> LoginAsync(string username, string password)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    /// <summary>Adds a user directly (optionally without password, linked to a provider) and returns a token for it.</summary>
    public string AddUser(string username, string email, bool systemAdmin, string? password = null,
        (string Provider, string Subject)? link = null, bool emailVerified = true)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        User user;
        if (password != null)
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            user = User.Register(users.NextIdentity(), username, new EmailAddress(email), HashedPassword.Create(password, hasher));
            if (emailVerified)
            {
                user.VerifyEmail(DateTime.UtcNow);
            }
            if (link is { } l)
            {
                user.LinkExternalIdentity(l.Provider, l.Subject);
            }
        }
        else
        {
            var (provider, subject) = link ?? throw new ArgumentException("A user without password needs a link.");
            user = User.RegisterExternal(users.NextIdentity(), username, new EmailAddress(email), provider, subject, emailVerified);
        }

        if (systemAdmin)
        {
            user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        }
        users.Add(user);
        return scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateToken(user);
    }

    public User? FindUser(string username)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IUserRepository>().FindByUsername(username);
    }

    public List<User> AllUsers()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IUserRepository>().GetAll().ToList();
    }

    // ------------------------------------------------------------------ providers

    public IOidcSettingsService OidcSettings => Services.GetRequiredService<IOidcSettingsService>();

    /// <summary>Adds an enabled provider at the test identity provider directly to rsgo.oidc.json.</summary>
    public async Task<OidcProviderSettings> AddProviderAsync(
        string name = "oidc",
        string clientId = TestIdentityProviderHost.ManualClientId,
        string clientSecret = TestIdentityProviderHost.ManualClientSecret,
        string registration = RegistrationKinds.Manual,
        bool trustUnverifiedEmail = false,
        bool enabled = true,
        bool tested = true)
    {
        var provider = new OidcProviderSettings
        {
            Name = name,
            DisplayName = name,
            Authority = TestIdentityProviderHost.Issuer,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scopes = "openid profile email",
            Enabled = enabled,
            Template = registration == RegistrationKinds.Pairing ? "wysch" : OidcProviderSettings.GenericTemplateId,
            Registration = registration,
            RequirePar = registration == RegistrationKinds.Pairing,
            TrustUnverifiedEmail = trustUnverifiedEmail
        };
        if (tested)
        {
            provider.TestedSignIn = new OidcTestedSignIn(DateTime.UtcNow, OidcConnectionFingerprint.Compute(provider));
        }
        await OidcSettings.AddAsync(provider);
        return provider;
    }

    public string ConfigPath => Services.GetRequiredService<IConfiguration>()["ConfigPath"]!;

    public string OidcConfigFile => Path.Combine(ConfigPath, "rsgo.oidc.json");

    public ISystemConfigService SystemConfig => Services.GetRequiredService<ISystemConfigService>();

    public async ValueTask DisposeAsync()
    {
        foreach (var browser in _browsers)
        {
            browser.Dispose();
        }
        await _factory.DisposeAsync();
        await Idp.DisposeAsync();
        try
        {
            Directory.Delete(TemplateDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Ignore cleanup errors
        }
    }

    private sealed class SsoWebApplicationFactory(TestIdentityProviderHost idp, Dictionary<string, string?> settings)
        : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(IOidcService.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(idp.CreateServerHandler));
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// The setup wizard's sign-in with an identity provider (E19): start, pairing, continue,
/// sign-in, the run's own expiry, and the races with the setup window and the built-in path.
/// The template directory holds a "wysch" template pointing at the test identity provider.
/// </summary>
public class WizardSsoIntegrationTests : IAsyncLifetime
{
    private const string BaseUrl = TestIdentityProviderHost.RsgoBaseUrl;

    private readonly List<SsoTestContext> _contexts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var ctx in _contexts)
        {
            await ctx.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------ start

    [Fact]
    public async Task Templates_OffersOnlyConnectTemplatesWithFixedAuthority()
    {
        var ctx = await StartAsync();

        var templates = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/templates"));

        templates.EnumerateArray().Select(t => t.GetProperty("id").GetString()).Should().Equal("wysch");
    }

    [Fact]
    public async Task Start_SetsFlowCookieAndBaseUrl_ReturnsPairingForm()
    {
        var ctx = await StartAsync();

        var response = await ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = "https://rsgo.example.com/" });
        var body = await Expect(Task.FromResult(response));

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rsgo_sso_flow=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("path=/api").And.Contain("samesite=lax");
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().Be("https://rsgo.example.com");

        body.GetProperty("run").GetProperty("state").GetString().Should().Be("started");
        var start = body.GetProperty("start");
        start.GetProperty("kind").GetString().Should().Be("formPost");
        start.GetProperty("url").GetString().Should().Be($"{TestIdentityProviderHost.Issuer}pairing");
        var manifest = JsonDocument.Parse(start.GetProperty("fields").GetProperty("manifest").GetString()!).RootElement;
        manifest.GetProperty("kind").GetString().Should().Be("readystackgo");
        manifest.GetProperty("redirect_uri").GetString().Should().Be("https://rsgo.example.com/api/auth/oidc/wysch/callback");
        manifest.GetProperty("return_uri").GetString().Should().Be("https://rsgo.example.com/api/sso/registration/callback");
    }

    [Fact]
    public async Task Start_AfterSetupWindow_Returns403()
    {
        var ctx = await StartAsync(timeoutSeconds: 1);
        await ctx.Browser.GetAsync("/api/wizard/status");
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        var response = await ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = "https://rsgo.example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().BeNull("a refused start changes nothing");
    }

    [Fact]
    public async Task Start_WithExistingSystemAdmin_IsRejected()
    {
        var ctx = await StartAsync();
        await ctx.CreatePasswordAdminAsync();

        var error = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = BaseUrl }),
            HttpStatusCode.Conflict);

        error.GetProperty("code").GetString().Should().Be("completed_elsewhere");
    }

    [Theory]
    [InlineData("http://server:8080", HttpStatusCode.BadRequest, "https_required")]
    [InlineData("http://localhost.example.com", HttpStatusCode.BadRequest, "https_required")]
    [InlineData("not a url", HttpStatusCode.BadRequest, "base_url_invalid")]
    [InlineData(null, HttpStatusCode.BadRequest, "base_url_invalid")]
    public async Task Start_WithUnsuitableBaseUrl_IsRejected(string? baseUrl, HttpStatusCode status, string code)
    {
        var ctx = await StartAsync();

        var error = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl }), status);

        error.GetProperty("code").GetString().Should().Be(code);
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Start_WithTemplateNotOfferedInSetup_Returns404()
    {
        var ctx = await StartAsync();

        var error = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "generic-oidc", baseUrl = BaseUrl }),
            HttpStatusCode.NotFound);

        error.GetProperty("code").GetString().Should().Be("template_unknown");
    }

    [Fact]
    public async Task Start_ProviderUnreachable_IsRejected()
    {
        var ctx = await StartAsync();
        ctx.Idp.Unreachable = true;

        var error = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = BaseUrl }),
            HttpStatusCode.BadGateway);

        error.GetProperty("code").GetString().Should().Be("unreachable");
    }

    // ------------------------------------------------------------------ binding to the browser

    [Fact]
    public async Task RunEndpoints_WithoutFlowCookie_Return404()
    {
        var ctx = await StartAsync();
        await StartRunAsync(ctx.Browser);
        using var other = ctx.NewBrowser();

        var error = await Expect(other.PostJsonAsync("/api/wizard/sso/continue"), HttpStatusCode.NotFound);
        error.GetProperty("code").GetString().Should().Be("no_run");
        (await other.GetAsync("/api/wizard/sso/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostJsonAsync("/api/wizard/sso/retry")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var (signIn, _) = await other.NavigateAsync($"{BaseUrl}/api/wizard/sso/sign-in");
        signIn.Should().Be($"{BaseUrl}/wizard?sso=foreign");
    }

    [Fact]
    public async Task PairingReturn_InAnotherBrowser_IsForeign_AndTheRunKeepsWaiting()
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser);
        using var other = ctx.NewBrowser();

        var back = await other.PairAsync(start.GetProperty("start"));

        back.Should().Be($"{BaseUrl}/wizard?sso=foreign");
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("state").GetString().Should().Be("started");
        status.GetProperty("registrationReturned").GetBoolean().Should().BeFalse();
    }

    // ------------------------------------------------------------------ the whole run

    [Fact]
    public async Task WholeRun_CreatesAdminWithoutPassword_Linked_EmailVerified_ProviderEnabled()
    {
        var ctx = await StartAsync();
        var browser = ctx.Browser;
        var start = await StartRunAsync(browser);

        (await browser.PairAsync(start.GetProperty("start"))).Should().Be($"{BaseUrl}/wizard?sso=returned");
        (await Expect(browser.GetAsync("/api/wizard/sso/status"))).GetProperty("registrationReturned").GetBoolean().Should().BeTrue();

        var registered = await Expect(browser.PostJsonAsync("/api/wizard/sso/continue"));
        registered.GetProperty("state").GetString().Should().Be("registered");
        registered.GetProperty("providerName").GetString().Should().Be("wysch");
        var saved = await ctx.OidcSettings.GetByNameAsync("wysch");
        saved!.Enabled.Should().BeFalse("the provider stays disabled until the sign-in succeeded");
        saved.IsPaired.Should().BeTrue();

        var final = await browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "alex");
        final.Should().StartWith($"{BaseUrl}/wizard?sso=returned#token=");

        var status = await Expect(browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("state").GetString().Should().Be("signedIn");
        status.GetProperty("signedInUsername").GetString().Should().Be("alex");
        status.GetProperty("signedInEmail").GetString().Should().Be("alex@example.com");

        var admin = ctx.FindUser("alex")!;
        admin.HasPassword.Should().BeFalse();
        admin.HasRole(RoleId.SystemAdmin).Should().BeTrue();
        admin.IsEmailVerified.Should().BeTrue();
        admin.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-alex-verified");

        var provider = await ctx.OidcSettings.GetByNameAsync("wysch");
        provider!.Enabled.Should().BeTrue();
        provider.HasPassedTestSignInForCurrentConnection.Should().BeTrue();

        var wizard = await Expect(browser.GetAsync("/api/wizard/status"));
        wizard.GetProperty("isCompleted").GetBoolean().Should().BeTrue();

        // The token from the fragment works, and the new admin is warned about the missing password.
        browser.Token = Uri.UnescapeDataString(final.Split("#token=")[1]);
        var profile = await Expect(browser.GetAsync("/api/user/profile"));
        profile.GetProperty("hasPassword").GetBoolean().Should().BeFalse();
        profile.GetProperty("noAdminWithPassword").GetBoolean().Should().BeTrue();

        // Signing in later from the login page works through the same provider.
        using var later = ctx.NewBrowser();
        (await later.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/wysch/challenge", "alex")).Should().StartWith($"{BaseUrl}/oidc-callback#token=");
    }

    [Fact]
    public async Task UserWithoutPreferredUsername_GetsUsernameFromEmail()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser);

        await ctx.Browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "noah");

        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("state").GetString().Should().Be("signedIn");
        status.GetProperty("signedInUsername").GetString().Should().Be("noah_no_username");
    }

    [Fact]
    public async Task Run_ContinuesWhenTheSetupWindowExpiresDuringTheRun()
    {
        var ctx = await StartAsync(timeoutSeconds: 5);
        var windowEnd = await WindowEndAsync(ctx);
        var start = await StartRunAsync(ctx.Browser);
        await ctx.Browser.PairAsync(start.GetProperty("start"));

        await DelayUntilAsync(windowEnd);
        var locked = await Expect(ctx.Browser.GetAsync("/api/wizard/status"));
        locked.GetProperty("isCompleted").GetBoolean().Should().BeFalse();
        locked.GetProperty("timeout").GetProperty("isLocked").GetBoolean().Should().BeTrue();

        (await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"))).GetProperty("state").GetString().Should().Be("registered");
        await ctx.Browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "alex");

        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("state").GetString().Should().Be("signedIn");
        var done = await Expect(ctx.Browser.GetAsync("/api/wizard/status"));
        done.GetProperty("isCompleted").GetBoolean().Should().BeTrue();
        done.GetProperty("timeout").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Run_ExpiresAfterItsDuration()
    {
        var ctx = await StartAsync(runSeconds: 1);
        var start = await StartRunAsync(ctx.Browser);
        await ctx.Browser.PairAsync(start.GetProperty("start"));

        await Task.Delay(TimeSpan.FromSeconds(1.5));

        var status = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"));
        status.GetProperty("state").GetString().Should().Be("failed");
        status.GetProperty("failureReason").GetString().Should().Be("expired");
        (await ctx.OidcSettings.GetAllAsync()).Should().BeEmpty("an expired run saves no provider");

        var retry = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/retry"), HttpStatusCode.Conflict);
        retry.GetProperty("code").GetString().Should().Be("retry_not_possible");
    }

    [Fact]
    public async Task UnverifiedEmail_CreatesNoAdmin_TryAgainSignsInAgain()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser);

        await ctx.Browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "uma");

        var failed = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        failed.GetProperty("state").GetString().Should().Be("failed");
        failed.GetProperty("failureReason").GetString().Should().Be("email_unverified");
        ctx.AllUsers().Should().BeEmpty();
        (await ctx.OidcSettings.GetByNameAsync("wysch"))!.Enabled.Should().BeFalse();

        var retry = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/retry"));
        retry.GetProperty("signIn").GetBoolean().Should().BeTrue();
        retry.GetProperty("run").GetProperty("state").GetString().Should().Be("registered");

        await ctx.Browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "alex");
        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("state").GetString().Should().Be("signedIn");
    }

    [Fact]
    public async Task EmailWithPlus_FailsWithEmailInvalid()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser);

        await ctx.Browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "pat");

        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("failureReason").GetString().Should().Be("email_invalid");
        ctx.AllUsers().Should().BeEmpty();
    }

    [Fact]
    public async Task OtherPathCreatesAdminMeanwhile_RunFailsWithCompletedElsewhere()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser);
        var (authorizeUrl, _) = await ctx.Browser.NavigateAsync($"{BaseUrl}/api/wizard/sso/sign-in");

        // Someone completes the built-in path while the user is at the provider.
        await ctx.CreatePasswordAdminAsync("builtin", "builtin@example.com");
        var final = await ctx.Browser.SignInAtProviderAsync(authorizeUrl, "alex");

        final.Should().Be($"{BaseUrl}/wizard?sso=returned");
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("state").GetString().Should().Be("failed");
        status.GetProperty("failureReason").GetString().Should().Be("completed_elsewhere");
        ctx.AllUsers().Select(u => u.Username).Should().Equal("builtin");
        (await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/retry"), HttpStatusCode.Conflict))
            .GetProperty("code").GetString().Should().Be("retry_not_possible");
    }

    [Fact]
    public async Task PairingCancelled_TryAgainStartsANewPairing()
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser);

        await ctx.Browser.PairAsync(start.GetProperty("start"), connect: false);

        var failed = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        failed.GetProperty("failureReason").GetString().Should().Be("cancelled");

        var retry = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/retry"));
        retry.GetProperty("signIn").GetBoolean().Should().BeFalse();
        retry.GetProperty("run").GetProperty("state").GetString().Should().Be("started");

        (await ctx.Browser.PairAsync(retry.GetProperty("start"))).Should().Be($"{BaseUrl}/wizard?sso=returned");
        (await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"))).GetProperty("state").GetString().Should().Be("registered");
    }

    [Fact]
    public async Task ProviderDownAtContinue_FailsWithUnreachable()
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser);
        await ctx.Browser.PairAsync(start.GetProperty("start"));
        ctx.Idp.Unreachable = true;

        var status = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"));

        status.GetProperty("state").GetString().Should().Be("failed");
        status.GetProperty("failureReason").GetString().Should().Be("unreachable");
    }

    [Fact]
    public async Task End_RemovesTheRun()
    {
        var ctx = await StartAsync();
        await StartRunAsync(ctx.Browser);

        (await ctx.Browser.DeleteAsync("/api/wizard/sso")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ctx.Browser.GetAsync("/api/wizard/sso/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EarlierAbortedRun_ProviderIsReusedByTheNextRun()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser);
        var firstClient = (await ctx.OidcSettings.GetByNameAsync("wysch"))!.ClientId;
        (await ctx.Browser.DeleteAsync("/api/wizard/sso")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await RegisterRunAsync(ctx.Browser);

        var providers = await ctx.OidcSettings.GetAllAsync();
        providers.Select(p => p.Name).Should().Equal("wysch");
        providers[0].ClientId.Should().NotBe(firstClient, "the new run replaces the credentials of the aborted one");
    }

    // ------------------------------------------------------------------ wizard status

    [Fact]
    public async Task WizardStatus_AfterWindowExpiry_WithExistingAdmin_IsCompletedWithoutTimeout()
    {
        var ctx = await StartAsync(timeoutSeconds: 5);
        var windowEnd = await WindowEndAsync(ctx);
        await ctx.CreatePasswordAdminAsync();

        await DelayUntilAsync(windowEnd);
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/status"));

        status.GetProperty("isCompleted").GetBoolean().Should().BeTrue();
        status.GetProperty("timeout").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task WizardStatus_AfterWindowExpiry_WithoutAdmin_IsLocked()
    {
        var ctx = await StartAsync(timeoutSeconds: 1);
        await ctx.Browser.GetAsync("/api/wizard/status");

        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/status"));

        status.GetProperty("isCompleted").GetBoolean().Should().BeFalse();
        status.GetProperty("timeout").GetProperty("isLocked").GetBoolean().Should().BeTrue();
    }

    // ------------------------------------------------------------------ helpers

    private async Task<SsoTestContext> StartAsync(int? timeoutSeconds = null, int? runSeconds = null)
    {
        var settings = new Dictionary<string, string?>();
        if (timeoutSeconds != null)
        {
            settings["Wizard:TimeoutSeconds"] = timeoutSeconds.ToString();
        }
        if (runSeconds != null)
        {
            settings["Wizard:SsoRunSeconds"] = runSeconds.ToString();
        }
        var ctx = await SsoTestContext.StartAsync(settings);
        _contexts.Add(ctx);
        return ctx;
    }

    /// <summary>
    /// Opens the setup window (the first status call starts it, if startup did not) and returns
    /// a time by which it has certainly ended.
    /// </summary>
    private static async Task<DateTime> WindowEndAsync(SsoTestContext ctx, int timeoutSeconds = 5)
    {
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/status"));
        status.GetProperty("timeout").GetProperty("isTimedOut").GetBoolean().Should().BeFalse();
        return DateTime.UtcNow.AddSeconds(timeoutSeconds);
    }

    private static async Task DelayUntilAsync(DateTime utc)
    {
        var wait = utc - DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait);
        }
    }

    private static Task<JsonElement> Expect(Task<HttpResponseMessage> call, HttpStatusCode status = HttpStatusCode.OK) =>
        SsoBrowser.ExpectAsync(call, status);

    private static Task<JsonElement> StartRunAsync(SsoBrowser browser) =>
        Expect(browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = BaseUrl }));

    /// <summary>Start, pairing and continue: the run is registered and the provider saved disabled.</summary>
    private static async Task RegisterRunAsync(SsoBrowser browser)
    {
        var start = await StartRunAsync(browser);
        (await browser.PairAsync(start.GetProperty("start"))).Should().Be($"{BaseUrl}/wizard?sso=returned");
        (await Expect(browser.PostJsonAsync("/api/wizard/sso/continue"))).GetProperty("state").GetString().Should().Be("registered");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Application.Services.Oidc;
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

    /// <summary>
    /// A run address other than the unset default (http://localhost:5000), on the same host so the
    /// browser's cookies still apply.
    /// </summary>
    private const string RunAddress = "http://localhost:5001";

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
    public async Task Start_SetsFlowCookie_ReturnsPairingForm_AndLeavesTheBaseUrlUntilTheAdminExists()
    {
        var ctx = await StartAsync();

        var response = await ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = "https://rsgo.example.com/" });
        var body = await Expect(Task.FromResult(response));

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rsgo_sso_flow=", StringComparison.Ordinal));
        cookie.Should().Contain("httponly").And.Contain("path=/api").And.Contain("samesite=lax");
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().BeNull("an anonymous start must not change the installation's address");

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
        // The reason on its own, so the wizard card can show it (frame 172:2794).
        var reason = error.GetProperty("reason").GetString();
        reason.Should().NotBeNullOrWhiteSpace();
        error.GetProperty("message").GetString().Should().Contain(reason!.TrimEnd('.'));
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
        status.GetProperty("signedInDisplayName").GetString().Should().Be("Alex V.", "WYSCH sends the display name in nickname");

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
    public async Task SecondStartWithAnotherAddress_DoesNotRedirectTheFirstRun_NorChangeTheBaseUrl()
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser, RunAddress);

        // Another browser starts anonymously with a foreign address while the first run is open.
        using var other = ctx.NewBrowser();
        await Expect(other.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl = "https://attacker.example" }));
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().BeNull();

        (await ctx.Browser.PairAsync(start.GetProperty("start"))).Should().Be($"{RunAddress}/wizard?sso=returned");
        await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"));
        var final = await ctx.Browser.SignInThroughAsync($"{RunAddress}/api/wizard/sso/sign-in", "alex");

        final.Should().StartWith($"{RunAddress}/wizard?sso=returned#token=", "the token goes back to the address of this run");
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().Be(RunAddress, "the run that created the administrator sets the address");
    }

    [Fact]
    public async Task SignInFailure_RedirectsToTheRunAddress_WhileTheBaseUrlIsUnset()
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser, RunAddress);
        (await ctx.Browser.PairAsync(start.GetProperty("start"))).Should().Be($"{RunAddress}/wizard?sso=returned");
        await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"));

        // The provider is down when the sign-in starts: back to the run's address, not the unset default.
        ctx.Idp.Unreachable = true;
        var (url, _) = await ctx.Browser.NavigateAsync($"{RunAddress}/api/wizard/sso/sign-in");

        url.Should().Be($"{RunAddress}/wizard?sso=returned");
        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("failureReason").GetString().Should().Be("unreachable");
        (await ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().BeNull();
    }

    [Fact]
    public async Task WizardRedirectsWithoutARun_AreRelative()
    {
        var ctx = await StartAsync();
        using var other = ctx.NewBrowser();

        var signIn = await other.GetAsync("/api/wizard/sso/sign-in");

        SsoBrowser.IsRedirect(signIn.StatusCode).Should().BeTrue();
        signIn.Headers.Location!.OriginalString.Should().Be("/wizard?sso=foreign", "the browser is on the installation's host already");
    }

    [Fact]
    public async Task SignInReturnAfterTheStateExpired_FailsTheWaitingRun()
    {
        var ctx = await StartAsync(flowStateSeconds: 2);
        await RegisterRunAsync(ctx.Browser, RunAddress);
        var (authorizeUrl, _) = await ctx.Browser.NavigateAsync($"{RunAddress}/api/wizard/sso/sign-in");
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        var back = await ctx.Browser.SignInAtProviderAsync(authorizeUrl, "alex");

        back.Should().Be($"{RunAddress}/wizard?sso=returned", "back to the run's address, not the unset default");
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("failureReason").GetString().Should().Be("sign_in_failed");
        status.GetProperty("failureDetail").GetString().Should().Contain("more than 10 minutes");
        ctx.FindUser("alex").Should().BeNull();
    }

    [Fact]
    public async Task SignInCallbackInAnotherBrowser_IsForeign_AndCreatesNoAdmin()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser);
        var (authorizeUrl, _) = await ctx.Browser.NavigateAsync($"{BaseUrl}/api/wizard/sso/sign-in");

        // Only the browser that started the run can finish it (E19).
        using var other = ctx.NewBrowser();
        var back = await other.SignInAtProviderAsync(authorizeUrl, "alex");

        back.Should().EndWith("/wizard?sso=foreign");
        ctx.FindUser("alex").Should().BeNull();
        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("state").GetString().Should().Be("registered");
    }

    [Fact]
    public async Task ProviderEnabledElsewhereWhileThePairingIsRedeemed_RunFails_ProviderUntouched()
    {
        // Same template, enabled meanwhile: only "Enabled" stops the run.
        await ProviderSetUpElsewhereWhileRedeeming(ctx => ctx.AddProviderAsync("wysch", registration: RegistrationKinds.Pairing,
                clientId: TestIdentityProviderHost.PairedStaticClientId, clientSecret: TestIdentityProviderHost.PairedStaticClientSecret, enabled: true),
            expectedClientId: TestIdentityProviderHost.PairedStaticClientId, expectedEnabled: true);
    }

    [Fact]
    public async Task ProviderOfAnotherTemplateSavedWhileThePairingIsRedeemed_RunFails_ProviderUntouched()
    {
        // Disabled, but from another template: only the template stops the run.
        await ProviderSetUpElsewhereWhileRedeeming(ctx => ctx.AddProviderAsync("wysch", enabled: false),
            expectedClientId: TestIdentityProviderHost.ManualClientId, expectedEnabled: false);
    }

    private async Task ProviderSetUpElsewhereWhileRedeeming(Func<SsoTestContext, Task> setUpElsewhere, string expectedClientId, bool expectedEnabled)
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser);
        await ctx.Browser.PairAsync(start.GetProperty("start"));

        // The provider answers /pairing/token slowly; meanwhile "wysch" is saved elsewhere.
        ctx.Idp.TokenDelay = TimeSpan.FromSeconds(3);
        var continued = ctx.Browser.PostJsonAsync("/api/wizard/sso/continue");
        await Task.Delay(TimeSpan.FromSeconds(1));
        await setUpElsewhere(ctx);

        var run = await Expect(continued);
        run.GetProperty("state").GetString().Should().Be("failed");
        run.GetProperty("failureReason").GetString().Should().Be("registration_failed");
        run.GetProperty("failureDetail").GetString().Should().Contain("set up elsewhere");
        var provider = await ctx.OidcSettings.GetByNameAsync("wysch");
        provider!.Enabled.Should().Be(expectedEnabled);
        provider.ClientId.Should().Be(expectedClientId, "the provider saved elsewhere is not overwritten");
    }

    [Fact]
    public async Task ContinueTwiceAtTheSameTime_OneGets409_TheRunEndsRegistered()
    {
        var ctx = await StartAsync();
        var start = await StartRunAsync(ctx.Browser);
        await ctx.Browser.PairAsync(start.GetProperty("start"));

        // The first redemption waits at the provider; a second tab asks at the same time.
        ctx.Idp.TokenDelay = TimeSpan.FromSeconds(2);
        var first = ctx.Browser.PostJsonAsync("/api/wizard/sso/continue");
        await Task.Delay(TimeSpan.FromSeconds(0.5));
        var second = await ctx.Browser.PostJsonAsync("/api/wizard/sso/continue");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).Should().Contain("continue_in_progress");
        (await Expect(first)).GetProperty("state").GetString().Should().Be("registered");

        // Afterwards the run is free again: continue answers with the current state.
        ctx.Idp.TokenDelay = TimeSpan.Zero;
        (await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/continue"))).GetProperty("state").GetString().Should().Be("registered");
    }

    [Fact]
    public async Task LatePairingCancel_StaysCancelled()
    {
        var ctx = await StartAsync(flowStateSeconds: 1);
        var start = await StartRunAsync(ctx.Browser, RunAddress);
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        (await ctx.Browser.PairAsync(start.GetProperty("start"), connect: false)).Should().Be($"{RunAddress}/wizard?sso=returned");

        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("failureReason").GetString().Should().Be("cancelled");
    }

    [Fact]
    public async Task PairingReturnTheRunDoesNotWaitFor_IsLost_AndTheRunKeepsWaiting()
    {
        var ctx = await StartAsync();
        await StartRunAsync(ctx.Browser, RunAddress);

        var back = await ctx.Browser.GetAsync($"{RunAddress}/api/sso/registration/callback?state=forged&code=x");

        back.Headers.Location!.OriginalString.Should().Be("/settings/oidc?error=registration_lost", "relative, the base URL is still unset");
        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("state").GetString().Should().Be("started");
    }

    [Fact]
    public async Task SignInCallbackWithAForeignState_LeavesTheRunWaiting()
    {
        var ctx = await StartAsync();
        await RegisterRunAsync(ctx.Browser, RunAddress);
        await ctx.Browser.NavigateAsync($"{RunAddress}/api/wizard/sso/sign-in");

        var back = await ctx.Browser.GetAsync($"{RunAddress}/api/auth/oidc/wysch/callback?state=forged&code=x");

        back.Headers.Location!.OriginalString.Should().Be("/login?error=oidc_state", "relative, the base URL is still unset");
        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("state").GetString().Should().Be("registered");
    }

    [Fact]
    public async Task LateSignInCancel_StaysCancelled()
    {
        var ctx = await StartAsync(flowStateSeconds: 2);
        await RegisterRunAsync(ctx.Browser, RunAddress);
        var (authorizeUrl, _) = await ctx.Browser.NavigateAsync($"{RunAddress}/api/wizard/sso/sign-in");
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        (await ctx.Browser.SignInAtProviderAsync(authorizeUrl, null)).Should().Be($"{RunAddress}/wizard?sso=returned");

        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("failureReason").GetString().Should().Be("cancelled");
    }

    [Fact]
    public async Task Templates_EachFilterConditionExcludesATemplate()
    {
        var ctx = await StartAsync(writeTemplates: dir =>
        {
            SsoTestContext.WriteTestWyschTemplate(dir);
            // Fixed authority and connect, but not offered in setup.
            SsoTestContext.WriteTemplate(dir, "pair-hidden", FilterTemplate("pair-hidden", fixedAuthority: true, kind: "pairing", offer: false));
            // Offered and connect, but the authority has to be entered.
            SsoTestContext.WriteTemplate(dir, "pair-input", FilterTemplate("pair-input", fixedAuthority: false, kind: "pairing", offer: true));
            // Offered with a fixed authority, but manual registration (no connect).
            SsoTestContext.WriteTemplate(dir, "manual-fixed", FilterTemplate("manual-fixed", fixedAuthority: true, kind: "manual", offer: true));
        });

        var templates = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/templates"));

        templates.EnumerateArray().Select(t => t.GetProperty("id").GetString()).Should().Equal("wysch");
        foreach (var id in new[] { "pair-hidden", "pair-input", "manual-fixed" })
        {
            var error = await Expect(ctx.Browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = id, baseUrl = BaseUrl }), HttpStatusCode.NotFound);
            error.GetProperty("code").GetString().Should().Be("template_unknown", id);
        }
    }

    private static string FilterTemplate(string id, bool fixedAuthority, string kind, bool offer)
    {
        var authority = fixedAuthority
            ? "{ \"url\": \"" + TestIdentityProviderHost.Issuer + "\" }"
            : "{ \"input\": { \"hint\": \"The issuer.\", \"example\": \"https://id.example.com/\" } }";
        return "{ \"id\": \"" + id + "\", \"name\": \"" + id + "\", \"description\": \"Filter test\", " +
               "\"provider\": { \"name\": \"" + id + "\" }, \"authority\": " + authority + ", " +
               "\"registration\": { \"kind\": \"" + kind + "\" }, \"scopes\": \"openid profile email\", " +
               "\"offerInSetup\": " + (offer ? "true" : "false") + " }";
    }

    [Fact]
    public async Task PairingReturnAfterTheStateExpired_FailsTheWaitingRun()
    {
        var ctx = await StartAsync(flowStateSeconds: 1);
        var start = await StartRunAsync(ctx.Browser, RunAddress);
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        (await ctx.Browser.PairAsync(start.GetProperty("start"))).Should().Be($"{RunAddress}/wizard?sso=returned");

        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        status.GetProperty("state").GetString().Should().Be("failed");
        status.GetProperty("failureReason").GetString().Should().Be("registration_failed");
        status.GetProperty("failureDetail").GetString().Should().Contain("more than 10 minutes");
    }

    [Fact]
    public async Task RunExpiresDuringTheTokenExchange_CreatesNoAdmin_AndTheWizardStaysOpen()
    {
        var ctx = await StartAsync(runSeconds: 6);
        await RegisterRunAsync(ctx.Browser);
        var status = await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"));
        var expiresAt = status.GetProperty("expiresAt").GetDateTime().ToUniversalTime();

        // The provider answers the token request only after the run has expired.
        ctx.Idp.TokenDelay = expiresAt - DateTime.UtcNow + TimeSpan.FromSeconds(1);
        var final = await ctx.Browser.SignInThroughAsync($"{BaseUrl}/api/wizard/sso/sign-in", "alex");

        final.Should().Be($"{BaseUrl}/wizard?sso=returned");
        (await Expect(ctx.Browser.GetAsync("/api/wizard/sso/status"))).GetProperty("failureReason").GetString().Should().Be("expired");
        ctx.FindUser("alex").Should().BeNull("no administrator may be left behind without an enabled provider");
        (await Expect(ctx.Browser.GetAsync("/api/wizard/status"))).GetProperty("isCompleted").GetBoolean().Should().BeFalse();
        (await ctx.OidcSettings.GetByNameAsync("wysch"))!.Enabled.Should().BeFalse();
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

    private async Task<SsoTestContext> StartAsync(int? timeoutSeconds = null, int? runSeconds = null, int? flowStateSeconds = null,
        Action<string>? writeTemplates = null)
    {
        var settings = new Dictionary<string, string?>();
        if (flowStateSeconds != null)
        {
            settings["Sso:FlowStateSeconds"] = flowStateSeconds.ToString();
        }
        if (timeoutSeconds != null)
        {
            settings["Wizard:TimeoutSeconds"] = timeoutSeconds.ToString();
        }
        if (runSeconds != null)
        {
            settings["Wizard:SsoRunSeconds"] = runSeconds.ToString();
        }
        var ctx = await SsoTestContext.StartAsync(settings, writeTemplates);
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

    private static Task<JsonElement> StartRunAsync(SsoBrowser browser, string baseUrl = BaseUrl) =>
        Expect(browser.PostJsonAsync("/api/wizard/sso/start", new { templateId = "wysch", baseUrl }));

    /// <summary>Start, pairing and continue: the run is registered and the provider saved disabled.</summary>
    private static async Task RegisterRunAsync(SsoBrowser browser, string baseUrl = BaseUrl)
    {
        var start = await StartRunAsync(browser, baseUrl);
        (await browser.PairAsync(start.GetProperty("start"))).Should().Be($"{baseUrl}/wizard?sso=returned");
        (await Expect(browser.PostJsonAsync("/api/wizard/sso/continue"))).GetProperty("state").GetString().Should().Be("registered");
    }
}

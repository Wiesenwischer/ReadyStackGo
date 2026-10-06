using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// "Add provider" and a provider's page (setup sessions under /api/settings/oidc/setup) against
/// the test identity provider: checks, test sign-in, saving, lockout protection, removing and
/// pairing.
/// </summary>
public class SsoSetupIntegrationTests : IAsyncLifetime
{
    private const string BaseUrl = TestIdentityProviderHost.RsgoBaseUrl;
    private const string Issuer = TestIdentityProviderHost.Issuer;

    private SsoTestContext _ctx = null!;
    private SsoBrowser Browser => _ctx.Browser;

    public async Task InitializeAsync()
    {
        _ctx = await SsoTestContext.StartAsync();
        Browser.Token = await _ctx.CreatePasswordAdminAsync();
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ------------------------------------------------------------------ sessions

    [Fact]
    public async Task Session_Lifecycle_CreateGetUpdateCancel_LeavesNoTraces()
    {
        var created = await Expect(Browser.PostJsonAsync("/api/settings/oidc/setup", new { templateId = "generic-oidc" }));
        var id = created.GetProperty("id").GetString()!;
        created.GetProperty("isNew").GetBoolean().Should().BeTrue();
        created.GetProperty("name").GetString().Should().Be("oidc");
        created.GetProperty("registrationKind").GetString().Should().Be("manual");
        created.GetProperty("interaction").GetString().Should().Be("manualEntry");

        var updated = await Expect(Browser.PatchJsonAsync($"/api/settings/oidc/setup/{id}", new { displayName = "  Company SSO ", trustUnverifiedEmail = true }));
        updated.GetProperty("displayName").GetString().Should().Be("Company SSO");
        updated.GetProperty("trustUnverifiedEmail").GetBoolean().Should().BeTrue();

        (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("displayName").GetString().Should().Be("Company SSO");

        (await Browser.DeleteAsync($"/api/settings/oidc/setup/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var gone = await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"), HttpStatusCode.NotFound);
        gone.GetProperty("code").GetString().Should().Be("session_unknown");
        (await _ctx.OidcSettings.GetAllAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("unknown-template")]
    [InlineData("..")]
    [InlineData("")]
    public async Task Session_WithUnknownTemplate_Returns404(string templateId)
    {
        var error = await Expect(Browser.PostJsonAsync("/api/settings/oidc/setup", new { templateId }), HttpStatusCode.NotFound);
        error.GetProperty("code").GetString().Should().Be("template_unknown");
    }

    [Fact]
    public async Task Session_ForUnknownProvider_Returns404()
    {
        var error = await Expect(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "nope" }), HttpStatusCode.NotFound);
        error.GetProperty("code").GetString().Should().Be("provider_unknown");
    }

    [Fact]
    public async Task Session_OfAnotherAdmin_IsNotVisible()
    {
        var id = (await NewSessionAsync()).GetProperty("id").GetString()!;
        using var other = _ctx.NewBrowser();
        other.Token = _ctx.AddUser("admin2", "admin2@example.com", systemAdmin: true, password: "Password123!x");

        (await other.GetAsync($"/api/settings/oidc/setup/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PatchJsonAsync($"/api/settings/oidc/setup/{id}", new { displayName = "x" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/settings/oidc/setup/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Still there for its owner.
        (await Browser.GetAsync($"/api/settings/oidc/setup/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetupEndpoints_RequireSystemAdmin()
    {
        using var anonymous = _ctx.NewBrowser();
        (await anonymous.PostJsonAsync("/api/settings/oidc/setup", new { templateId = "generic-oidc" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        using var user = _ctx.NewBrowser();
        user.Token = _ctx.AddUser("plainuser", "plain@example.com", systemAdmin: false, password: "Password123!x");
        (await user.PostJsonAsync("/api/settings/oidc/setup", new { templateId = "generic-oidc" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await user.DeleteAsync("/api/settings/oidc/providers/oidc")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ discovery and checks

    [Fact]
    public async Task Discovery_NotFound_HasOwnCode()
    {
        var id = await IdAsync(NewSessionAsync());

        var session = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority = $"{Issuer}realms/missing" }));

        var discovery = session.GetProperty("discovery");
        discovery.GetProperty("passed").GetBoolean().Should().BeFalse();
        Item(discovery, OidcCheckIds.Discovery).GetProperty("code").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task Discovery_UnknownHost_HasOwnCode()
    {
        var id = await IdAsync(NewSessionAsync());

        var session = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority = "http://no-such-idp.example/" }));

        var item = Item(session.GetProperty("discovery"), OidcCheckIds.Discovery);
        item.GetProperty("code").GetString().Should().Be(OidcErrorCodes.Unreachable);
        item.GetProperty("detail").GetString().Should().Contain("could not be resolved");
    }

    [Fact]
    public async Task Discovery_WrongIssuer_HasOwnCodeAndNamesBothValues()
    {
        var id = await IdAsync(NewSessionAsync());

        var session = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority = $"{Issuer}wrong-issuer" }));

        var item = Item(session.GetProperty("discovery"), OidcCheckIds.Issuer);
        item.GetProperty("code").GetString().Should().Be("issuer_mismatch");
        item.GetProperty("detail").GetString().Should().Contain(Issuer).And.Contain($"{Issuer}wrong-issuer");
    }

    [Fact]
    public async Task Discovery_Valid_PassesFirstThreeChecks()
    {
        var id = await IdAsync(NewSessionAsync());

        var session = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority = Issuer }));

        var discovery = session.GetProperty("discovery");
        discovery.GetProperty("passed").GetBoolean().Should().BeTrue();
        discovery.GetProperty("items").GetArrayLength().Should().Be(3);
        session.GetProperty("authority").GetString().Should().Be(Issuer);
    }

    [Theory]
    [InlineData("ftp://idp.example/")]
    [InlineData("not an address")]
    [InlineData("")]
    public async Task Discovery_InvalidAuthority_Returns400(string authority)
    {
        var id = await IdAsync(NewSessionAsync());

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority }), HttpStatusCode.BadRequest);

        error.GetProperty("code").GetString().Should().Be("authority_invalid");
    }

    [Fact]
    public async Task Checks_WrongSecret_ClientRejected()
    {
        var id = await PrepareManualSessionAsync(secret: "wrong-secret");

        var checks = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"))).GetProperty("checks");

        checks.GetProperty("passed").GetBoolean().Should().BeFalse();
        checks.GetProperty("failedCount").GetInt32().Should().Be(1);
        checks.GetProperty("executedCount").GetInt32().Should().Be(5);
        Item(checks, OidcCheckIds.Client).GetProperty("code").GetString().Should().Be(OidcErrorCodes.InvalidClient);
    }

    [Fact]
    public async Task Checks_RedirectUriNotRegistered_HasOwnCode()
    {
        var id = await PrepareManualSessionAsync(name: "unregistered");

        var checks = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"))).GetProperty("checks");

        var item = Item(checks, OidcCheckIds.Client);
        item.GetProperty("code").GetString().Should().Be("redirect_uri_rejected");
        item.GetProperty("detail").GetString().Should().Contain(TestIdentityProviderHost.RedirectUri("unregistered"));
    }

    [Fact]
    public async Task Checks_WithoutCredentials_Returns400()
    {
        var id = await IdAsync(NewSessionAsync());
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority = Issuer }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = BaseUrl, name = "oidc" }));

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"), HttpStatusCode.BadRequest);

        error.GetProperty("code").GetString().Should().Be("credentials_missing");
    }

    [Fact]
    public async Task Registration_ScopesWithoutOpenid_Returns400()
    {
        var id = await IdAsync(NewSessionAsync());

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration",
            new { clientId = "x", clientSecret = "y", scopes = "profile email" }), HttpStatusCode.BadRequest);

        error.GetProperty("code").GetString().Should().Be("scopes_invalid");
    }

    // ------------------------------------------------------------------ name and address

    [Theory]
    [InlineData("Bad Name")]
    [InlineData("UPPER")]
    [InlineData("-leading-dash")]
    [InlineData("")]
    public async Task Installation_InvalidName_Returns400(string name)
    {
        var id = await IdAsync(NewSessionAsync());

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = BaseUrl, name }),
            HttpStatusCode.BadRequest);

        error.GetProperty("code").GetString().Should().Be("name_invalid");
    }

    [Fact]
    public async Task Installation_TakenName_Returns409_AndNewSessionSuggestsNextFreeName()
    {
        await _ctx.AddProviderAsync("oidc");
        var session = await NewSessionAsync();
        session.GetProperty("name").GetString().Should().Be("oidc-2");

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{await IdAsync(Task.FromResult(session))}/installation",
            new { baseUrl = BaseUrl, name = "oidc" }), HttpStatusCode.Conflict);

        error.GetProperty("code").GetString().Should().Be("name_taken");
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://server")]
    [InlineData("http://server/?x=1")]
    public async Task Installation_InvalidBaseUrl_Returns400(string baseUrl)
    {
        var id = await IdAsync(NewSessionAsync());

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl, name = "oidc" }),
            HttpStatusCode.BadRequest);

        error.GetProperty("code").GetString().Should().Be("base_url_invalid");
    }

    [Fact]
    public async Task Installation_StoresBaseUrlAndRedirectUri()
    {
        var id = await IdAsync(NewSessionAsync());

        var session = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation",
            new { baseUrl = "https://rsgo.example.com/", name = "corp" }));

        session.GetProperty("redirectUri").GetString().Should().Be("https://rsgo.example.com/api/auth/oidc/corp/callback");
        (await _ctx.SystemConfig.GetConfiguredBaseUrlAsync()).Should().Be("https://rsgo.example.com");
    }

    // ------------------------------------------------------------------ test sign-in and save

    [Fact]
    public async Task TestSignIn_BeforeChecks_Returns409()
    {
        var id = await PrepareManualSessionAsync();

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/test-sign-in"), HttpStatusCode.Conflict);

        error.GetProperty("code").GetString().Should().Be("checks_required");
    }

    [Fact]
    public async Task Save_EnabledWithoutTest_Returns409_SaveDisabled_Works()
    {
        var id = await PrepareManualSessionAsync();
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"));

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/save", new { enabled = true }), HttpStatusCode.Conflict);
        error.GetProperty("code").GetString().Should().Be("test_required");
        (await _ctx.OidcSettings.GetAllAsync()).Should().BeEmpty();

        var saved = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/save", new { enabled = false }));
        saved.GetProperty("enabled").GetBoolean().Should().BeFalse();

        var provider = await _ctx.OidcSettings.GetByNameAsync("oidc");
        provider!.Enabled.Should().BeFalse();
        provider.ClientId.Should().Be(TestIdentityProviderHost.ManualClientId);
        provider.TrustUnverifiedEmail.Should().BeFalse("new providers do not trust unverified addresses");
        // The session is gone after saving.
        (await Browser.GetAsync($"/api/settings/oidc/setup/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullManualSetup_ChecksTestSignInSave_EnablesProvider_AndNeverReturnsTheSecret()
    {
        var id = await PrepareManualSessionAsync();
        var checks = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"))).GetProperty("checks");
        checks.GetProperty("passed").GetBoolean().Should().BeTrue(checks.ToString());

        var back = await TestSignInAsync(id, "alex");
        back.Should().Be($"{BaseUrl}/settings/oidc/add?session={id}");

        var session = await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"));
        var test = session.GetProperty("testSignIn");
        test.GetProperty("passed").GetBoolean().Should().BeTrue();
        test.GetProperty("current").GetBoolean().Should().BeTrue();
        test.GetProperty("signedInAs").GetString().Should().Be("alex@example.com");
        session.GetProperty("canEnable").GetBoolean().Should().BeTrue();
        session.GetProperty("hasClientSecret").GetBoolean().Should().BeTrue();

        var saved = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/save", new { enabled = true }));
        saved.GetProperty("enabled").GetBoolean().Should().BeTrue();

        var list = await Expect(Browser.GetAsync("/api/settings/oidc"));
        var provider = list.GetProperty("providers")[0];
        provider.GetProperty("enabled").GetBoolean().Should().BeTrue();
        provider.GetProperty("hasPassedTestSignIn").GetBoolean().Should().BeTrue();
        provider.GetProperty("hasClientSecret").GetBoolean().Should().BeTrue();

        // No response of the whole run carried the secret, and the file stores it encrypted.
        Browser.RsgoResponses.Should().NotContain(body => body.Contains(TestIdentityProviderHost.ManualClientSecret));
        (await File.ReadAllTextAsync(_ctx.OidcConfigFile)).Should().NotContain(TestIdentityProviderHost.ManualClientSecret);

        // The new provider signs in an existing user with a verified email.
        _ctx.AddUser("alexuser", "alex@example.com", systemAdmin: false, password: "Password123!x");
        using var signIn = _ctx.NewBrowser();
        (await signIn.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "alex")).Should().StartWith($"{BaseUrl}/oidc-callback#token=");
    }

    [Fact]
    public async Task TestSignIn_UnverifiedEmail_PassesWithWarning()
    {
        var id = await PrepareCheckedSessionAsync();

        await TestSignInAsync(id, "uma");

        var test = (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("testSignIn");
        test.GetProperty("passed").GetBoolean().Should().BeTrue();
        test.GetProperty("warningTitle").GetString().Should().NotBeNullOrEmpty();
        test.GetProperty("claims").EnumerateArray().Single(c => c.GetProperty("claim").GetString() == "email")
            .GetProperty("status").GetString().Should().Be("notVerified");
    }

    [Fact]
    public async Task TestSignIn_UserWithoutUsername_ReportsMissingClaim()
    {
        var id = await PrepareCheckedSessionAsync();

        await TestSignInAsync(id, "noah");

        var test = (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("testSignIn");
        test.GetProperty("passed").GetBoolean().Should().BeTrue();
        test.GetProperty("claims").EnumerateArray().Single(c => c.GetProperty("claim").GetString() == "preferred_username")
            .GetProperty("status").GetString().Should().Be("missing");
    }

    [Fact]
    public async Task TestSignIn_Cancelled_IsNotPassed()
    {
        var id = await PrepareCheckedSessionAsync();

        await TestSignInAsync(id, userId: null);

        var session = await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"));
        session.GetProperty("testSignIn").GetProperty("passed").GetBoolean().Should().BeFalse();
        session.GetProperty("canEnable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TestSignIn_ReturningInAnotherBrowser_IsLost()
    {
        var id = await PrepareCheckedSessionAsync();
        var start = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/test-sign-in"));

        // Another browser (no flow cookie) finishes the round trip.
        using var other = _ctx.NewBrowser();
        var back = await other.SignInThroughAsync(start.GetProperty("url").GetString()!, "alex");

        back.Should().Be($"{BaseUrl}/settings/oidc?error=test_sign_in_lost");
        (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("testSignIn").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ------------------------------------------------------------------ existing providers

    [Fact]
    public async Task EnabledProvider_ChangedConnectionWithoutTest_KeepsOld_AfterTestSignIn_TakesNew()
    {
        await _ctx.AddProviderAsync("oidc");

        // Change the client without a test sign-in, then "Save changes".
        var first = await IdAsync(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "oidc" }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{first}/registration",
            new { clientId = TestIdentityProviderHost.SecondClientId, clientSecret = TestIdentityProviderHost.SecondClientSecret }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{first}/save", new { enabled = true }));

        var provider = await _ctx.OidcSettings.GetByNameAsync("oidc");
        provider!.ClientId.Should().Be(TestIdentityProviderHost.ManualClientId, "an enabled provider keeps its connection until a test sign-in passes");
        provider.ClientSecret.Should().Be(TestIdentityProviderHost.ManualClientSecret);
        provider.Enabled.Should().BeTrue();

        // Same change with checks and a passed test sign-in: taken over at the callback.
        var second = await IdAsync(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "oidc" }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{second}/registration",
            new { clientId = TestIdentityProviderHost.SecondClientId, clientSecret = TestIdentityProviderHost.SecondClientSecret }));
        (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{second}/checks"))).GetProperty("checks")
            .GetProperty("passed").GetBoolean().Should().BeTrue();
        var back = await TestSignInAsync(second, "alex");
        back.Should().Be($"{BaseUrl}/settings/oidc/providers/oidc?session={second}");

        provider = await _ctx.OidcSettings.GetByNameAsync("oidc");
        provider!.ClientId.Should().Be(TestIdentityProviderHost.SecondClientId);
        provider.ClientSecret.Should().Be(TestIdentityProviderHost.SecondClientSecret);
        provider.HasPassedTestSignInForCurrentConnection.Should().BeTrue();
    }

    [Fact]
    public async Task DisabledProvider_SaveChanges_TakesConnectionRightAway()
    {
        await _ctx.AddProviderAsync("oidc", enabled: false, tested: false);
        var id = await IdAsync(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "oidc" }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration",
            new { clientId = TestIdentityProviderHost.SecondClientId, clientSecret = "" }));

        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/save", new { enabled = false }));

        var provider = await _ctx.OidcSettings.GetByNameAsync("oidc");
        provider!.ClientId.Should().Be(TestIdentityProviderHost.SecondClientId);
        provider.ClientSecret.Should().Be(TestIdentityProviderHost.ManualClientSecret, "an empty secret keeps the stored one");
    }

    [Fact]
    public async Task DisabledUntestedProvider_CannotBeEnabled()
    {
        await _ctx.AddProviderAsync("oidc", enabled: false, tested: false);
        var id = await IdAsync(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "oidc" }));

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/save", new { enabled = true }), HttpStatusCode.Conflict);

        error.GetProperty("code").GetString().Should().Be("test_required");
        (await _ctx.OidcSettings.GetByNameAsync("oidc"))!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task OwnOnlyProvider_WithoutPassword_CannotBeDisabledOrRemoved()
    {
        await _ctx.AddProviderAsync("oidc");
        Browser.Token = _ctx.AddUser("ssoadmin", "sso.admin@example.com", systemAdmin: true, link: ("oidc", "sub-sso-admin"));

        var removal = await Expect(Browser.DeleteAsync("/api/settings/oidc/providers/oidc"), HttpStatusCode.Conflict);
        removal.GetProperty("code").GetString().Should().Be("lockout");

        var id = await IdAsync(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "oidc" }));
        var disable = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/save", new { enabled = false }), HttpStatusCode.Conflict);
        disable.GetProperty("code").GetString().Should().Be("lockout");

        (await _ctx.OidcSettings.GetByNameAsync("oidc"))!.Enabled.Should().BeTrue();
        var list = await Expect(Browser.GetAsync("/api/settings/oidc"));
        list.GetProperty("providers")[0].GetProperty("isOnlySignInOfCurrentUser").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task OwnProvider_WithAnotherEnabledLinkedProvider_CanBeRemoved()
    {
        await _ctx.AddProviderAsync("oidc");
        await _ctx.AddProviderAsync("oidc-2", TestIdentityProviderHost.SecondClientId, TestIdentityProviderHost.SecondClientSecret);
        Browser.Token = _ctx.AddUser("ssoadmin", "sso.admin@example.com", systemAdmin: true, link: ("oidc", "sub-sso-admin"));
        using (var scope = _ctx.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = users.FindByUsername("ssoadmin")!;
            user.LinkExternalIdentity("oidc-2", "sub-sso-admin-2");
            users.Update(user);
        }

        (await Browser.DeleteAsync("/api/settings/oidc/providers/oidc")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveProvider_DeletesAccountLinks()
    {
        await _ctx.AddProviderAsync("oidc");
        _ctx.AddUser("linked", "linked@example.com", systemAdmin: false, password: "Password123!x", link: ("oidc", "sub-linked"));

        (await Browser.DeleteAsync("/api/settings/oidc/providers/oidc")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _ctx.OidcSettings.GetAllAsync()).Should().BeEmpty();
        _ctx.FindUser("linked")!.FindExternalIdentity("oidc").Should().BeNull();

        // A new provider with the same name does not inherit the old subject.
        await _ctx.AddProviderAsync("oidc");
        _ctx.FindUser("linked")!.FindExternalIdentity("oidc").Should().BeNull();
    }

    [Fact]
    public async Task RemoveProvider_Unknown_Returns404()
    {
        var error = await Expect(Browser.DeleteAsync("/api/settings/oidc/providers/nope"), HttpStatusCode.NotFound);
        error.GetProperty("code").GetString().Should().Be("provider_unknown");
    }

    // ------------------------------------------------------------------ pairing

    [Fact]
    public async Task Pairing_ConnectAndComplete_StoresPairedClient_ChecksPass()
    {
        var id = await IdAsync(NewSessionAsync("wysch"));
        var installation = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = BaseUrl, name = "wysch" }));
        installation.GetProperty("authority").GetString().Should().Be(Issuer);

        var registration = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration"));
        var start = registration.GetProperty("start");
        start.GetProperty("url").GetString().Should().Be($"{Issuer}pairing");
        var manifest = JsonDocument.Parse(start.GetProperty("fields").GetProperty("manifest").GetString()!).RootElement;
        manifest.GetProperty("redirect_uri").GetString().Should().Be(TestIdentityProviderHost.RedirectUri("wysch"));
        manifest.GetProperty("return_uri").GetString().Should().Be($"{BaseUrl}/api/sso/registration/callback");

        var back = await Browser.PairAsync(start);
        back.Should().Be($"{BaseUrl}/settings/oidc/add?session={id}");
        (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("registrationPending").GetBoolean().Should().BeTrue();

        var completed = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration/complete"));
        completed.GetProperty("clientId").GetString().Should().StartWith("paired-");
        completed.GetProperty("hasClientSecret").GetBoolean().Should().BeTrue();
        completed.GetProperty("pairedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        var checks = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"))).GetProperty("checks");
        checks.GetProperty("passed").GetBoolean().Should().BeTrue(checks.ToString());

        // Redeeming twice fails: the code is gone.
        var again = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration/complete"), HttpStatusCode.BadRequest);
        again.GetProperty("code").GetString().Should().Be("registration_pending");
    }

    [Fact]
    public async Task Pairing_CancelledAtProvider_ReportsAccessDenied()
    {
        var id = await IdAsync(NewSessionAsync("wysch"));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = BaseUrl, name = "wysch" }));
        var start = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration"))).GetProperty("start");

        await Browser.PairAsync(start, connect: false);

        (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("registrationError").GetString().Should().Be("access_denied");
        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration/complete"), HttpStatusCode.BadRequest);
        error.GetProperty("code").GetString().Should().Be("access_denied");
    }

    [Fact]
    public async Task Pairing_ReturnInAnotherBrowser_IsLost()
    {
        var id = await IdAsync(NewSessionAsync("wysch"));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = BaseUrl, name = "wysch" }));
        var start = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration"))).GetProperty("start");

        using var other = _ctx.NewBrowser();
        var back = await other.PairAsync(start);

        back.Should().Be($"{BaseUrl}/settings/oidc?error=registration_lost");
        (await Expect(Browser.GetAsync($"/api/settings/oidc/setup/{id}"))).GetProperty("registrationPending").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Reconnect_AfterRevocation_TakesNewPairingAfterTestSignIn_AndClearsReconnectNeeded()
    {
        await _ctx.AddProviderAsync("wysch", TestIdentityProviderHost.PairedStaticClientId, TestIdentityProviderHost.PairedStaticClientSecret,
            registration: RegistrationKinds.Pairing);
        using (var idp = _ctx.Idp.CreateBrowserClient())
        {
            await idp.PostAsync($"test/clients/{TestIdentityProviderHost.PairedStaticClientId}/revoke", null);
        }
        using (var signIn = _ctx.NewBrowser())
        {
            (await signIn.NavigateAsync($"{BaseUrl}/api/auth/oidc/wysch/challenge")).Url.Should().EndWith("error=oidc_provider_rejected");
        }
        (await Expect(Browser.GetAsync("/api/settings/oidc"))).GetProperty("providers")[0].GetProperty("reconnectNeeded").GetBoolean().Should().BeTrue();

        // "Reconnect": pair again on the provider's page, check, test sign-in.
        var id = await IdAsync(Browser.PostJsonAsync("/api/settings/oidc/setup", new { provider = "wysch" }));
        var start = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration"))).GetProperty("start");
        (await Browser.PairAsync(start)).Should().Be($"{BaseUrl}/settings/oidc/providers/wysch?session={id}");
        var newClient = (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration/complete"))).GetProperty("clientId").GetString();
        (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"))).GetProperty("checks").GetProperty("passed").GetBoolean().Should().BeTrue();

        // Until the test sign-in passed, the old (revoked) client stays.
        (await _ctx.OidcSettings.GetByNameAsync("wysch"))!.ClientId.Should().Be(TestIdentityProviderHost.PairedStaticClientId);

        await TestSignInAsync(id, "alex");

        var provider = await _ctx.OidcSettings.GetByNameAsync("wysch");
        provider!.ClientId.Should().Be(newClient);
        provider.ReconnectNeeded.Should().BeFalse();
        provider.PairedAt.Should().NotBeNull();
        provider.LastResult!.Passed.Should().BeTrue();
        _ctx.AddUser("alexuser", "alex@example.com", systemAdmin: false, password: "Password123!x");
        using var later = _ctx.NewBrowser();
        (await later.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/wysch/challenge", "alex")).Should().StartWith($"{BaseUrl}/oidc-callback#token=");
    }

    [Fact]
    public async Task Pairing_WithoutHttps_IsRejected()
    {
        var id = await IdAsync(NewSessionAsync("wysch"));

        var error = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = "http://server:8080", name = "wysch" }),
            HttpStatusCode.BadRequest);

        error.GetProperty("code").GetString().Should().Be("https_required");
    }

    // ------------------------------------------------------------------ helpers

    private static Task<JsonElement> Expect(Task<HttpResponseMessage> call, HttpStatusCode status = HttpStatusCode.OK) =>
        SsoBrowser.ExpectAsync(call, status);

    private static JsonElement Item(JsonElement report, string id) =>
        report.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == id);

    private Task<JsonElement> NewSessionAsync(string templateId = "generic-oidc") =>
        Expect(Browser.PostJsonAsync("/api/settings/oidc/setup", new { templateId }));

    private static async Task<string> IdAsync(Task<JsonElement> session) => (await session).GetProperty("id").GetString()!;

    private static async Task<string> IdAsync(Task<HttpResponseMessage> call) => await IdAsync(Expect(call));

    private async Task<string> PrepareManualSessionAsync(
        string name = "oidc",
        string clientId = TestIdentityProviderHost.ManualClientId,
        string secret = TestIdentityProviderHost.ManualClientSecret)
    {
        var id = await IdAsync(NewSessionAsync());
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/discovery", new { authority = Issuer }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/installation", new { baseUrl = BaseUrl, name }));
        await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/registration", new { clientId, clientSecret = secret }));
        return id;
    }

    private async Task<string> PrepareCheckedSessionAsync()
    {
        var id = await PrepareManualSessionAsync();
        (await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{id}/checks"))).GetProperty("checks")
            .GetProperty("passed").GetBoolean().Should().BeTrue();
        return id;
    }

    /// <summary>Starts the test sign-in and plays it through in the browser; returns the UI address it ends on.</summary>
    private async Task<string> TestSignInAsync(string sessionId, string? userId)
    {
        var start = await Expect(Browser.PostJsonAsync($"/api/settings/oidc/setup/{sessionId}/test-sign-in"));
        return await Browser.SignInThroughAsync(start.GetProperty("url").GetString()!, userId);
    }
}

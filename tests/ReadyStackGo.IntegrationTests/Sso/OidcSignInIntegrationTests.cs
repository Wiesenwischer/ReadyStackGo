using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// Sign-in from the login page against the test identity provider: PAR, account mapping,
/// provider errors (revoked client, provider down), unusable emails and old provider entries.
/// </summary>
public class OidcSignInIntegrationTests : IAsyncLifetime
{
    private const string BaseUrl = TestIdentityProviderHost.RsgoBaseUrl;

    private SsoTestContext _ctx = null!;
    private SsoBrowser Browser => _ctx.Browser;

    public async Task InitializeAsync()
    {
        _ctx = await SsoTestContext.StartAsync();
        await _ctx.CreatePasswordAdminAsync();
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task Challenge_UsesPushedAuthorizationRequest_RedirectCarriesOnlyClientIdAndRequestUri()
    {
        await _ctx.AddProviderAsync();

        var response = await Browser.GetAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).Should().Be($"{TestIdentityProviderHost.Issuer}authorize");
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        query.AllKeys.Should().BeEquivalentTo("client_id", "request_uri");
        query["client_id"].Should().Be(TestIdentityProviderHost.ManualClientId);
        query["request_uri"].Should().StartWith("urn:ietf:params:oauth:request_uri:");
    }

    [Fact]
    public async Task Challenge_ForDisabledOrUnknownProvider_Returns404()
    {
        await _ctx.AddProviderAsync(enabled: false);

        (await Browser.GetAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Browser.GetAsync($"{BaseUrl}/api/auth/oidc/nope/challenge")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Callback_MapsThroughSubject_EvenWhenTheEmailDiffers()
    {
        await _ctx.AddProviderAsync();
        _ctx.AddUser("alexlinked", "alex.old@example.com", systemAdmin: false, password: "Password123!x",
            link: ("oidc", "sub-alex-verified"));

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "alex");

        final.Should().StartWith($"{BaseUrl}/oidc-callback#token=");
        Browser.Token = Uri.UnescapeDataString(final.Split("#token=")[1]);
        var profile = await SsoBrowser.ExpectAsync(Browser.GetAsync("/api/user/profile"), HttpStatusCode.OK);
        profile.GetProperty("username").GetString().Should().Be("alexlinked");
    }

    [Fact]
    public async Task Callback_ByVerifiedEmail_LinksExistingUser()
    {
        await _ctx.AddProviderAsync();
        _ctx.AddUser("alexmail", "alex@example.com", systemAdmin: false, password: "Password123!x", emailVerified: false);

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "alex");

        final.Should().StartWith($"{BaseUrl}/oidc-callback#token=");
        var user = _ctx.FindUser("alexmail")!;
        user.FindExternalIdentity("oidc")!.Subject.Should().Be("sub-alex-verified");
        user.IsEmailVerified.Should().BeTrue("the provider confirmed the address");
    }

    [Fact]
    public async Task Callback_UnverifiedEmailAtNewProvider_IsNotMappedByEmail()
    {
        await _ctx.AddProviderAsync(trustUnverifiedEmail: false);
        _ctx.AddUser("umamail", "uma@example.com", systemAdmin: false, password: "Password123!x");

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "uma");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.EmailUnverified}");
        _ctx.FindUser("umamail")!.FindExternalIdentity("oidc").Should().BeNull();
    }

    [Fact]
    public async Task Callback_WithoutAccount_RedirectsWithNoAccount()
    {
        await _ctx.AddProviderAsync();

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "noah");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.NoAccount}");
    }

    [Fact]
    public async Task Callback_CancelledAtProvider_RedirectsWithFailed()
    {
        await _ctx.AddProviderAsync();

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", userId: null);

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.Failed}");
    }

    [Fact]
    public async Task Callback_WithUnknownState_RedirectsWithStateError()
    {
        await _ctx.AddProviderAsync();

        var (final, _) = await Browser.NavigateAsync($"{BaseUrl}/api/auth/oidc/oidc/callback?code=abc&state=forged");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.State}");
    }

    [Fact]
    public async Task Callback_EmailWithPlus_RedirectsWithEmailInvalid()
    {
        await _ctx.AddProviderAsync();

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "pat");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.EmailInvalid}");
    }

    [Fact]
    public async Task Callback_ProviderUnreachableAtTokenExchange_RedirectsWithUnreachable()
    {
        await _ctx.AddProviderAsync();
        var (authorizeUrl, _) = await Browser.NavigateAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge");

        // The provider goes down between the redirect to it and the return.
        _ctx.Idp.Unreachable = true;
        var final = await Browser.SignInAtProviderAsync(authorizeUrl, "alex");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.Unreachable}");
        var provider = await _ctx.OidcSettings.GetByNameAsync("oidc");
        provider!.LastResult!.Passed.Should().BeFalse();
        provider.ReconnectNeeded.Should().BeFalse("an unreachable provider is no reason to reconnect");
    }

    [Fact]
    public async Task Challenge_ProviderUnreachable_RedirectsWithUnreachable()
    {
        await _ctx.AddProviderAsync();
        _ctx.Idp.Unreachable = true;

        var (final, _) = await Browser.NavigateAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.Unreachable}");
    }

    [Fact]
    public async Task Challenge_RevokedClientOfPairedProvider_SetsReconnectNeeded_AndRedirectsWithProviderRejected()
    {
        await _ctx.AddProviderAsync("wysch", TestIdentityProviderHost.PairedStaticClientId, TestIdentityProviderHost.PairedStaticClientSecret,
            registration: RegistrationKinds.Pairing);
        await RevokeAsync(TestIdentityProviderHost.PairedStaticClientId);

        var (final, _) = await Browser.NavigateAsync($"{BaseUrl}/api/auth/oidc/wysch/challenge");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.ProviderRejected}");
        var provider = await _ctx.OidcSettings.GetByNameAsync("wysch");
        provider!.ReconnectNeeded.Should().BeTrue();
        provider.LastResult.Should().NotBeNull();
        provider.LastResult!.Passed.Should().BeFalse();
        provider.LastResult.Kind.Should().Be(OidcResultKinds.SignIn);
    }

    [Fact]
    public async Task Callback_RevokedClientOfPairedProviderAtTokenExchange_SetsReconnectNeeded()
    {
        await _ctx.AddProviderAsync("wysch", TestIdentityProviderHost.PairedStaticClientId, TestIdentityProviderHost.PairedStaticClientSecret,
            registration: RegistrationKinds.Pairing);
        var (authorizeUrl, _) = await Browser.NavigateAsync($"{BaseUrl}/api/auth/oidc/wysch/challenge");
        await RevokeAsync(TestIdentityProviderHost.PairedStaticClientId);

        var final = await Browser.SignInAtProviderAsync(authorizeUrl, "alex");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.ProviderRejected}");
        (await _ctx.OidcSettings.GetByNameAsync("wysch"))!.ReconnectNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Challenge_RevokedClientOfManualProvider_RecordsResultWithoutReconnect()
    {
        await _ctx.AddProviderAsync();
        await RevokeAsync(TestIdentityProviderHost.ManualClientId);

        var (final, _) = await Browser.NavigateAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.ProviderRejected}");
        var provider = await _ctx.OidcSettings.GetByNameAsync("oidc");
        provider!.ReconnectNeeded.Should().BeFalse("only paired providers can be reconnected");
        provider.LastResult!.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Callback_DisabledAccount_IsRejected()
    {
        await _ctx.AddProviderAsync();
        _ctx.AddUser("alexoff", "alex@example.com", systemAdmin: false, password: "Password123!x", link: ("oidc", "sub-alex-verified"));
        using (var scope = _ctx.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Domain.IdentityAccess.Users.IUserRepository>();
            var user = users.FindByUsername("alexoff")!;
            user.Disable();
            users.Update(user);
        }

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/oidc/challenge", "alex");

        final.Should().Be($"{BaseUrl}/login?error={OidcSignInErrors.AccountDisabled}");
    }

    [Fact]
    public async Task OldRsgoOidcJsonEntry_WithoutNewFields_StillSignsIn()
    {
        // An entry as written before templates existed: upper-case name, no template,
        // registration, claims or trust setting.
        var encryption = _ctx.Services.GetRequiredService<ICredentialEncryptionService>();
        var legacy = new
        {
            providers = new[]
            {
                new
                {
                    name = "Legacy",
                    displayName = "Legacy SSO",
                    authority = TestIdentityProviderHost.Issuer,
                    clientId = TestIdentityProviderHost.ManualClientId,
                    encryptedClientSecret = encryption.Encrypt(TestIdentityProviderHost.ManualClientSecret),
                    scopes = "openid email profile",
                    enabled = true
                }
            }
        };
        await File.WriteAllTextAsync(_ctx.OidcConfigFile, JsonSerializer.Serialize(legacy));
        // Old providers trust unverified email addresses: Uma is matched by email.
        _ctx.AddUser("umaold", "uma@example.com", systemAdmin: false, password: "Password123!x");

        var providers = await SsoBrowser.ExpectAsync(Browser.GetAsync("/api/auth/oidc/providers"), HttpStatusCode.OK);
        providers.EnumerateArray().Select(p => p.GetProperty("name").GetString()).Should().Equal("Legacy");

        var final = await Browser.SignInThroughAsync($"{BaseUrl}/api/auth/oidc/Legacy/challenge", "uma");

        final.Should().StartWith($"{BaseUrl}/oidc-callback#token=");
        _ctx.FindUser("umaold")!.FindExternalIdentity("Legacy")!.Subject.Should().Be("sub-uma-unverified");
        var stored = await _ctx.OidcSettings.GetByNameAsync("legacy");
        stored!.Template.Should().Be(OidcProviderSettings.GenericTemplateId);
        stored.Registration.Should().Be(RegistrationKinds.Manual);
        stored.TrustUnverifiedEmail.Should().BeTrue();
    }

    private async Task RevokeAsync(string clientId)
    {
        using var idp = _ctx.Idp.CreateBrowserClient();
        (await idp.PostAsync($"test/clients/{clientId}/revoke", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

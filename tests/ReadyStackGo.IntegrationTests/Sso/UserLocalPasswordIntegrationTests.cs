using System.Net;
using FluentAssertions;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// Profile of users created through an identity provider: hasPassword, the "no administrator
/// has a local password" warning and POST /api/user/set-password.
/// </summary>
public class UserLocalPasswordIntegrationTests : IAsyncLifetime
{
    private const string NewPassword = "Local-Password-123!";

    private SsoTestContext _ctx = null!;

    public async Task InitializeAsync() => _ctx = await SsoTestContext.StartAsync();

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public async Task AdminWithoutPassword_SeesWarning_SetsPassword_ThenSignsInWithIt()
    {
        var browser = _ctx.Browser;
        browser.Token = _ctx.AddUser("ssoadmin", "sso.admin@example.com", systemAdmin: true, link: ("wysch", "sub-sso-admin"));

        var before = await Expect(browser.GetAsync("/api/user/profile"));
        before.GetProperty("hasPassword").GetBoolean().Should().BeFalse();
        before.GetProperty("noAdminWithPassword").GetBoolean().Should().BeTrue();
        before.GetProperty("systemAdminCount").GetInt32().Should().Be(1);
        (await Expect(browser.GetAsync("/api/settings/oidc"))).GetProperty("noAdminWithPassword").GetBoolean().Should().BeTrue();

        (await browser.PostJsonAsync("/api/user/set-password", new { newPassword = NewPassword })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var after = await Expect(browser.GetAsync("/api/user/profile"));
        after.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
        after.GetProperty("noAdminWithPassword").GetBoolean().Should().BeFalse();
        (await Expect(browser.GetAsync("/api/settings/oidc"))).GetProperty("noAdminWithPassword").GetBoolean().Should().BeFalse();

        (await _ctx.LoginAsync("ssoadmin", NewPassword)).Should().NotBeNullOrEmpty();
        _ctx.FindUser("ssoadmin")!.FindExternalIdentity("wysch").Should().NotBeNull("setting a password keeps the link");
    }

    [Fact]
    public async Task SetPassword_WhenUserHasOne_Returns409_AndKeepsTheOldPassword()
    {
        _ctx.Browser.Token = await _ctx.CreatePasswordAdminAsync();

        var response = await _ctx.Browser.PostJsonAsync("/api/user/set-password", new { newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _ctx.LoginAsync("admin", SsoTestContext.AdminPassword)).Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task SetPassword_NotMeetingTheRules_Returns400_AndSetsNothing(string password)
    {
        _ctx.Browser.Token = _ctx.AddUser("ssoadmin", "sso.admin@example.com", systemAdmin: true, link: ("wysch", "sub-sso-admin"));

        var response = await _ctx.Browser.PostJsonAsync("/api/user/set-password", new { newPassword = password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _ctx.FindUser("ssoadmin")!.HasPassword.Should().BeFalse();
    }

    [Fact]
    public async Task SetPassword_Anonymous_Returns401()
    {
        (await _ctx.Browser.PostJsonAsync("/api/user/set-password", new { newPassword = NewPassword })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Warning_OnlyForAdminsWithoutPassword_WhileNoAdminHasOne()
    {
        _ctx.AddUser("ssoadmin", "sso.admin@example.com", systemAdmin: true, link: ("wysch", "sub-sso-admin"));
        var userToken = _ctx.AddUser("ssouser", "sso.user@example.com", systemAdmin: false, link: ("wysch", "sub-sso-user"));
        var passwordAdminToken = _ctx.AddUser("pwadmin", "pw.admin@example.com", systemAdmin: true, password: "Password123!x");

        _ctx.Browser.Token = userToken;
        var user = await Expect(_ctx.Browser.GetAsync("/api/user/profile"));
        user.GetProperty("hasPassword").GetBoolean().Should().BeFalse();
        user.GetProperty("noAdminWithPassword").GetBoolean().Should().BeFalse("plain users are not warned");

        _ctx.Browser.Token = passwordAdminToken;
        var settings = await Expect(_ctx.Browser.GetAsync("/api/settings/oidc"));
        settings.GetProperty("noAdminWithPassword").GetBoolean().Should().BeFalse("one administrator has a password");
        settings.GetProperty("systemAdminCount").GetInt32().Should().Be(2);
    }

    private static Task<System.Text.Json.JsonElement> Expect(Task<HttpResponseMessage> call) =>
        SsoBrowser.ExpectAsync(call, HttpStatusCode.OK);
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// A simulated browser: one cookie jar for ReadyStackGo (the flow cookie rsgo_sso_flow), plain
/// requests to the test identity provider, redirects followed by hand. Optionally carries a
/// bearer token for ReadyStackGo API calls, like the SPA. Records every ReadyStackGo response
/// body so tests can assert that no secret ever leaves the server.
/// </summary>
public sealed partial class SsoBrowser : IDisposable
{
    private readonly HttpClient _rsgo;
    private readonly HttpClient _idp;

    public SsoBrowser(HttpClient rsgo, HttpClient idp)
    {
        _rsgo = rsgo;
        _idp = idp;
    }

    /// <summary>Bearer token sent with ReadyStackGo requests (null: anonymous).</summary>
    public string? Token { get; set; }

    /// <summary>All response bodies ReadyStackGo returned to this browser.</summary>
    public List<string> RsgoResponses { get; } = [];

    // ------------------------------------------------------------------ API calls

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<HttpResponseMessage> DeleteAsync(string url) => SendAsync(new HttpRequestMessage(HttpMethod.Delete, url));

    public Task<HttpResponseMessage> PostJsonAsync(string url, object? body = null) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) });

    public Task<HttpResponseMessage> PatchJsonAsync(string url, object body) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) });

    public Task<HttpResponseMessage> PostFormAsync(string url, IReadOnlyDictionary<string, string> fields) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) });

    /// <summary>Sends to the provider if the URL points at it, otherwise to ReadyStackGo.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        if (IsProvider(request.RequestUri!))
        {
            return await _idp.SendAsync(request);
        }

        if (Token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }
        var response = await _rsgo.SendAsync(request);
        await response.Content.LoadIntoBufferAsync();
        RsgoResponses.Add(await response.Content.ReadAsStringAsync());
        return response;
    }

    /// <summary>Sends and expects a status; returns the JSON body (or default for an empty body).</summary>
    public static async Task<JsonElement> ExpectAsync(Task<HttpResponseMessage> call, HttpStatusCode status)
    {
        var response = await call;
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(status, text);
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    // ------------------------------------------------------------------ navigation

    /// <summary>
    /// Follows redirects starting with <paramref name="response"/> through ReadyStackGo's API and
    /// the provider until a page is reached: a ReadyStackGo UI address (not under /api/) or a
    /// provider page that answers 200. Returns the final address and the last response.
    /// </summary>
    public async Task<(string Url, HttpResponseMessage Response)> FollowAsync(HttpResponseMessage response, string currentUrl)
    {
        var url = currentUrl;
        for (var hops = 0; hops < 15; hops++)
        {
            if (!IsRedirect(response.StatusCode))
            {
                return (url, response);
            }

            var location = response.Headers.Location!;
            url = location.IsAbsoluteUri ? location.ToString() : new Uri(new Uri(url), location).ToString();
            var target = new Uri(url);
            if (!IsProvider(target) && !target.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
            {
                // A page of the single-page application: the browser stops here.
                return (url, response);
            }
            response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
        }
        throw new InvalidOperationException("Too many redirects.");
    }

    public async Task<(string Url, HttpResponseMessage Response)> NavigateAsync(string url) =>
        await FollowAsync(await SendAsync(new HttpRequestMessage(HttpMethod.Get, url)), url);

    /// <summary>Posts a form like a browser (e.g. the pairing form) and follows the redirects.</summary>
    public async Task<(string Url, HttpResponseMessage Response)> SubmitFormAsync(string url, IReadOnlyDictionary<string, string> fields) =>
        await FollowAsync(await PostFormAsync(url, fields), url);

    /// <summary>
    /// On the provider's sign-in page (reached through <paramref name="authorizeUrl"/>): picks a
    /// test user (or cancels) and follows the redirects back to ReadyStackGo.
    /// </summary>
    public async Task<string> SignInAtProviderAsync(string authorizeUrl, string? userId)
    {
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(authorizeUrl).Query);
        var requestUri = query["request_uri"];
        requestUri.Should().NotBeNullOrEmpty("the provider is called with a pushed authorization request");

        var fields = new Dictionary<string, string> { ["request_uri"] = requestUri! };
        if (userId == null)
        {
            fields["cancel"] = "true";
        }
        else
        {
            fields["user"] = userId;
        }

        var (url, _) = await SubmitFormAsync($"{TestIdentityProviderHost.Issuer}authorize/decision", fields);
        return url;
    }

    /// <summary>Opens the authorize page, checks it renders, then signs in.</summary>
    public async Task<string> SignInThroughAsync(string startUrl, string? userId)
    {
        var (authorizeUrl, page) = await NavigateAsync(startUrl);
        if (!IsProvider(new Uri(authorizeUrl)))
        {
            return authorizeUrl;
        }
        page.StatusCode.Should().Be(HttpStatusCode.OK, await page.Content.ReadAsStringAsync());
        return await SignInAtProviderAsync(authorizeUrl, userId);
    }

    /// <summary>
    /// Runs a pairing: posts the form ReadyStackGo returned to the provider, then presses
    /// "Connect" (or "Cancel") on the confirm page and follows the return to ReadyStackGo.
    /// </summary>
    public async Task<string> PairAsync(JsonElement start, bool connect = true)
    {
        start.GetProperty("kind").GetString().Should().Be("formPost");
        var url = start.GetProperty("url").GetString()!;
        var fields = start.GetProperty("fields").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);

        var (_, page) = await SubmitFormAsync(url, fields);
        var html = await page.Content.ReadAsStringAsync();
        page.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain(">Connect</button>").And.Contain(">Cancel</button>");

        var pairingId = PairingField().Match(html).Groups[1].Value;
        pairingId.Should().NotBeEmpty();

        var (returnUrl, _) = await SubmitFormAsync($"{TestIdentityProviderHost.Issuer}pairing/decision", new Dictionary<string, string>
        {
            ["pairing"] = pairingId,
            ["decision"] = connect ? "connect" : "cancel"
        });
        return returnUrl;
    }

    public static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;

    private static bool IsProvider(Uri uri) => uri.IsAbsoluteUri &&
        string.Equals(uri.Authority, TestIdentityProviderHost.Authority, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("name=\"pairing\" value=\"([^\"]+)\"")]
    private static partial Regex PairingField();

    public void Dispose()
    {
        _rsgo.Dispose();
        _idp.Dispose();
    }
}

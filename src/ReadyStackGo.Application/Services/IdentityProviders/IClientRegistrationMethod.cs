namespace ReadyStackGo.Application.Services.IdentityProviders;

/// <summary>
/// A way to obtain client credentials at an identity provider ("manual" entry, "pairing").
/// Templates name the kind; new kinds (another pairing protocol, RFC 7591) plug in here
/// without changing the setup flow or the templates.
/// </summary>
public interface IClientRegistrationMethod
{
    /// <summary>The kind as written in template.json, e.g. "manual" or "pairing".</summary>
    string Kind { get; }

    /// <summary>What the setup flow shows for this kind.</summary>
    RegistrationInteraction Interaction { get; }

    /// <summary>
    /// Starts the registration: either a browser round trip (form post or redirect) or a
    /// completed registration (server-only kinds).
    /// </summary>
    /// <exception cref="ClientRegistrationException">The provider is not reachable or rejected the request.</exception>
    Task<RegistrationStart> StartAsync(RegistrationContext context, CancellationToken cancellationToken);

    /// <summary>Redeems the code the browser brought back and returns the credentials.</summary>
    /// <exception cref="ClientRegistrationException">The code was rejected or the provider is not reachable.</exception>
    Task<RegisteredClient> CompleteAsync(RegistrationContext context, string code, CancellationToken cancellationToken);
}

public enum RegistrationInteraction
{
    /// <summary>The user copies the redirect URI and enters client ID and secret ("Register").</summary>
    ManualEntry,

    /// <summary>A "Connect with &lt;provider&gt;" button starts the registration ("Connect").</summary>
    Connect
}

/// <summary>Everything a registration needs to know about the provider and this installation.</summary>
public sealed record RegistrationContext(
    string Authority,
    string ProviderName,
    string InstallationName,
    string BaseUrl,
    string RedirectUri,
    string ReturnUri,
    string State);

public enum RegistrationStartKind
{
    /// <summary>The browser posts <see cref="RegistrationStart.Fields"/> to <see cref="RegistrationStart.Url"/>.</summary>
    FormPost,

    /// <summary>The browser navigates to <see cref="RegistrationStart.Url"/>.</summary>
    Redirect,

    /// <summary>No browser round trip; <see cref="RegistrationStart.Client"/> holds the result.</summary>
    Completed
}

public sealed record RegistrationStart(
    RegistrationStartKind Kind,
    string? Url,
    IReadOnlyDictionary<string, string>? Fields,
    RegisteredClient? Client)
{
    public static RegistrationStart FormPost(string url, IReadOnlyDictionary<string, string> fields) =>
        new(RegistrationStartKind.FormPost, url, fields, null);

    public static RegistrationStart Completed(RegisteredClient client) =>
        new(RegistrationStartKind.Completed, null, null, client);
}

/// <summary>Credentials obtained through a registration.</summary>
public sealed record RegisteredClient(
    string ClientId,
    string ClientSecret,
    string? Issuer,
    DateTime RegisteredAt,
    string? ConfirmedBy);

/// <summary>A registration failed for a reason the setup flow explains to the user.</summary>
public class ClientRegistrationException : Exception
{
    public ClientRegistrationException(string code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    /// <summary>
    /// Machine-readable reason: "unreachable", "not_supported", "invalid_grant",
    /// "invalid_request", "rate_limited", "rejected".
    /// </summary>
    public string Code { get; }
}

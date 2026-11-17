namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>
/// Checks a provider connection from the server, without anyone signing in: discovery,
/// issuer, endpoints, PAR and (where PAR is offered) client ID and secret. Always fetches a
/// fresh discovery document.
/// </summary>
public interface IOidcConnectionChecker
{
    Task<OidcCheckReport> CheckAsync(OidcCheckRequest request, OidcCheckScope scope, CancellationToken cancellationToken = default);
}

public enum OidcCheckScope
{
    /// <summary>Checks 1 to 3: discovery, issuer, endpoints (step "Provider address").</summary>
    Discovery,

    /// <summary>All checks, including PAR and client credentials.</summary>
    Full
}

/// <param name="RequirePar">The template requires pushed authorization requests.</param>
/// <param name="RedirectUri">The real redirect URI, used for the PAR call that checks the credentials.</param>
public sealed record OidcCheckRequest(
    string Authority,
    string? ClientId,
    string? ClientSecret,
    string Scopes,
    string? RedirectUri,
    bool RequirePar);

public static class OidcCheckIds
{
    public const string Discovery = "discovery";
    public const string Issuer = "issuer";
    public const string Endpoints = "endpoints";
    public const string Par = "par";
    public const string Client = "client";
}

public static class OidcCheckStatus
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

/// <summary>One check with a plain-language title and detail.</summary>
/// <param name="Code">Machine-readable reason of a failure (e.g. "not_found", "issuer_mismatch", "invalid_client").</param>
public sealed record OidcCheckItem(string Id, string Status, string Title, string? Detail, string? Code = null);

public sealed record OidcCheckReport(IReadOnlyList<OidcCheckItem> Items, string? Issuer, bool ParOffered, bool ParRequired)
{
    /// <summary>Checks that ran (not skipped).</summary>
    public int ExecutedCount => Items.Count(i => i.Status != OidcCheckStatus.Skipped);

    public int FailedCount => Items.Count(i => i.Status == OidcCheckStatus.Failed);

    public bool Passed => FailedCount == 0 && ExecutedCount > 0;
}

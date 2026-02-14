using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/verify-registry-access - Verify registry access with optional credentials.
/// If credentials are provided, uses Basic auth during the token flow.
/// If no credentials, checks anonymous access (same as the GET endpoint).
/// Anonymous access (wizard runs before login).
/// </summary>
public class VerifyRegistryAccessEndpoint : Endpoint<VerifyRegistryAccessRequest, VerifyRegistryAccessResponse>
{
    private readonly IRegistryAccessChecker _accessChecker;
    private readonly IImageReferenceExtractor _extractor;

    public VerifyRegistryAccessEndpoint(
        IRegistryAccessChecker accessChecker,
        IImageReferenceExtractor extractor)
    {
        _accessChecker = accessChecker;
        _extractor = extractor;
    }

    public override void Configure()
    {
        Post("/api/wizard/verify-registry-access");
        AllowAnonymous();
        PreProcessor<WizardTimeoutPreProcessor<VerifyRegistryAccessRequest>>();
    }

    public override async Task HandleAsync(VerifyRegistryAccessRequest req, CancellationToken ct)
    {
        var parsed = _extractor.Parse(req.Image);
        var host = parsed.Host;
        var namespacePath = parsed.Namespace;
        var repository = parsed.Repository;

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(repository))
        {
            Response = new VerifyRegistryAccessResponse { AccessLevel = "Unknown" };
            return;
        }

        var hasCredentials = !string.IsNullOrEmpty(req.Username) && !string.IsNullOrEmpty(req.Password);

        var result = hasCredentials
            ? await _accessChecker.CheckAccessAsync(host, namespacePath, repository, req.Username!, req.Password!, ct)
            : await _accessChecker.CheckAccessAsync(host, namespacePath, repository, ct);

        Response = new VerifyRegistryAccessResponse
        {
            AccessLevel = result.ToString(),
            Host = host,
            Namespace = namespacePath,
            Repository = repository
        };
    }
}

public class VerifyRegistryAccessRequest
{
    /// <summary>
    /// A representative image reference to check (e.g., "ghcr.io/myorg/myapp:v1").
    /// </summary>
    public required string Image { get; init; }

    /// <summary>Optional registry username for authenticated check.</summary>
    public string? Username { get; init; }

    /// <summary>Optional registry password or access token for authenticated check.</summary>
    public string? Password { get; init; }
}

public class VerifyRegistryAccessResponse
{
    /// <summary>"Public", "AuthRequired", or "Unknown"</summary>
    public required string AccessLevel { get; init; }

    public string Host { get; init; } = "";
    public string Namespace { get; init; } = "";
    public string Repository { get; init; } = "";
}

using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// GET /api/wizard/check-registry-access - Check if a registry allows anonymous image pulls.
/// Uses the Docker v2 API token auth flow to verify access.
/// Anonymous access (wizard runs before login).
/// </summary>
public class CheckRegistryAccessEndpoint : Endpoint<CheckRegistryAccessRequest, CheckRegistryAccessResponse>
{
    private readonly IRegistryAccessChecker _accessChecker;
    private readonly IImageReferenceExtractor _extractor;

    public CheckRegistryAccessEndpoint(
        IRegistryAccessChecker accessChecker,
        IImageReferenceExtractor extractor)
    {
        _accessChecker = accessChecker;
        _extractor = extractor;
    }

    public override void Configure()
    {
        Get("/api/wizard/check-registry-access");
        AllowAnonymous();
        PreProcessor<WizardTimeoutPreProcessor<CheckRegistryAccessRequest>>();
    }

    public override async Task HandleAsync(CheckRegistryAccessRequest req, CancellationToken ct)
    {
        // Parse a representative image to get host/namespace/repository
        string host, namespacePath, repository;

        if (!string.IsNullOrEmpty(req.Image))
        {
            var parsed = _extractor.Parse(req.Image);
            host = parsed.Host;
            namespacePath = parsed.Namespace;
            repository = parsed.Repository;
        }
        else
        {
            host = req.Host;
            namespacePath = req.Namespace;
            repository = req.Repository;
        }

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(repository))
        {
            Response = new CheckRegistryAccessResponse { AccessLevel = "Unknown" };
            return;
        }

        var result = await _accessChecker.CheckAccessAsync(host, namespacePath, repository, ct);

        Response = new CheckRegistryAccessResponse
        {
            AccessLevel = result.ToString(),
            Host = host,
            Namespace = namespacePath,
            Repository = repository
        };
    }
}

public class CheckRegistryAccessRequest
{
    /// <summary>
    /// Full image reference to check (e.g., "ghcr.io/myorg/myapp:v1").
    /// If provided, host/namespace/repository are extracted automatically.
    /// </summary>
    [QueryParam]
    public string Image { get; init; } = "";

    /// <summary>Registry host (e.g., "ghcr.io"). Used when Image is not provided.</summary>
    [QueryParam]
    public string Host { get; init; } = "";

    /// <summary>Image namespace (e.g., "myorg"). Used when Image is not provided.</summary>
    [QueryParam]
    public string Namespace { get; init; } = "";

    /// <summary>Repository name (e.g., "myapp"). Used when Image is not provided.</summary>
    [QueryParam]
    public string Repository { get; init; } = "";
}

public class CheckRegistryAccessResponse
{
    /// <summary>"Public", "AuthRequired", or "Unknown"</summary>
    public required string AccessLevel { get; init; }

    public string Host { get; init; } = "";
    public string Namespace { get; init; } = "";
    public string Repository { get; init; } = "";
}

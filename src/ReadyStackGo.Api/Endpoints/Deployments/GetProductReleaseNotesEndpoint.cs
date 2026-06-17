using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/environments/{environmentId}/product-deployments/{productDeploymentId}/release-notes?version=X.Y.Z
/// Returns release notes for a product version (own CHANGELOG.md as markdown, or an external URL).
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetProductReleaseNotesEndpoint : Endpoint<GetProductReleaseNotesRequest, GetProductReleaseNotesResponse>
{
    private readonly IMediator _mediator;

    public GetProductReleaseNotesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/release-notes");
        PreProcessor<RbacPreProcessor<GetProductReleaseNotesRequest>>();
    }

    public override async Task HandleAsync(GetProductReleaseNotesRequest req, CancellationToken ct)
    {
        var productDeploymentId = Route<string>("productDeploymentId")!;

        var response = await _mediator.Send(
            new GetProductReleaseNotesQuery(productDeploymentId, req.Version ?? string.Empty), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                || response.Message?.Contains("No release notes", StringComparison.OrdinalIgnoreCase) == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Failed to load release notes", statusCode);
        }

        Response = response;
    }
}

public class GetProductReleaseNotesRequest
{
    /// <summary>Route-bound; used by RBAC for environment scoping.</summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>Query param ?version=X.Y.Z.</summary>
    [QueryParam]
    public string? Version { get; set; }
}

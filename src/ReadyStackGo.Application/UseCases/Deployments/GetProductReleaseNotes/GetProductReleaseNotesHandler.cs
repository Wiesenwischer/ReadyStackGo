using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

/// <summary>
/// Resolves release notes for a product version: prefers a CHANGELOG.md loaded from the
/// product's own source (returned as markdown), otherwise an external release-notes URL
/// (returned as a link, never fetched server-side — SSRF protection).
/// </summary>
public class GetProductReleaseNotesHandler
    : IRequestHandler<GetProductReleaseNotesQuery, GetProductReleaseNotesResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IProductSourceService _productSourceService;

    public GetProductReleaseNotesHandler(
        IProductDeploymentRepository repository,
        IProductSourceService productSourceService)
    {
        _repository = repository;
        _productSourceService = productSourceService;
    }

    public async Task<GetProductReleaseNotesResponse> Handle(
        GetProductReleaseNotesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return GetProductReleaseNotesResponse.Failed("Version is required.");
        }

        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return GetProductReleaseNotesResponse.Failed("Invalid product deployment ID format.");
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return GetProductReleaseNotesResponse.Failed("Product deployment not found.");
        }

        var versions = await _productSourceService.GetProductVersionsAsync(
            productDeployment.ProductGroupId, cancellationToken);

        var definition = versions.FirstOrDefault(v =>
            string.Equals(v.ProductVersion, request.Version, StringComparison.OrdinalIgnoreCase));

        if (definition == null)
        {
            return GetProductReleaseNotesResponse.Failed("Version not found in catalog.");
        }

        // Prefer own CHANGELOG.md (safe to render) over an external URL.
        if (!string.IsNullOrWhiteSpace(definition.ChangelogMarkdown))
        {
            return new GetProductReleaseNotesResponse
            {
                Success = true,
                Mode = "markdown",
                Content = definition.ChangelogMarkdown,
                Version = definition.ProductVersion
            };
        }

        if (!string.IsNullOrWhiteSpace(definition.ReleaseNotesUrl))
        {
            return new GetProductReleaseNotesResponse
            {
                Success = true,
                Mode = "url",
                Url = definition.ReleaseNotesUrl,
                Version = definition.ProductVersion
            };
        }

        return GetProductReleaseNotesResponse.Failed("No release notes available for this version.");
    }
}

using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;

namespace ReadyStackGo.Application.UseCases.Stacks.GetProductReleaseNotes;

/// <summary>
/// Resolves release notes for a catalog product version by its product id, without needing
/// a deployment. Used by the Stack Catalog so operators can read release notes before (or
/// independently of) deploying/upgrading.
/// </summary>
public class GetCatalogProductReleaseNotesHandler
    : IRequestHandler<GetCatalogProductReleaseNotesQuery, GetProductReleaseNotesResponse>
{
    private readonly IProductSourceService _productSourceService;

    public GetCatalogProductReleaseNotesHandler(IProductSourceService productSourceService)
    {
        _productSourceService = productSourceService;
    }

    public async Task<GetProductReleaseNotesResponse> Handle(
        GetCatalogProductReleaseNotesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            return GetProductReleaseNotesResponse.Failed("Product id is required.");
        }

        var definition = await _productSourceService.GetProductAsync(request.ProductId, cancellationToken);
        if (definition == null)
        {
            return GetProductReleaseNotesResponse.Failed("Product not found in catalog.");
        }

        return ReleaseNotesResolver.Resolve(definition, request.Locale);
    }
}

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Stacks.GetProduct;

public class GetProductHandler : IRequestHandler<GetProductQuery, GetProductResult?>
{
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<GetProductHandler> _logger;

    public GetProductHandler(IProductSourceService productSourceService, ILogger<GetProductHandler> logger)
    {
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<GetProductResult?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetProduct request for ProductId: {ProductId}", request.ProductId);

        var product = await _productSourceService.GetProductAsync(request.ProductId, cancellationToken);

        if (product == null)
        {
            _logger.LogWarning("Product not found for ProductId: {ProductId}", request.ProductId);
            return null;
        }

        _logger.LogInformation("Found product: Name={Name}, Version={Version}, IsMultiStack={IsMultiStack}, StackCount={StackCount}, TotalServices={TotalServices}",
            product.Name, product.ProductVersion, product.IsMultiStack, product.Stacks.Count, product.TotalServices);

        var sources = await _productSourceService.GetSourcesAsync(cancellationToken);
        var sourceNames = sources.ToDictionary(s => s.Id.Value, s => s.Name);

        // Get all versions for this product
        var allVersions = await _productSourceService.GetProductVersionsAsync(product.GroupId, cancellationToken);
        var availableVersions = allVersions
            .Select(v => new ProductVersionInfo(
                Version: v.ProductVersion ?? "unknown",
                ProductId: v.Id,
                DefaultStackId: v.DefaultStack?.Id.Value ?? v.Stacks.FirstOrDefault()?.Id.Value ?? v.Id,
                IsCurrent: v.Id == product.Id
            ))
            .ToList();

        var productDetails = new ProductDetails(
            Id: product.Id,
            GroupId: product.GroupId,
            SourceId: product.SourceId,
            SourceName: sourceNames.GetValueOrDefault(product.SourceId, product.SourceId),
            Name: product.DisplayName,
            Description: product.Description,
            Version: product.ProductVersion,
            Category: product.Category,
            Tags: product.Tags?.ToList() ?? new List<string>(),
            IsMultiStack: product.IsMultiStack,
            TotalServices: product.TotalServices,
            TotalVariables: product.TotalVariables,
            Stacks: product.Stacks.Select(s => new ProductStackDetails(
                s.Id.Value,
                s.Name,
                s.Description,
                s.GetServiceNames().ToList(),
                s.Variables.Select(v => new StackVariableDetails(
                    v.Name,
                    v.DefaultValue,
                    v.IsRequired,
                    v.Type.ToString(),
                    v.Label,
                    v.Description,
                    v.Placeholder,
                    v.Group,
                    v.Order,
                    v.Pattern,
                    v.PatternError,
                    v.Min,
                    v.Max,
                    v.Options?.Select(o => new SelectOptionDetails(o.Value, o.Label ?? o.Value, o.Description)).ToList()
                )).ToList()
            )).ToList(),
            LastSyncedAt: product.LastSyncedAt,
            AvailableVersions: availableVersions
        );

        return new GetProductResult(productDetails);
    }
}

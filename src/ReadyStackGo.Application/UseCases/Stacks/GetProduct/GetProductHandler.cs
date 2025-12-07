using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Stacks.GetProduct;

public class GetProductHandler : IRequestHandler<GetProductQuery, GetProductResult?>
{
    private readonly IStackSourceService _stackSourceService;

    public GetProductHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<GetProductResult?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _stackSourceService.GetProductAsync(request.ProductId, cancellationToken);

        if (product == null)
        {
            return null;
        }

        var sources = await _stackSourceService.GetSourcesAsync(cancellationToken);
        var sourceNames = sources.ToDictionary(s => s.Id.Value, s => s.Name);

        var productDetails = new ProductDetails(
            Id: product.Id,
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
                s.Id,
                s.Name,
                s.Description,
                s.Services.ToList(),
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
                    v.Options?.Select(o => new SelectOptionDetails(o.Value, o.Label, o.Description)).ToList()
                )).ToList()
            )).ToList(),
            LastSyncedAt: product.LastSyncedAt
        );

        return new GetProductResult(productDetails);
    }
}

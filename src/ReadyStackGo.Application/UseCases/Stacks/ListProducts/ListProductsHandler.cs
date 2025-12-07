using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;

namespace ReadyStackGo.Application.UseCases.Stacks.ListProducts;

public class ListProductsHandler : IRequestHandler<ListProductsQuery, ListProductsResult>
{
    private readonly IStackSourceService _stackSourceService;

    public ListProductsHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<ListProductsResult> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _stackSourceService.GetProductsAsync(cancellationToken);
        var sources = await _stackSourceService.GetSourcesAsync(cancellationToken);
        var sourceNames = sources.ToDictionary(s => s.Id.Value, s => s.Name);

        var productItems = products.Select(p => new ProductItem(
            Id: p.Id,
            SourceId: p.SourceId,
            SourceName: sourceNames.GetValueOrDefault(p.SourceId, p.SourceId),
            Name: p.DisplayName,
            Description: p.Description,
            Version: p.ProductVersion,
            IsMultiStack: p.IsMultiStack,
            TotalServices: p.TotalServices,
            TotalVariables: p.TotalVariables,
            Stacks: p.Stacks.Select(s => new ProductStackItem(
                s.Id,
                s.Name,
                s.Description,
                s.Services.ToList(),
                s.Variables.Select(v => new StackVariableItem(
                    v.Name,
                    v.DefaultValue,
                    v.IsRequired,
                    v.Type,
                    v.Label,
                    v.Description,
                    v.Placeholder,
                    v.Group,
                    v.Order,
                    v.Pattern,
                    v.PatternError,
                    v.Min,
                    v.Max,
                    v.Options?.Select(o => new SelectOptionItem(o.Value, o.Label, o.Description)).ToList()
                )).ToList()
            )).ToList(),
            LastSyncedAt: p.LastSyncedAt
        )).ToList();

        return new ListProductsResult(productItems);
    }
}

using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Stacks.ListStacks;

public class ListStacksHandler : IRequestHandler<ListStacksQuery, ListStacksResult>
{
    private readonly IStackSourceService _stackSourceService;

    public ListStacksHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<ListStacksResult> Handle(ListStacksQuery request, CancellationToken cancellationToken)
    {
        var stacks = await _stackSourceService.GetStacksAsync(cancellationToken);
        var sources = await _stackSourceService.GetSourcesAsync(cancellationToken);
        var sourceNames = sources.ToDictionary(s => s.Id.Value, s => s.Name);

        var items = stacks.Select(s => new StackListItem(
            s.Id,
            s.SourceId,
            sourceNames.GetValueOrDefault(s.SourceId, s.SourceId),
            s.Name,
            s.Description,
            s.RelativePath,
            s.GetServiceNames().ToList(),
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
            )).ToList(),
            s.LastSyncedAt,
            s.Version
        ));

        return new ListStacksResult(items);
    }
}

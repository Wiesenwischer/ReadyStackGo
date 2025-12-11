using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Stacks.ListStacks;
using ReadyStackGo.Domain.Catalog.Stacks;

namespace ReadyStackGo.Application.UseCases.Stacks.GetStack;

public class GetStackHandler : IRequestHandler<GetStackQuery, GetStackResult?>
{
    private readonly IStackSourceService _stackSourceService;

    public GetStackHandler(IStackSourceService stackSourceService)
    {
        _stackSourceService = stackSourceService;
    }

    public async Task<GetStackResult?> Handle(GetStackQuery request, CancellationToken cancellationToken)
    {
        var stack = await _stackSourceService.GetStackAsync(request.StackId, cancellationToken);

        if (stack == null)
            return null;

        var sources = await _stackSourceService.GetSourcesAsync(cancellationToken);
        var sourceName = sources.FirstOrDefault(s => s.Id.Value == stack.SourceId)?.Name ?? stack.SourceId;

        // Construct product ID from sourceId and productName (for back navigation in UI)
        var productId = $"{stack.SourceId}:{stack.ProductName}";

        return new GetStackResult(
            stack.Id,
            stack.SourceId,
            sourceName,
            stack.Name,
            stack.Description,
            stack.Services.Select(MapService).ToList(),
            stack.Variables.Select(v => new StackVariableItem(
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
            stack.Volumes.Select(v => new VolumeItem(v.Name, v.Driver, v.External)).ToList(),
            stack.Networks.Select(n => new NetworkItem(n.Name, n.Driver, n.External)).ToList(),
            stack.FilePath,
            stack.LastSyncedAt,
            stack.Version,
            productId
        );
    }

    private static ServiceItem MapService(ServiceTemplate service)
    {
        return new ServiceItem(
            service.Name,
            service.Image,
            service.ContainerName,
            service.Ports.Select(p => p.ToString()).ToList(),
            service.Volumes.Select(v => v.ToString()).ToList(),
            new Dictionary<string, string>(service.Environment),
            service.Networks.ToList(),
            service.DependsOn.ToList(),
            service.RestartPolicy,
            service.Command,
            service.HealthCheck != null ? MapHealthCheck(service.HealthCheck) : null
        );
    }

    private static ServiceHealthCheckItem MapHealthCheck(ServiceHealthCheck healthCheck)
    {
        return new ServiceHealthCheckItem(
            healthCheck.Test.ToList(),
            healthCheck.Interval?.ToString(),
            healthCheck.Timeout?.ToString(),
            healthCheck.Retries,
            healthCheck.StartPeriod?.ToString()
        );
    }
}

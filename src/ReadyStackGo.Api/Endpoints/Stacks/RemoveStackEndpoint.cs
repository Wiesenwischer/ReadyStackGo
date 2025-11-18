using FastEndpoints;
using ReadyStackGo.Application.Stacks;

namespace ReadyStackGo.API.Endpoints.Stacks;

public class RemoveStackRequest
{
    public string Id { get; set; } = null!;
}

public class RemoveStackEndpoint : Endpoint<RemoveStackRequest, EmptyResponse>
{
    public IStackService StackService { get; set; } = null!;

    public override void Configure()
    {
        Delete("/api/stacks/{Id}");
        Roles("admin");
    }

    public override async Task HandleAsync(RemoveStackRequest req, CancellationToken ct)
    {
        await StackService.RemoveStackAsync(req.Id, ct);
        // No content response
    }
}

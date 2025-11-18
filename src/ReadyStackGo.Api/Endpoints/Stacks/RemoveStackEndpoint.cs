using FastEndpoints;
using ReadyStackGo.Application.Stacks;

namespace ReadyStackGo.API.Endpoints.Stacks;

public class RemoveStackRequest
{
    public string Id { get; set; } = null!;
}

public class RemoveStackEndpoint : EndpointWithoutRequest
{
    public IStackService StackService { get; set; } = null!;

    public override void Configure()
    {
        Delete("/api/stacks/{id}");
        Roles("admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<string>("id")!;
        await StackService.RemoveStackAsync(id, ct);
    }
}

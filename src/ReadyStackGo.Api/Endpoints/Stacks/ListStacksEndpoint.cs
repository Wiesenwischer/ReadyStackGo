using FastEndpoints;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Application.Stacks.DTOs;

namespace ReadyStackGo.API.Endpoints.Stacks;

public class ListStacksEndpoint : Endpoint<EmptyRequest, IEnumerable<StackDto>>
{
    public IStackService StackService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stacks");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var stacks = await StackService.ListStacksAsync(ct);
        Response = stacks;
    }
}

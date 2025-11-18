using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Application.Stacks.DTOs;

namespace ReadyStackGo.API.Endpoints.Stacks;

public class DeployStackRequest
{
    public string Id { get; set; } = null!;
}

public class DeployStackEndpoint : Endpoint<DeployStackRequest, StackDto>
{
    public IStackService StackService { get; set; } = null!;

    public override void Configure()
    {
        Post("/api/stacks/{Id}/deploy");
        Roles("admin");
    }

    public override async Task HandleAsync(DeployStackRequest req, CancellationToken ct)
    {
        var stack = await StackService.DeployStackAsync(req.Id, ct);

        if (stack is null)
        {
            ThrowError("Stack not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = stack;
    }
}

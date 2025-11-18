using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Application.Stacks.DTOs;

namespace ReadyStackGo.API.Endpoints.Stacks;

public class GetStackRequest
{
    public string Id { get; set; } = null!;
}

public class GetStackEndpoint : Endpoint<GetStackRequest, StackDto>
{
    public IStackService StackService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stacks/{Id}");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(GetStackRequest req, CancellationToken ct)
    {
        var stack = await StackService.GetStackAsync(req.Id, ct);

        if (stack is null)
        {
            ThrowError("Stack not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = stack;
    }
}

using FastEndpoints;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Application.Stacks.DTOs;

namespace ReadyStackGo.API.Endpoints.Stacks;

public class GetStackEndpoint : EndpointWithoutRequest<StackDto>
{
    public IStackService StackService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/stacks/{Id}");
        Roles("admin", "operator");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Manually bind from route since this is a GET request
        var id = Route<string>("Id");
        if (string.IsNullOrWhiteSpace(id))
        {
            ThrowError("Stack ID is required");
            return;
        }

        var stack = await StackService.GetStackAsync(id, ct);

        if (stack is null)
        {
            ThrowError("Stack not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = stack;
    }
}

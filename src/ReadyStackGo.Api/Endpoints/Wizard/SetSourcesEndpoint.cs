using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Wizard;
using ReadyStackGo.Application.UseCases.Wizard.SetSources;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/sources - Step 4: Add stack sources from registry (optional)
/// </summary>
public class SetSourcesEndpoint : Endpoint<SetSourcesRequest, SetSourcesResponse>
{
    private readonly IMediator _mediator;

    public SetSourcesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/wizard/sources");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SetSourcesRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new SetSourcesCommand(req.RegistrySourceIds),
            ct);

        if (!result.Success)
        {
            ThrowError(result.Message ?? "Failed to add sources");
            return;
        }

        Response = new SetSourcesResponse
        {
            Success = true,
            Message = result.Message,
            SourcesCreated = result.SourcesCreated
        };
    }
}

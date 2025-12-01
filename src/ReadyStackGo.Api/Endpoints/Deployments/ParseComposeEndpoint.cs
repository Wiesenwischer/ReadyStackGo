using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.ParseCompose;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class ParseComposeEndpoint : Endpoint<ParseComposeRequest, ParseComposeResponse>
{
    private readonly IMediator _mediator;

    public ParseComposeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/deployments/parse");
    }

    public override async Task HandleAsync(ParseComposeRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new ParseComposeCommand(req.YamlContent), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to parse compose file");
        }

        Response = response;
    }
}

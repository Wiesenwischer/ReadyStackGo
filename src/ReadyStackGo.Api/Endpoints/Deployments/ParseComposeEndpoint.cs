using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.ParseCompose;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// POST /api/deployments/parse - Parse a Docker Compose file.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Create")]
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
        PreProcessor<RbacPreProcessor<ParseComposeRequest>>();
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

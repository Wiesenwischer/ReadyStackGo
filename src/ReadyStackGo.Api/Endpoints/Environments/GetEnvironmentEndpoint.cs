using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Environments;
using ReadyStackGo.Application.UseCases.Environments.GetEnvironment;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// GET /api/environments/{id} - Get a specific environment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Environments", "Read")]
public class GetEnvironmentEndpoint : Endpoint<GetEnvironmentRequest, EnvironmentResponse>
{
    private readonly IMediator _mediator;

    public GetEnvironmentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{id}");
        PreProcessor<RbacPreProcessor<GetEnvironmentRequest>>();
    }

    public override async Task HandleAsync(GetEnvironmentRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetEnvironmentQuery(req.Id), ct);

        if (response == null)
        {
            ThrowError("Environment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetEnvironmentRequest
{
    /// <summary>
    /// The environment ID (from route).
    /// </summary>
    public string Id { get; set; } = string.Empty;
}

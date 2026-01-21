using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

/// <summary>
/// POST /api/registries - Create a new registry.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Registries", "Create")]
public class CreateRegistryEndpoint : Endpoint<CreateRegistryRequest, RegistryResponse>
{
    private readonly IMediator _mediator;

    public CreateRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/registries");
        PreProcessor<RbacPreProcessor<CreateRegistryRequest>>();
    }

    public override async Task HandleAsync(CreateRegistryRequest req, CancellationToken ct)
    {
        // Organization is resolved in the handler
        var response = await _mediator.Send(new CreateRegistryCommand(req), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to create registry");
        }

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        Response = response;
    }
}

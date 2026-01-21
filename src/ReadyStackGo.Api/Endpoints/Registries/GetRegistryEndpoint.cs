using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

public class GetRegistryRequest
{
    public string RegistryId { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/registries/{registryId} - Get a specific registry.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator.
/// </summary>
[RequirePermission("Registries", "Read")]
public class GetRegistryEndpoint : Endpoint<GetRegistryRequest, RegistryResponse>
{
    private readonly IMediator _mediator;

    public GetRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/registries/{registryId}");
        PreProcessor<RbacPreProcessor<GetRegistryRequest>>();
    }

    public override async Task HandleAsync(GetRegistryRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetRegistryQuery(req.RegistryId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Registry not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

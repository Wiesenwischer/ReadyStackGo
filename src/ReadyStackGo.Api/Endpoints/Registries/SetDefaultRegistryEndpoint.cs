using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

/// <summary>
/// POST /api/registries/{id}/default - Set a registry as the default.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Registries", "Update")]
public class SetDefaultRegistryEndpoint : Endpoint<EmptyRequest, RegistryResponse>
{
    private readonly IMediator _mediator;

    public SetDefaultRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/registries/{id}/default");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var registryId = Route<string>("id")!;
        var response = await _mediator.Send(new SetDefaultRegistryCommand(registryId), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Failed to set default registry", statusCode);
        }

        Response = response;
    }
}

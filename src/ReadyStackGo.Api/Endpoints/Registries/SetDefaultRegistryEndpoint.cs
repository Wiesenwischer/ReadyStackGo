using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

public class SetDefaultRegistryRequest
{
    public string RegistryId { get; set; } = string.Empty;
}

/// <summary>
/// POST /api/registries/{registryId}/default - Set a registry as the default.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Registries", "Update")]
public class SetDefaultRegistryEndpoint : Endpoint<SetDefaultRegistryRequest, RegistryResponse>
{
    private readonly IMediator _mediator;

    public SetDefaultRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/registries/{registryId}/default");
        PreProcessor<RbacPreProcessor<SetDefaultRegistryRequest>>();
    }

    public override async Task HandleAsync(SetDefaultRegistryRequest req, CancellationToken ct)
    {
        // Organization is resolved in the handler
        var response = await _mediator.Send(new SetDefaultRegistryCommand(req.RegistryId), ct);

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

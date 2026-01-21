using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

public class DeleteRegistryRequest
{
    public string RegistryId { get; set; } = string.Empty;
}

/// <summary>
/// DELETE /api/registries/{registryId} - Delete a registry.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Registries", "Delete")]
public class DeleteRegistryEndpoint : Endpoint<DeleteRegistryRequest, RegistryResponse>
{
    private readonly IMediator _mediator;

    public DeleteRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/registries/{registryId}");
        PreProcessor<RbacPreProcessor<DeleteRegistryRequest>>();
    }

    public override async Task HandleAsync(DeleteRegistryRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteRegistryCommand(req.RegistryId), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Failed to delete registry", statusCode);
        }

        Response = response;
    }
}

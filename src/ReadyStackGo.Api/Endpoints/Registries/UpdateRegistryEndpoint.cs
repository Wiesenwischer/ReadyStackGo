using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Registries;

namespace ReadyStackGo.Api.Endpoints.Registries;

public class UpdateRegistryEndpointRequest
{
    public string RegistryId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool? ClearCredentials { get; set; }
    public List<string>? ImagePatterns { get; set; }
}

/// <summary>
/// PUT /api/registries/{registryId} - Update an existing registry.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Registries", "Update")]
public class UpdateRegistryEndpoint : Endpoint<UpdateRegistryEndpointRequest, RegistryResponse>
{
    private readonly IMediator _mediator;

    public UpdateRegistryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/registries/{registryId}");
        PreProcessor<RbacPreProcessor<UpdateRegistryEndpointRequest>>();
    }

    public override async Task HandleAsync(UpdateRegistryEndpointRequest req, CancellationToken ct)
    {
        var updateRequest = new UpdateRegistryRequest(
            req.Name,
            req.Url,
            req.Username,
            req.Password,
            req.ClearCredentials,
            req.ImagePatterns);

        var response = await _mediator.Send(new UpdateRegistryCommand(req.RegistryId, updateRequest), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Failed to update registry", statusCode);
        }

        Response = response;
    }
}

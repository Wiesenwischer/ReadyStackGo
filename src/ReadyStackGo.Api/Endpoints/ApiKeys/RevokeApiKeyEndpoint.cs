using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.ApiKeys;
using ReadyStackGo.Application.UseCases.ApiKeys.RevokeApiKey;

namespace ReadyStackGo.Api.Endpoints.ApiKeys;

public class RevokeApiKeyEndpointRequest
{
    public string ApiKeyId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

[RequirePermission("ApiKeys", "Delete")]
public class RevokeApiKeyEndpoint : Endpoint<RevokeApiKeyEndpointRequest, RevokeApiKeyResponse>
{
    private readonly IMediator _mediator;

    public RevokeApiKeyEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/api-keys/{apiKeyId}");
        PreProcessor<RbacPreProcessor<RevokeApiKeyEndpointRequest>>();
    }

    public override async Task HandleAsync(RevokeApiKeyEndpointRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new RevokeApiKeyCommand(req.ApiKeyId, req.Reason), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Failed to revoke API key", statusCode);
        }

        Response = response;
    }
}

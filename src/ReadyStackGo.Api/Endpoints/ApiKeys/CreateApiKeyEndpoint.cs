using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.ApiKeys;
using ReadyStackGo.Application.UseCases.ApiKeys.CreateApiKey;

namespace ReadyStackGo.Api.Endpoints.ApiKeys;

[RequirePermission("ApiKeys", "Create")]
public class CreateApiKeyEndpoint : Endpoint<CreateApiKeyRequest, CreateApiKeyResponse>
{
    private readonly IMediator _mediator;

    public CreateApiKeyEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/api-keys");
        PreProcessor<RbacPreProcessor<CreateApiKeyRequest>>();
    }

    public override async Task HandleAsync(CreateApiKeyRequest req, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateApiKeyCommand(req), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to create API key");
        }

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        Response = response;
    }
}

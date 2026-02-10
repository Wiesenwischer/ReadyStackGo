using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.ApiKeys;
using ReadyStackGo.Application.UseCases.ApiKeys.ListApiKeys;

namespace ReadyStackGo.Api.Endpoints.ApiKeys;

[RequirePermission("ApiKeys", "Read")]
public class ListApiKeysEndpoint : Endpoint<EmptyRequest, ListApiKeysResponse>
{
    private readonly IMediator _mediator;

    public ListApiKeysEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/api-keys");
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        Response = await _mediator.Send(new ListApiKeysQuery(), ct);
    }
}

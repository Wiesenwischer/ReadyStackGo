using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.System.GetHealthNotificationSettings;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// GET /api/system/health-notification-settings
/// Get current health notification settings (cooldown).
/// </summary>
[RequirePermission("System", "Read")]
public class GetHealthNotificationSettingsEndpoint : EndpointWithoutRequest<GetHealthNotificationSettingsResponse>
{
    private readonly IMediator _mediator;

    public GetHealthNotificationSettingsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/system/health-notification-settings");
        Description(b => b.WithTags("System"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetHealthNotificationSettingsQuery();
        Response = await _mediator.Send(query, ct);
    }
}

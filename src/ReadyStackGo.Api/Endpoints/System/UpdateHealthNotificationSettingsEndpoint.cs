using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.System.UpdateHealthNotificationSettings;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// Request DTO for health notification settings update.
/// </summary>
public class UpdateHealthNotificationSettingsRequest
{
    public int CooldownSeconds { get; set; }
}

/// <summary>
/// PUT /api/system/health-notification-settings
/// Update health notification settings (cooldown).
/// </summary>
[RequirePermission("System", "Write")]
public class UpdateHealthNotificationSettingsEndpoint
    : Endpoint<UpdateHealthNotificationSettingsRequest, UpdateHealthNotificationSettingsResponse>
{
    private readonly IMediator _mediator;

    public UpdateHealthNotificationSettingsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/system/health-notification-settings");
        Description(b => b.WithTags("System"));
        PreProcessor<RbacPreProcessor<UpdateHealthNotificationSettingsRequest>>();
    }

    public override async Task HandleAsync(UpdateHealthNotificationSettingsRequest req, CancellationToken ct)
    {
        var command = new UpdateHealthNotificationSettingsCommand
        {
            CooldownSeconds = req.CooldownSeconds
        };

        Response = await _mediator.Send(command, ct);

        if (!Response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}

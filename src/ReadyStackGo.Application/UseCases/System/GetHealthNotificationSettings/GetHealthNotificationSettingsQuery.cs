using MediatR;

namespace ReadyStackGo.Application.UseCases.System.GetHealthNotificationSettings;

public record GetHealthNotificationSettingsQuery() : IRequest<GetHealthNotificationSettingsResponse>;

using MediatR;

namespace ReadyStackGo.Application.UseCases.System.TriggerSelfUpdate;

/// <summary>
/// Command to trigger a self-update of the RSGO container to the specified version.
/// </summary>
public record TriggerSelfUpdateCommand(string TargetVersion) : IRequest<TriggerSelfUpdateResponse>;

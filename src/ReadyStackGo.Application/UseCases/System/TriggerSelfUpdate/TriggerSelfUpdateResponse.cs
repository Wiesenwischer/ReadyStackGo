namespace ReadyStackGo.Application.UseCases.System.TriggerSelfUpdate;

/// <summary>
/// Response from a self-update trigger operation.
/// </summary>
public record TriggerSelfUpdateResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
}

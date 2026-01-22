namespace ReadyStackGo.Application.UseCases.System.UpdateTlsConfig;

public record UpdateTlsConfigResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public bool RequiresRestart { get; init; }
}

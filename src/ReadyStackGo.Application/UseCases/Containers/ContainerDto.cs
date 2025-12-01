namespace ReadyStackGo.Application.UseCases.Containers;

public record ContainerDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Image { get; init; }
    public required string State { get; init; }
    public required string Status { get; init; }
    public DateTime Created { get; init; }
    public List<PortDto> Ports { get; init; } = [];
    public Dictionary<string, string> Labels { get; init; } = new();

    /// <summary>
    /// Health check status: "healthy", "unhealthy", "starting", or "none"
    /// </summary>
    public string HealthStatus { get; init; } = "none";

    /// <summary>
    /// Number of failing health checks (if any)
    /// </summary>
    public int FailingStreak { get; init; } = 0;
}

public record PortDto
{
    public int PrivatePort { get; init; }
    public int PublicPort { get; init; }
    public string Type { get; init; } = "tcp";
}

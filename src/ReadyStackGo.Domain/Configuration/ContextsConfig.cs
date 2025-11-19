namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// Service contexts configuration stored in rsgo.contexts.json
/// </summary>
public class ContextsConfig
{
    public ConnectionMode Mode { get; set; } = ConnectionMode.Simple;
    public GlobalConnections? GlobalConnections { get; set; }
    public Dictionary<string, ContextConnections> Contexts { get; set; } = new();
}

public enum ConnectionMode
{
    Simple,
    Advanced
}

public class GlobalConnections
{
    public required string Transport { get; set; }
    public required string Persistence { get; set; }
    public string? EventStore { get; set; }
}

public class ContextConnections
{
    public string? Transport { get; set; }
    public string? Persistence { get; set; }
    public string? EventStore { get; set; }
}

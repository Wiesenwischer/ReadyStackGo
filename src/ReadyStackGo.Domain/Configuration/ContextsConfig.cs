namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// Service contexts configuration stored in rsgo.contexts.json.
///
/// DEPRECATED in v0.4: Global connection strings are replaced by stack-specific
/// configuration. Connection values are now provided per deployment in
/// /app/config/deployments/{environmentId}/{stackName}.deployment.json
///
/// This file is kept for backwards compatibility and will be removed in v0.5.
/// </summary>
[Obsolete("Use stack-specific deployment configuration instead. Will be removed in v0.5.")]
public class ContextsConfig
{
    public ConnectionMode Mode { get; set; } = ConnectionMode.Simple;
    public GlobalConnections? GlobalConnections { get; set; }
    public Dictionary<string, ContextConnections> Contexts { get; set; } = new();
}

[Obsolete("Use stack-specific deployment configuration instead. Will be removed in v0.5.")]
public enum ConnectionMode
{
    Simple,
    Advanced
}

[Obsolete("Use stack-specific deployment configuration instead. Will be removed in v0.5.")]
public class GlobalConnections
{
    public required string Transport { get; set; }
    public required string Persistence { get; set; }
    public string? EventStore { get; set; }
}

[Obsolete("Use stack-specific deployment configuration instead. Will be removed in v0.5.")]
public class ContextConnections
{
    public string? Transport { get; set; }
    public string? Persistence { get; set; }
    public string? EventStore { get; set; }
}

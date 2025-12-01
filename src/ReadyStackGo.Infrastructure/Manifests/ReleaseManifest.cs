namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// Release manifest as defined in the specification
/// Defines the complete deployment configuration for a stack release
/// </summary>
public class ReleaseManifest
{
    public required string ManifestVersion { get; set; } = "1.0.0";
    public required string StackVersion { get; set; }
    public int SchemaVersion { get; set; }
    public GatewayConfig? Gateway { get; set; }
    public Dictionary<string, ContextDefinition> Contexts { get; set; } = new();
    public Dictionary<string, FeatureDefault> Features { get; set; } = new();
    public ManifestMetadata? Metadata { get; set; }
}

public class GatewayConfig
{
    public required string Context { get; set; }
    public string Protocol { get; set; } = "https";
    public int PublicPort { get; set; } = 8443;
    public int InternalHttpPort { get; set; } = 8080;
}

public class ContextDefinition
{
    public required string Image { get; set; }
    public required string Version { get; set; }
    public required string ContainerName { get; set; }
    public bool Internal { get; set; } = true;
    public Dictionary<string, string>? Env { get; set; }
    public List<string>? Ports { get; set; }
    public Dictionary<string, string>? Volumes { get; set; }
    public List<string>? DependsOn { get; set; }
}

public class FeatureDefault
{
    public bool Default { get; set; }
    public string? Description { get; set; }
}

public class ManifestMetadata
{
    public string? ReleaseName { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Description { get; set; }
    public List<string>? ChangeNotes { get; set; }
}

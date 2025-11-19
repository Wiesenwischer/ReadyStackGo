namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// Feature flags configuration stored in rsgo.features.json
/// </summary>
public class FeaturesConfig
{
    public Dictionary<string, bool> Features { get; set; } = new();
}

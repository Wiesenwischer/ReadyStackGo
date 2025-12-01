namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Interface for managing configuration files in the rsgo-config volume
/// </summary>
public interface IConfigStore
{
    Task<SystemConfig> GetSystemConfigAsync();
    Task SaveSystemConfigAsync(SystemConfig config);

    Task<TlsConfig> GetTlsConfigAsync();
    Task SaveTlsConfigAsync(TlsConfig config);

    Task<FeaturesConfig> GetFeaturesConfigAsync();
    Task SaveFeaturesConfigAsync(FeaturesConfig config);

    Task<ReleaseConfig> GetReleaseConfigAsync();
    Task SaveReleaseConfigAsync(ReleaseConfig config);
}

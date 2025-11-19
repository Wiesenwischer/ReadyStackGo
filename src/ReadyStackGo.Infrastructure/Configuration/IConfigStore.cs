using ReadyStackGo.Domain.Configuration;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Interface for managing configuration files in the rsgo-config volume
/// </summary>
public interface IConfigStore
{
    Task<SystemConfig> GetSystemConfigAsync();
    Task SaveSystemConfigAsync(SystemConfig config);

    Task<SecurityConfig> GetSecurityConfigAsync();
    Task SaveSecurityConfigAsync(SecurityConfig config);

    Task<TlsConfig> GetTlsConfigAsync();
    Task SaveTlsConfigAsync(TlsConfig config);

    Task<ContextsConfig> GetContextsConfigAsync();
    Task SaveContextsConfigAsync(ContextsConfig config);

    Task<FeaturesConfig> GetFeaturesConfigAsync();
    Task SaveFeaturesConfigAsync(FeaturesConfig config);

    Task<ReleaseConfig> GetReleaseConfigAsync();
    Task SaveReleaseConfigAsync(ReleaseConfig config);
}

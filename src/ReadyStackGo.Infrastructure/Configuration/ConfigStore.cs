using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Organizations;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// File-based configuration store that manages all rsgo.*.json files
/// in the /app/config volume.
///
/// v0.4: Updated to support polymorphic Environment serialization with $type discriminator.
/// </summary>
public class ConfigStore : IConfigStore
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigStore(IConfiguration configuration)
    {
        _configPath = configuration.GetValue<string>("ConfigPath") ?? "/app/config";

        // Ensure config directory exists
        if (!Directory.Exists(_configPath))
        {
            Directory.CreateDirectory(_configPath);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Required for polymorphic Environment serialization
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<SystemConfig> GetSystemConfigAsync()
    {
        return await LoadConfigAsync<SystemConfig>("rsgo.system.json")
            ?? new SystemConfig();
    }

    public async Task SaveSystemConfigAsync(SystemConfig config)
    {
        await SaveConfigAsync("rsgo.system.json", config);
    }

    public async Task<SecurityConfig> GetSecurityConfigAsync()
    {
        return await LoadConfigAsync<SecurityConfig>("rsgo.security.json")
            ?? new SecurityConfig();
    }

    public async Task SaveSecurityConfigAsync(SecurityConfig config)
    {
        await SaveConfigAsync("rsgo.security.json", config);
    }

    public async Task<TlsConfig> GetTlsConfigAsync()
    {
        return await LoadConfigAsync<TlsConfig>("rsgo.tls.json")
            ?? new TlsConfig();
    }

    public async Task SaveTlsConfigAsync(TlsConfig config)
    {
        await SaveConfigAsync("rsgo.tls.json", config);
    }

    public async Task<ContextsConfig> GetContextsConfigAsync()
    {
        return await LoadConfigAsync<ContextsConfig>("rsgo.contexts.json")
            ?? new ContextsConfig();
    }

    public async Task SaveContextsConfigAsync(ContextsConfig config)
    {
        await SaveConfigAsync("rsgo.contexts.json", config);
    }

    public async Task<FeaturesConfig> GetFeaturesConfigAsync()
    {
        return await LoadConfigAsync<FeaturesConfig>("rsgo.features.json")
            ?? new FeaturesConfig();
    }

    public async Task SaveFeaturesConfigAsync(FeaturesConfig config)
    {
        await SaveConfigAsync("rsgo.features.json", config);
    }

    public async Task<ReleaseConfig> GetReleaseConfigAsync()
    {
        return await LoadConfigAsync<ReleaseConfig>("rsgo.release.json")
            ?? new ReleaseConfig();
    }

    public async Task SaveReleaseConfigAsync(ReleaseConfig config)
    {
        await SaveConfigAsync("rsgo.release.json", config);
    }

    private async Task<T?> LoadConfigAsync<T>(string fileName) where T : class
    {
        var filePath = Path.Combine(_configPath, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration file {fileName}", ex);
        }
    }

    private async Task SaveConfigAsync<T>(string fileName, T config) where T : class
    {
        var filePath = Path.Combine(_configPath, fileName);

        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration file {fileName}", ex);
        }
    }
}

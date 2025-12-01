using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// File-based configuration store that manages static rsgo.*.json files
/// in the /app/config volume.
///
/// v0.6: Reduced to static configuration only.
/// - Organization and environments: Moved to SQLite (IOrganizationRepository, IEnvironmentRepository)
/// - Users and roles: Moved to SQLite (IUserRepository, IRoleRepository)
/// - Deployments: Moved to SQLite (IDeploymentRepository)
/// - Security: Moved to SQLite (User authentication)
///
/// Remaining in JSON files:
/// - rsgo.system.json: BaseUrl, ports, network, wizard state
/// - rsgo.tls.json: TLS certificate configuration
/// - rsgo.contexts.json: Legacy connection strings (deprecated, to be removed in v0.7)
/// - rsgo.features.json: Feature flags
/// - rsgo.release.json: Release/version information
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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Enable enum serialization as strings (also reads integer values for backwards compatibility)
            Converters = { new JsonStringEnumConverter() },
            // Ensure polymorphic type handling is enabled for Environment class hierarchy
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
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

            // Handle empty files gracefully
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Configuration file '{fileName}' contains invalid JSON. Please check the file format.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load configuration file '{fileName}': {ex.Message}", ex);
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

using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Infrastructure.Docker;

/// <summary>
/// Converts Docker Compose files to RSGo manifest format.
/// v0.10: Docker Compose Import (Conversion on Import)
/// </summary>
public class DockerComposeToRsgoConverter
{
    private readonly ILogger<DockerComposeToRsgoConverter> _logger;
    private readonly IDockerComposeParser _composeParser;

    public DockerComposeToRsgoConverter(
        ILogger<DockerComposeToRsgoConverter> logger,
        IDockerComposeParser composeParser)
    {
        _logger = logger;
        _composeParser = composeParser;
    }

    /// <summary>
    /// Convert a Docker Compose file to RSGo manifest format.
    /// </summary>
    /// <param name="composeYaml">Docker Compose YAML content</param>
    /// <param name="stackName">Optional stack name for metadata</param>
    /// <returns>Converted RSGo manifest</returns>
    public async Task<RsgoManifest> ConvertAsync(string composeYaml, string? stackName = null)
    {
        _logger.LogInformation("Converting Docker Compose to RSGo manifest format");

        // Parse the Docker Compose file
        var compose = await _composeParser.ParseAsync(composeYaml);
        var variables = await _composeParser.DetectVariablesAsync(composeYaml);

        var manifest = new RsgoManifest
        {
            Version = "1.0",
            Metadata = new RsgoProductMetadata
            {
                Name = stackName,
                Description = $"Converted from Docker Compose",
                ProductVersion = "1.0.0"
            },
            Variables = new Dictionary<string, RsgoVariable>(),
            Services = new Dictionary<string, RsgoService>()
        };

        // Convert variables with inferred types
        foreach (var variable in variables)
        {
            var rsgoVar = InferVariableType(variable);
            manifest.Variables![variable.Name] = rsgoVar;
        }

        // Convert services
        foreach (var (serviceName, service) in compose.Services)
        {
            var rsgoService = new RsgoService
            {
                Image = service.Image ?? throw new InvalidOperationException($"Service '{serviceName}' has no image"),
                ContainerName = service.ContainerName,
                Restart = service.Restart,
                Command = service.Command,
                Entrypoint = service.Entrypoint,
                WorkingDir = service.WorkingDir,
                User = service.User
            };

            if (service.Environment != null)
                rsgoService.Environment = new Dictionary<string, string>(service.Environment);

            if (service.Ports != null)
                rsgoService.Ports = new List<string>(service.Ports);

            if (service.Volumes != null)
                rsgoService.Volumes = new List<string>(service.Volumes);

            if (service.Networks != null)
                rsgoService.Networks = new List<string>(service.Networks);

            if (service.DependsOn != null)
                rsgoService.DependsOn = new List<string>(service.DependsOn);

            if (service.Labels != null)
                rsgoService.Labels = new Dictionary<string, string>(service.Labels);

            if (service.HealthCheck != null)
            {
                rsgoService.HealthCheck = new RsgoHealthCheck
                {
                    Test = service.HealthCheck.Test,
                    Interval = service.HealthCheck.Interval,
                    Timeout = service.HealthCheck.Timeout,
                    Retries = service.HealthCheck.Retries,
                    StartPeriod = service.HealthCheck.StartPeriod
                };
            }

            manifest.Services![serviceName] = rsgoService;
        }

        // Convert volumes
        if (compose.Volumes != null && compose.Volumes.Count > 0)
        {
            manifest.Volumes = new Dictionary<string, RsgoVolume>();
            foreach (var (volumeName, volume) in compose.Volumes)
            {
                manifest.Volumes[volumeName] = new RsgoVolume
                {
                    Driver = volume.Driver,
                    External = volume.External,
                    DriverOpts = volume.DriverOpts
                };
            }
        }

        // Convert networks
        if (compose.Networks != null && compose.Networks.Count > 0)
        {
            manifest.Networks = new Dictionary<string, RsgoNetwork>();
            foreach (var (networkName, network) in compose.Networks)
            {
                manifest.Networks[networkName] = new RsgoNetwork
                {
                    Driver = network.Driver,
                    External = network.External,
                    DriverOpts = network.DriverOpts
                };
            }
        }

        _logger.LogInformation("Converted Docker Compose to RSGo manifest: {ServiceCount} services, {VariableCount} variables",
            manifest.Services!.Count, manifest.Variables!.Count);

        return manifest;
    }

    /// <summary>
    /// Infer the variable type from its name and default value.
    /// </summary>
    private RsgoVariable InferVariableType(EnvironmentVariableDefinition variable)
    {
        var name = variable.Name.ToUpperInvariant();
        var defaultValue = variable.DefaultValue;

        // Infer type from common naming patterns
        var inferredType = VariableType.String;
        double? min = null;
        double? max = null;
        string? placeholder = null;
        string? description = null;
        string? pattern = null;
        string? patternError = null;

        // Port detection
        if (name.Contains("PORT") || name.EndsWith("_PORT"))
        {
            inferredType = VariableType.Port;
            min = 1;
            max = 65535;
            placeholder = "8080";
            description = "Port number (1-65535)";
        }
        // Password detection
        else if (name.Contains("PASSWORD") || name.Contains("SECRET") || name.Contains("TOKEN") || name.Contains("KEY"))
        {
            inferredType = VariableType.Password;
            description = "Sensitive value (will not be displayed)";
        }
        // Boolean detection
        else if (name.StartsWith("ENABLE_") || name.StartsWith("DISABLE_") ||
                 name.StartsWith("USE_") || name.StartsWith("IS_") ||
                 name.EndsWith("_ENABLED") || name.EndsWith("_DISABLED"))
        {
            inferredType = VariableType.Boolean;
            if (string.IsNullOrEmpty(defaultValue))
                defaultValue = "false";
        }
        // Number detection from default value
        else if (!string.IsNullOrEmpty(defaultValue) && double.TryParse(defaultValue, out _))
        {
            // Check if it looks like a port
            if (int.TryParse(defaultValue, out var numVal) && numVal >= 1 && numVal <= 65535 &&
                (name.Contains("PORT") || name.EndsWith("PORT")))
            {
                inferredType = VariableType.Port;
            }
            else
            {
                inferredType = VariableType.Number;
            }
        }
        // Version detection
        else if (name.Contains("VERSION"))
        {
            pattern = @"^[\d]+\.[\d]+\.?[\d]*$";
            patternError = "Must be a valid version number (e.g., 1.0 or 1.0.0)";
            placeholder = "1.0.0";
        }
        // Email detection
        else if (name.Contains("EMAIL"))
        {
            pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            patternError = "Must be a valid email address";
            placeholder = "user@example.com";
        }
        // URL detection
        else if (name.Contains("URL") || name.Contains("ENDPOINT") || name.Contains("HOST"))
        {
            if (name.Contains("URL"))
            {
                pattern = @"^https?://";
                patternError = "Must be a valid URL starting with http:// or https://";
                placeholder = "https://example.com";
            }
        }
        // Connection string detection
        else if (name.Contains("CONNECTIONSTRING") || name.Contains("CONNECTION_STRING") ||
                 name.EndsWith("_DB") || name.EndsWith("_DATABASE"))
        {
            // Try to infer the specific database type
            if (name.Contains("SQLSERVER") || name.Contains("MSSQL") ||
                (!string.IsNullOrEmpty(defaultValue) && defaultValue.Contains("Server=") && defaultValue.Contains("Database=")))
            {
                inferredType = VariableType.SqlServerConnectionString;
                description = "SQL Server connection string";
            }
            else if (name.Contains("POSTGRES") || name.Contains("PGSQL") ||
                     (!string.IsNullOrEmpty(defaultValue) && defaultValue.StartsWith("Host=")))
            {
                inferredType = VariableType.PostgresConnectionString;
                description = "PostgreSQL connection string";
            }
            else if (name.Contains("MYSQL") ||
                     (!string.IsNullOrEmpty(defaultValue) && defaultValue.StartsWith("server=")))
            {
                inferredType = VariableType.MySqlConnectionString;
                description = "MySQL connection string";
            }
            else if (name.Contains("EVENTSTORE") ||
                     (!string.IsNullOrEmpty(defaultValue) && defaultValue.StartsWith("esdb://")))
            {
                inferredType = VariableType.EventStoreConnectionString;
                description = "EventStoreDB connection string";
            }
            else if (name.Contains("MONGO") ||
                     (!string.IsNullOrEmpty(defaultValue) && defaultValue.StartsWith("mongodb://")))
            {
                inferredType = VariableType.MongoConnectionString;
                description = "MongoDB connection string";
            }
            else if (name.Contains("REDIS") ||
                     (!string.IsNullOrEmpty(defaultValue) && defaultValue.StartsWith("redis://")))
            {
                inferredType = VariableType.RedisConnectionString;
                description = "Redis connection string";
            }
            else
            {
                // Generic connection string
                inferredType = VariableType.ConnectionString;
                description = "Database connection string";
            }
        }

        return new RsgoVariable
        {
            Label = FormatLabel(variable.Name),
            Description = description,
            Type = inferredType,
            Default = defaultValue,
            Required = variable.IsRequired,
            Pattern = pattern,
            PatternError = patternError,
            Min = min,
            Max = max,
            Placeholder = placeholder
        };
    }

    /// <summary>
    /// Format a variable name as a human-readable label.
    /// </summary>
    private static string FormatLabel(string name)
    {
        // Convert SCREAMING_SNAKE_CASE to Title Case
        var words = name.Split('_')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower());

        return string.Join(" ", words);
    }
}

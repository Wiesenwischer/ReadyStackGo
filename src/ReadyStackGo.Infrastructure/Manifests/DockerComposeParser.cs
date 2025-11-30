using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Manifests;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// Parses Docker Compose YAML files and converts them to deployment plans.
/// Supports Docker Compose file format version 3.x
/// </summary>
public class DockerComposeParser : IDockerComposeParser
{
    private readonly ILogger<DockerComposeParser> _logger;
    private readonly IDeserializer _yamlDeserializer;

    // Regex to match environment variable references: ${VAR} or ${VAR:-default}
    private static readonly Regex EnvVarPattern = new(
        @"\$\{([^}:]+)(?::-([^}]*))?\}",
        RegexOptions.Compiled);

    public DockerComposeParser(ILogger<DockerComposeParser> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public Task<DockerComposeDefinition> ParseAsync(string yamlContent)
    {
        try
        {
            _logger.LogDebug("Parsing Docker Compose YAML content");

            var rawData = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            var definition = new DockerComposeDefinition
            {
                Version = rawData.ContainsKey("version")
                    ? rawData["version"]?.ToString() ?? "3"
                    : "3"
            };

            // Parse services
            if (rawData.TryGetValue("services", out var servicesObj) && servicesObj is Dictionary<object, object> services)
            {
                foreach (var kvp in services)
                {
                    var serviceName = kvp.Key.ToString()!;
                    var serviceData = kvp.Value as Dictionary<object, object>;

                    if (serviceData != null)
                    {
                        definition.Services[serviceName] = ParseService(serviceData);
                    }
                }
            }

            // Parse volumes
            if (rawData.TryGetValue("volumes", out var volumesObj) && volumesObj is Dictionary<object, object> volumes)
            {
                definition.Volumes = new Dictionary<string, ComposeVolumeDefinition>();
                foreach (var kvp in volumes)
                {
                    var volumeName = kvp.Key.ToString()!;
                    definition.Volumes[volumeName] = ParseVolume(kvp.Value);
                }
            }

            // Parse networks
            if (rawData.TryGetValue("networks", out var networksObj) && networksObj is Dictionary<object, object> networks)
            {
                definition.Networks = new Dictionary<string, ComposeNetworkDefinition>();
                foreach (var kvp in networks)
                {
                    var networkName = kvp.Key.ToString()!;
                    definition.Networks[networkName] = ParseNetwork(kvp.Value);
                }
            }

            _logger.LogInformation("Parsed Docker Compose file with {ServiceCount} services", definition.Services.Count);

            return Task.FromResult(definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Docker Compose YAML");
            throw new InvalidOperationException($"Failed to parse Docker Compose file: {ex.Message}", ex);
        }
    }

    public Task<List<EnvironmentVariableDefinition>> DetectVariablesAsync(string yamlContent)
    {
        var variables = new Dictionary<string, EnvironmentVariableDefinition>();

        try
        {
            // Parse the compose file to get service names
            var definition = ParseAsync(yamlContent).Result;

            // Find all variable references in the YAML content
            var matches = EnvVarPattern.Matches(yamlContent);

            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;

                if (!variables.ContainsKey(varName))
                {
                    variables[varName] = new EnvironmentVariableDefinition
                    {
                        Name = varName,
                        DefaultValue = defaultValue
                    };
                }
                else if (defaultValue != null && variables[varName].DefaultValue == null)
                {
                    // Update with default if found later
                    variables[varName].DefaultValue = defaultValue;
                }
            }

            // Now determine which services use each variable
            foreach (var service in definition.Services)
            {
                if (service.Value.Environment != null)
                {
                    foreach (var env in service.Value.Environment)
                    {
                        var envMatches = EnvVarPattern.Matches(env.Value ?? string.Empty);
                        foreach (Match match in envMatches)
                        {
                            var varName = match.Groups[1].Value;
                            if (variables.ContainsKey(varName) &&
                                !variables[varName].UsedInServices.Contains(service.Key))
                            {
                                variables[varName].UsedInServices.Add(service.Key);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("Detected {VarCount} environment variables in compose file", variables.Count);

            return Task.FromResult(variables.Values.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect environment variables");
            throw;
        }
    }

    public Task<DeploymentPlan> ConvertToDeploymentPlanAsync(
        DockerComposeDefinition compose,
        Dictionary<string, string> resolvedVariables,
        string stackName)
    {
        var plan = new DeploymentPlan
        {
            StackVersion = stackName,
            GlobalEnvVars = new Dictionary<string, string>(resolvedVariables)
        };

        // Process network definitions from compose file
        // Build a mapping from compose network name to resolved network name
        var networkMapping = new Dictionary<string, string>();

        if (compose.Networks != null && compose.Networks.Count > 0)
        {
            foreach (var (networkName, networkDef) in compose.Networks)
            {
                var isExternal = networkDef.External ?? false;
                // External networks use their name as-is, non-external get prefixed with stack name
                var resolvedName = isExternal ? networkName : $"{stackName}_{networkName}";

                plan.Networks[networkName] = new NetworkDefinition
                {
                    External = isExternal,
                    ResolvedName = resolvedName
                };
                networkMapping[networkName] = resolvedName;

                _logger.LogDebug("Network '{NetworkName}' -> '{ResolvedName}' (external: {External})",
                    networkName, resolvedName, isExternal);
            }
        }

        // If no networks defined, create a default stack network
        var defaultNetwork = $"{stackName}_default";
        if (networkMapping.Count == 0)
        {
            plan.Networks["default"] = new NetworkDefinition
            {
                External = false,
                ResolvedName = defaultNetwork
            };
            networkMapping["default"] = defaultNetwork;
        }

        int order = 0;

        // Build dependency graph and determine order
        var serviceOrder = DetermineDeploymentOrder(compose.Services);

        foreach (var serviceName in serviceOrder)
        {
            var service = compose.Services[serviceName];

            var step = new DeploymentStep
            {
                ContextName = serviceName,
                Image = ResolveVariables(service.Image ?? string.Empty, resolvedVariables),
                Version = "latest", // Docker Compose doesn't have explicit versions
                ContainerName = ResolveVariables(
                    service.ContainerName ?? $"{stackName}_{serviceName}",
                    resolvedVariables),
                Internal = service.Ports == null || service.Ports.Count == 0,
                Order = order++
            };

            // Resolve networks for this service
            if (service.Networks != null && service.Networks.Count > 0)
            {
                // Service has explicit network configuration
                foreach (var network in service.Networks)
                {
                    if (networkMapping.TryGetValue(network, out var resolvedNetwork))
                    {
                        step.Networks.Add(resolvedNetwork);
                    }
                    else
                    {
                        // Network referenced but not defined - treat as external
                        _logger.LogWarning("Service '{Service}' references undefined network '{Network}', treating as external",
                            serviceName, network);
                        step.Networks.Add(network);
                    }
                }
            }
            else
            {
                // No networks specified - use the first available network (like Docker Compose does)
                var firstNetwork = networkMapping.Values.FirstOrDefault() ?? defaultNetwork;
                step.Networks.Add(firstNetwork);
            }

            // Resolve environment variables
            if (service.Environment != null)
            {
                foreach (var env in service.Environment)
                {
                    step.EnvVars[env.Key] = ResolveVariables(env.Value ?? string.Empty, resolvedVariables);
                }
            }

            // Add ports
            if (service.Ports != null)
            {
                step.Ports = service.Ports
                    .Select(p => ResolveVariables(p, resolvedVariables))
                    .ToList();
            }

            // Add volumes - prefix named volumes with stack name for isolation
            if (service.Volumes != null)
            {
                foreach (var volume in service.Volumes)
                {
                    var resolved = ResolveVariables(volume, resolvedVariables);
                    var parts = resolved.Split(':');
                    if (parts.Length >= 2)
                    {
                        var volumeSource = parts[0];
                        var volumeTarget = parts[1];

                        // Check if this is a named volume (not a path)
                        // Named volumes don't start with / or . or contain path separators
                        if (!volumeSource.StartsWith("/") &&
                            !volumeSource.StartsWith(".") &&
                            !volumeSource.StartsWith("~") &&
                            !volumeSource.Contains("\\") &&
                            !volumeSource.Contains("/"))
                        {
                            // Prefix named volume with stack name for isolation
                            volumeSource = $"{stackName}_{volumeSource}";
                        }

                        step.Volumes[volumeSource] = volumeTarget;
                    }
                }
            }

            // Add dependencies
            if (service.DependsOn != null)
            {
                step.DependsOn = service.DependsOn.ToList();
            }

            plan.Steps.Add(step);
        }

        _logger.LogInformation("Converted compose file to deployment plan with {StepCount} steps and {NetworkCount} networks",
            plan.Steps.Count, plan.Networks.Count);

        return Task.FromResult(plan);
    }

    public Task<DockerComposeValidationResult> ValidateAsync(string yamlContent)
    {
        var result = new DockerComposeValidationResult { IsValid = true };

        try
        {
            // Try to parse the YAML
            DockerComposeDefinition definition;
            try
            {
                definition = ParseAsync(yamlContent).Result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid YAML syntax: {ex.Message}");
                return Task.FromResult(result);
            }

            // Validate services exist
            if (definition.Services.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No services defined in compose file");
                return Task.FromResult(result);
            }

            // Validate each service
            foreach (var kvp in definition.Services)
            {
                var serviceName = kvp.Key;
                var service = kvp.Value;

                // Must have image or build
                if (string.IsNullOrEmpty(service.Image) && string.IsNullOrEmpty(service.Build))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Service '{serviceName}' must have either 'image' or 'build' defined");
                }

                // Build is not supported for remote deployment
                if (!string.IsNullOrEmpty(service.Build))
                {
                    result.Warnings.Add($"Service '{serviceName}' uses 'build' which is not supported for remote deployment. Please use pre-built images.");
                }

                // Validate port format
                if (service.Ports != null)
                {
                    foreach (var port in service.Ports)
                    {
                        if (!IsValidPortMapping(port))
                        {
                            result.Warnings.Add($"Service '{serviceName}' has potentially invalid port mapping: {port}");
                        }
                    }
                }

                // Validate dependencies exist
                if (service.DependsOn != null)
                {
                    foreach (var dep in service.DependsOn)
                    {
                        if (!definition.Services.ContainsKey(dep))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Service '{serviceName}' depends on non-existent service '{dep}'");
                        }
                    }
                }
            }

            // Detect unresolved variables
            var variables = DetectVariablesAsync(yamlContent).Result;
            var requiredVars = variables.Where(v => v.IsRequired).ToList();
            if (requiredVars.Any())
            {
                result.Warnings.Add($"Compose file has {requiredVars.Count} required environment variables: {string.Join(", ", requiredVars.Select(v => v.Name))}");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private ComposeServiceDefinition ParseService(Dictionary<object, object> data)
    {
        var service = new ComposeServiceDefinition();

        if (data.TryGetValue("image", out var image))
            service.Image = image?.ToString();

        if (data.TryGetValue("build", out var build))
            service.Build = build?.ToString();

        if (data.TryGetValue("container_name", out var containerName))
            service.ContainerName = containerName?.ToString();

        if (data.TryGetValue("command", out var command))
            service.Command = command?.ToString();

        if (data.TryGetValue("entrypoint", out var entrypoint))
            service.Entrypoint = entrypoint?.ToString();

        if (data.TryGetValue("working_dir", out var workingDir))
            service.WorkingDir = workingDir?.ToString();

        if (data.TryGetValue("restart", out var restart))
            service.Restart = restart?.ToString();

        if (data.TryGetValue("user", out var user))
            service.User = user?.ToString();

        if (data.TryGetValue("privileged", out var privileged))
            service.Privileged = privileged is bool b && b;

        // Parse ports
        if (data.TryGetValue("ports", out var ports) && ports is IEnumerable<object> portList)
            service.Ports = portList.Select(p => p.ToString()!).ToList();

        // Parse expose
        if (data.TryGetValue("expose", out var expose) && expose is IEnumerable<object> exposeList)
            service.Expose = exposeList.Select(e => e.ToString()!).ToList();

        // Parse volumes
        if (data.TryGetValue("volumes", out var volumes) && volumes is IEnumerable<object> volumeList)
            service.Volumes = volumeList.Select(v => v.ToString()!).ToList();

        // Parse depends_on
        if (data.TryGetValue("depends_on", out var dependsOn) && dependsOn is IEnumerable<object> depList)
            service.DependsOn = depList.Select(d => d.ToString()!).ToList();

        // Parse networks
        if (data.TryGetValue("networks", out var networks) && networks is IEnumerable<object> netList)
            service.Networks = netList.Select(n => n.ToString()!).ToList();

        // Parse environment
        if (data.TryGetValue("environment", out var environment))
        {
            service.Environment = ParseEnvironment(environment);
        }

        // Parse env_file
        if (data.TryGetValue("env_file", out var envFile))
        {
            if (envFile is IEnumerable<object> envFileList)
                service.EnvFile = envFileList.Select(e => e.ToString()!).ToList();
            else if (envFile != null)
                service.EnvFile = new List<string> { envFile.ToString()! };
        }

        // Parse labels
        if (data.TryGetValue("labels", out var labels))
        {
            service.Labels = ParseKeyValuePairs(labels);
        }

        // Parse healthcheck
        if (data.TryGetValue("healthcheck", out var healthCheck) && healthCheck is Dictionary<object, object> hcData)
        {
            service.HealthCheck = ParseHealthCheck(hcData);
        }

        return service;
    }

    private Dictionary<string, string>? ParseEnvironment(object environment)
    {
        if (environment is Dictionary<object, object> envDict)
        {
            return envDict.ToDictionary(
                kvp => kvp.Key.ToString()!,
                kvp => kvp.Value?.ToString() ?? string.Empty);
        }
        else if (environment is IEnumerable<object> envList)
        {
            var result = new Dictionary<string, string>();
            foreach (var item in envList)
            {
                var str = item.ToString()!;
                var parts = str.Split('=', 2);
                result[parts[0]] = parts.Length > 1 ? parts[1] : string.Empty;
            }
            return result;
        }
        return null;
    }

    private Dictionary<string, string>? ParseKeyValuePairs(object data)
    {
        if (data is Dictionary<object, object> dict)
        {
            return dict.ToDictionary(
                kvp => kvp.Key.ToString()!,
                kvp => kvp.Value?.ToString() ?? string.Empty);
        }
        return null;
    }

    private ComposeHealthCheck ParseHealthCheck(Dictionary<object, object> data)
    {
        var hc = new ComposeHealthCheck();

        if (data.TryGetValue("test", out var test))
        {
            if (test is IEnumerable<object> testList)
                hc.Test = testList.Select(t => t.ToString()!).ToList();
            else if (test != null)
                hc.Test = new List<string> { test.ToString()! };
        }

        if (data.TryGetValue("interval", out var interval))
            hc.Interval = interval?.ToString();

        if (data.TryGetValue("timeout", out var timeout))
            hc.Timeout = timeout?.ToString();

        if (data.TryGetValue("retries", out var retries) && retries != null)
            hc.Retries = Convert.ToInt32(retries);

        if (data.TryGetValue("start_period", out var startPeriod))
            hc.StartPeriod = startPeriod?.ToString();

        return hc;
    }

    private ComposeVolumeDefinition ParseVolume(object? data)
    {
        var volume = new ComposeVolumeDefinition();

        if (data is Dictionary<object, object> dict)
        {
            if (dict.TryGetValue("driver", out var driver))
                volume.Driver = driver?.ToString();

            if (dict.TryGetValue("external", out var external))
                volume.External = external is bool b && b;

            if (dict.TryGetValue("driver_opts", out var driverOpts))
                volume.DriverOpts = ParseKeyValuePairs(driverOpts);
        }

        return volume;
    }

    private ComposeNetworkDefinition ParseNetwork(object? data)
    {
        var network = new ComposeNetworkDefinition();

        if (data is Dictionary<object, object> dict)
        {
            if (dict.TryGetValue("driver", out var driver))
                network.Driver = driver?.ToString();

            if (dict.TryGetValue("external", out var external))
                network.External = ParseBoolValue(external);

            if (dict.TryGetValue("driver_opts", out var driverOpts))
                network.DriverOpts = ParseKeyValuePairs(driverOpts);
        }

        return network;
    }

    private static bool ParseBoolValue(object? value)
    {
        if (value is bool b)
            return b;
        if (value is string s)
            return s.Equals("true", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private string ResolveVariables(string input, Dictionary<string, string> variables)
    {
        return EnvVarPattern.Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;

            if (variables.TryGetValue(varName, out var value))
                return value;

            return defaultValue;
        });
    }

    private List<string> DetermineDeploymentOrder(Dictionary<string, ComposeServiceDefinition> services)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string serviceName)
        {
            if (visited.Contains(serviceName))
                return;

            if (visiting.Contains(serviceName))
                throw new InvalidOperationException($"Circular dependency detected involving service '{serviceName}'");

            visiting.Add(serviceName);

            if (services.TryGetValue(serviceName, out var service) && service.DependsOn != null)
            {
                foreach (var dep in service.DependsOn)
                {
                    if (services.ContainsKey(dep))
                        Visit(dep);
                }
            }

            visiting.Remove(serviceName);
            visited.Add(serviceName);
            result.Add(serviceName);
        }

        foreach (var serviceName in services.Keys)
        {
            Visit(serviceName);
        }

        return result;
    }

    private bool IsValidPortMapping(string port)
    {
        // Simple validation for port mappings like "8080:80", "127.0.0.1:8080:80", "8080"
        var pattern = @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}:)?(\d+|\$\{[^}]+\})(:(\d+|\$\{[^}]+\}))?(/tcp|/udp)?$";
        return Regex.IsMatch(port, pattern) || port.Contains("${");
    }
}

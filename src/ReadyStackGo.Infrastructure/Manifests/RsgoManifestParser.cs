using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReadyStackGo.Infrastructure.Manifests;

/// <summary>
/// Parses RSGo manifest files (YAML format) - the native ReadyStackGo stack format.
/// v0.10: RSGo Manifest Format implementation.
/// </summary>
public class RsgoManifestParser : IRsgoManifestParser
{
    private readonly ILogger<RsgoManifestParser> _logger;
    private readonly IDeserializer _yamlDeserializer;

    // Regex to match environment variable references: ${VAR} or ${VAR:-default}
    private static readonly Regex EnvVarPattern = new(
        @"\$\{([^}:]+)(?::-([^}]*))?\}",
        RegexOptions.Compiled);

    public RsgoManifestParser(ILogger<RsgoManifestParser> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public Task<RsgoManifest> ParseAsync(string yamlContent)
    {
        try
        {
            _logger.LogDebug("Parsing RSGo manifest YAML content");

            var manifest = _yamlDeserializer.Deserialize<RsgoManifest>(yamlContent);

            if (manifest == null)
            {
                throw new InvalidOperationException("Failed to deserialize RSGo manifest");
            }

            _logger.LogInformation("Parsed RSGo manifest: {Name} with {ServiceCount} services and {VariableCount} variables",
                manifest.Metadata?.Name ?? "unnamed",
                manifest.Services.Count,
                manifest.Variables.Count);

            return Task.FromResult(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse RSGo manifest YAML");
            throw new InvalidOperationException($"Failed to parse RSGo manifest: {ex.Message}", ex);
        }
    }

    public Task<List<StackVariable>> ExtractVariablesAsync(RsgoManifest manifest)
    {
        var variables = new List<StackVariable>();

        foreach (var (name, def) in manifest.Variables)
        {
            var options = def.Options?.Select(o => new SelectOption(o.Value, o.Label, o.Description));

            var variable = new StackVariable(
                name: name,
                defaultValue: def.Default,
                description: def.Description,
                type: def.Type,
                label: def.Label,
                pattern: def.Pattern,
                patternError: def.PatternError,
                options: options,
                min: def.Min,
                max: def.Max,
                placeholder: def.Placeholder,
                group: def.Group,
                order: def.Order,
                isRequired: def.Required
            );

            variables.Add(variable);
        }

        // Sort by group and order
        return Task.FromResult(variables
            .OrderBy(v => v.Group ?? "")
            .ThenBy(v => v.Order)
            .ToList());
    }

    public async Task<DeploymentPlan> ConvertToDeploymentPlanAsync(
        RsgoManifest manifest,
        Dictionary<string, string> resolvedVariables,
        string stackName)
    {
        var plan = new DeploymentPlan
        {
            StackVersion = manifest.Metadata?.StackVersion ?? stackName,
            GlobalEnvVars = new Dictionary<string, string>(resolvedVariables)
        };

        // Process network definitions
        var networkMapping = new Dictionary<string, string>();

        if (manifest.Networks != null && manifest.Networks.Count > 0)
        {
            foreach (var (networkName, networkDef) in manifest.Networks)
            {
                var isExternal = networkDef.External ?? false;
                var resolvedName = isExternal ? networkName : $"{stackName}_{networkName}";

                plan.Networks[networkName] = new NetworkDefinition
                {
                    External = isExternal,
                    ResolvedName = resolvedName
                };
                networkMapping[networkName] = resolvedName;
            }
        }

        // Create default network if none defined
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

        // Determine deployment order based on dependencies
        var serviceOrder = DetermineDeploymentOrder(manifest.Services);
        int order = 0;

        foreach (var serviceName in serviceOrder)
        {
            var service = manifest.Services[serviceName];

            var step = new DeploymentStep
            {
                ContextName = serviceName,
                Image = ResolveVariables(service.Image, resolvedVariables),
                Version = ExtractImageVersion(service.Image),
                ContainerName = ResolveVariables(
                    service.ContainerName ?? $"{stackName}_{serviceName}",
                    resolvedVariables),
                Internal = service.Ports == null || service.Ports.Count == 0,
                Order = order++
            };

            // Resolve networks
            if (service.Networks != null && service.Networks.Count > 0)
            {
                foreach (var network in service.Networks)
                {
                    if (networkMapping.TryGetValue(network, out var resolvedNetwork))
                    {
                        step.Networks.Add(resolvedNetwork);
                    }
                    else
                    {
                        step.Networks.Add(network);
                    }
                }
            }
            else
            {
                step.Networks.Add(networkMapping.Values.FirstOrDefault() ?? defaultNetwork);
            }

            // Resolve environment variables
            if (service.Environment != null)
            {
                foreach (var env in service.Environment)
                {
                    step.EnvVars[env.Key] = ResolveVariables(env.Value, resolvedVariables);
                }
            }

            // Add ports
            if (service.Ports != null)
            {
                step.Ports = service.Ports
                    .Select(p => ResolveVariables(p, resolvedVariables))
                    .ToList();
            }

            // Add volumes
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

                        // Prefix named volumes with stack name
                        if (!volumeSource.StartsWith("/") &&
                            !volumeSource.StartsWith(".") &&
                            !volumeSource.StartsWith("~") &&
                            !volumeSource.Contains("\\") &&
                            !volumeSource.Contains("/"))
                        {
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

        _logger.LogInformation("Converted RSGo manifest to deployment plan with {StepCount} steps",
            plan.Steps.Count);

        return await Task.FromResult(plan);
    }

    public Task<RsgoManifestValidationResult> ValidateAsync(string yamlContent)
    {
        var result = new RsgoManifestValidationResult { IsValid = true };

        try
        {
            RsgoManifest manifest;
            try
            {
                manifest = ParseAsync(yamlContent).Result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid YAML syntax: {ex.Message}");
                return Task.FromResult(result);
            }

            // Validate version
            if (string.IsNullOrWhiteSpace(manifest.Version))
            {
                result.Warnings.Add("Manifest version not specified, assuming 1.0");
            }

            // Validate services exist
            if (manifest.Services.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No services defined in manifest");
                return Task.FromResult(result);
            }

            // Validate each service
            foreach (var (serviceName, service) in manifest.Services)
            {
                if (string.IsNullOrWhiteSpace(service.Image))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Service '{serviceName}': Image is required");
                }

                // Validate dependencies exist
                if (service.DependsOn != null)
                {
                    foreach (var dep in service.DependsOn)
                    {
                        if (!manifest.Services.ContainsKey(dep))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Service '{serviceName}' depends on non-existent service '{dep}'");
                        }
                    }
                }
            }

            // Validate variables
            foreach (var (varName, varDef) in manifest.Variables)
            {
                // Validate Select type has options
                if (varDef.Type == VariableType.Select &&
                    (varDef.Options == null || varDef.Options.Count == 0))
                {
                    result.Warnings.Add($"Variable '{varName}': Select type should have options defined");
                }

                // Validate regex pattern
                if (!string.IsNullOrEmpty(varDef.Pattern))
                {
                    try
                    {
                        _ = new Regex(varDef.Pattern);
                    }
                    catch (RegexParseException)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Variable '{varName}': Invalid regex pattern '{varDef.Pattern}'");
                    }
                }

                // Validate min/max for Number type
                if (varDef.Type == VariableType.Number)
                {
                    if (varDef.Min.HasValue && varDef.Max.HasValue && varDef.Min > varDef.Max)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Variable '{varName}': Min value cannot be greater than max value");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    public async Task<VariableValidationResult> ValidateVariablesAsync(
        RsgoManifest manifest,
        Dictionary<string, string> values)
    {
        var result = new VariableValidationResult { IsValid = true };
        var variables = await ExtractVariablesAsync(manifest);

        foreach (var variable in variables)
        {
            values.TryGetValue(variable.Name, out var value);

            var validation = variable.Validate(value);
            if (!validation.IsValid)
            {
                result.IsValid = false;
                result.VariableErrors[variable.Name] = validation.Errors.ToList();
            }

            if (variable.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                result.MissingRequired.Add(variable.Name);
            }
        }

        return result;
    }

    public ManifestFormat DetectFormat(string yamlContent)
    {
        try
        {
            var rawData = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            if (rawData == null)
                return ManifestFormat.Unknown;

            // RSGo format indicators
            if (rawData.ContainsKey("metadata") && rawData.ContainsKey("services"))
                return ManifestFormat.RsgoManifest;

            if (rawData.ContainsKey("variables") && rawData.ContainsKey("services"))
                return ManifestFormat.RsgoManifest;

            if (rawData.TryGetValue("version", out var version))
            {
                var versionStr = version?.ToString() ?? "";
                if (versionStr.StartsWith("rsgo", StringComparison.OrdinalIgnoreCase))
                    return ManifestFormat.RsgoManifest;

                // Docker Compose versions are typically "3", "3.8", "2.4", etc.
                if (Regex.IsMatch(versionStr, @"^[23]\.\d*$") || versionStr == "2" || versionStr == "3")
                {
                    if (rawData.ContainsKey("services"))
                        return ManifestFormat.DockerCompose;
                }
            }

            // Default Docker Compose detection (services at root)
            if (rawData.ContainsKey("services") && !rawData.ContainsKey("variables"))
                return ManifestFormat.DockerCompose;

            return ManifestFormat.Unknown;
        }
        catch
        {
            return ManifestFormat.Unknown;
        }
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

    private List<string> DetermineDeploymentOrder(Dictionary<string, RsgoService> services)
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

    private static string ExtractImageVersion(string image)
    {
        // Extract version from image tag (e.g., "nginx:1.21" -> "1.21")
        var colonIndex = image.LastIndexOf(':');
        if (colonIndex > 0 && !image.Substring(colonIndex).Contains('/'))
        {
            return image.Substring(colonIndex + 1);
        }
        return "latest";
    }
}

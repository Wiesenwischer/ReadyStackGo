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
/// v0.10: RSGo Manifest Format implementation with multi-stack and include support.
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

            var serviceCount = manifest.Services?.Count ?? 0;
            var variableCount = manifest.Variables?.Count ?? 0;
            var stackCount = manifest.Stacks?.Count ?? 0;

            if (manifest.IsMultiStack)
            {
                _logger.LogInformation("Parsed multi-stack RSGo manifest: {Name} v{Version} with {StackCount} stacks",
                    manifest.Metadata?.Name ?? "unnamed",
                    manifest.Metadata?.ProductVersion ?? "?",
                    stackCount);
            }
            else
            {
                _logger.LogInformation("Parsed RSGo manifest: {Name} with {ServiceCount} services and {VariableCount} variables",
                    manifest.Metadata?.Name ?? "unnamed",
                    serviceCount,
                    variableCount);
            }

            return Task.FromResult(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse RSGo manifest YAML");
            throw new InvalidOperationException($"Failed to parse RSGo manifest: {ex.Message}", ex);
        }
    }

    public async Task<RsgoManifest> ParseFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var yamlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var manifest = await ParseAsync(yamlContent);

        // Resolve includes if this is a multi-stack manifest
        if (manifest.IsMultiStack && manifest.Stacks != null)
        {
            var baseDir = Path.GetDirectoryName(filePath) ?? ".";
            await ResolveIncludesAsync(manifest, baseDir, cancellationToken);
        }

        return manifest;
    }

    public async Task<List<RsgoManifest>> LoadProductsFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        var products = new List<RsgoManifest>();

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory not found: {Path}", directoryPath);
            return products;
        }

        // Find all YAML files recursively
        var yamlFiles = Directory.GetFiles(directoryPath, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directoryPath, "*.yml", SearchOption.AllDirectories))
            .Distinct()
            .ToList();

        foreach (var file in yamlFiles)
        {
            try
            {
                var manifest = await ParseFromFileAsync(file, cancellationToken);

                // Only load products (manifests with productVersion)
                if (manifest.IsProduct)
                {
                    _logger.LogDebug("Loaded product from {File}: {Name} v{Version}",
                        file,
                        manifest.Metadata?.Name,
                        manifest.Metadata?.ProductVersion);
                    products.Add(manifest);
                }
                else
                {
                    _logger.LogDebug("Skipping fragment (no productVersion): {File}", file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse manifest from {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} products from {Path}", products.Count, directoryPath);
        return products;
    }

    private async Task ResolveIncludesAsync(RsgoManifest manifest, string baseDir, CancellationToken cancellationToken)
    {
        if (manifest.Stacks == null) return;

        foreach (var (stackKey, stackEntry) in manifest.Stacks)
        {
            if (stackEntry.IsInclude)
            {
                var includePath = Path.Combine(baseDir, stackEntry.Include!);

                if (!File.Exists(includePath))
                {
                    _logger.LogWarning("Include file not found: {Path}", includePath);
                    continue;
                }

                try
                {
                    var includeContent = await File.ReadAllTextAsync(includePath, cancellationToken);
                    var fragment = await ParseAsync(includeContent);

                    // Copy fragment content to stack entry
                    stackEntry.Metadata = fragment.Metadata != null
                        ? new RsgoStackMetadata
                        {
                            Name = fragment.Metadata.Name,
                            Description = fragment.Metadata.Description,
                            Category = fragment.Metadata.Category,
                            Tags = fragment.Metadata.Tags
                        }
                        : null;
                    stackEntry.Variables = fragment.Variables;
                    stackEntry.Services = fragment.Services;
                    stackEntry.Volumes = fragment.Volumes;
                    stackEntry.Networks = fragment.Networks;

                    _logger.LogDebug("Resolved include for stack '{StackKey}' from {Path}", stackKey, includePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve include for stack '{StackKey}' from {Path}", stackKey, includePath);
                }
            }
        }
    }

    public Task<List<StackVariable>> ExtractVariablesAsync(RsgoManifest manifest)
    {
        var variables = new Dictionary<string, StackVariable>();

        // First add shared variables (for multi-stack manifests)
        if (manifest.SharedVariables != null)
        {
            foreach (var (name, def) in manifest.SharedVariables)
            {
                variables[name] = ConvertToStackVariable(name, def);
            }
        }

        // Then add direct variables (for single-stack manifests)
        if (manifest.Variables != null)
        {
            foreach (var (name, def) in manifest.Variables)
            {
                variables[name] = ConvertToStackVariable(name, def);
            }
        }

        // For multi-stack manifests, collect all stack variables
        if (manifest.Stacks != null)
        {
            foreach (var (_, stackEntry) in manifest.Stacks)
            {
                if (stackEntry.Variables != null)
                {
                    foreach (var (name, def) in stackEntry.Variables)
                    {
                        // Stack-specific variables override shared variables
                        variables[name] = ConvertToStackVariable(name, def);
                    }
                }
            }
        }

        // Sort by group and order
        return Task.FromResult(variables.Values
            .OrderBy(v => v.Group ?? "")
            .ThenBy(v => v.Order)
            .ToList());
    }

    public Task<List<StackVariable>> ExtractStackVariablesAsync(RsgoManifest manifest, string stackKey)
    {
        var variables = new Dictionary<string, StackVariable>();

        // First add shared variables
        if (manifest.SharedVariables != null)
        {
            foreach (var (name, def) in manifest.SharedVariables)
            {
                variables[name] = ConvertToStackVariable(name, def);
            }
        }

        // Then add stack-specific variables (override shared)
        if (manifest.Stacks != null && manifest.Stacks.TryGetValue(stackKey, out var stackEntry))
        {
            if (stackEntry.Variables != null)
            {
                foreach (var (name, def) in stackEntry.Variables)
                {
                    variables[name] = ConvertToStackVariable(name, def);
                }
            }
        }

        return Task.FromResult(variables.Values
            .OrderBy(v => v.Group ?? "")
            .ThenBy(v => v.Order)
            .ToList());
    }

    private static StackVariable ConvertToStackVariable(string name, RsgoVariable def)
    {
        var options = def.Options?.Select(o => new SelectOption(o.Value, o.Label, o.Description));

        return new StackVariable(
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
    }

    public async Task<DeploymentPlan> ConvertToDeploymentPlanAsync(
        RsgoManifest manifest,
        Dictionary<string, string> resolvedVariables,
        string stackName)
    {
        // For single-stack manifests, use direct services
        if (manifest.IsSingleStack && manifest.Services != null)
        {
            return await BuildDeploymentPlanAsync(
                manifest.Services,
                manifest.Networks,
                manifest.Volumes,
                manifest.Metadata?.ProductVersion ?? stackName,
                resolvedVariables,
                stackName);
        }

        // For multi-stack manifests without specific stack, throw error
        if (manifest.IsMultiStack)
        {
            throw new InvalidOperationException(
                "Multi-stack manifest requires stack key. Use ConvertStackToDeploymentPlanAsync instead.");
        }

        throw new InvalidOperationException("Invalid manifest: no services defined");
    }

    public async Task<DeploymentPlan> ConvertStackToDeploymentPlanAsync(
        RsgoManifest manifest,
        string stackKey,
        Dictionary<string, string> resolvedVariables,
        string stackName)
    {
        if (!manifest.IsMultiStack || manifest.Stacks == null)
        {
            throw new InvalidOperationException("Manifest is not a multi-stack manifest");
        }

        if (!manifest.Stacks.TryGetValue(stackKey, out var stackEntry))
        {
            throw new InvalidOperationException($"Stack '{stackKey}' not found in manifest");
        }

        if (stackEntry.Services == null || stackEntry.Services.Count == 0)
        {
            throw new InvalidOperationException($"Stack '{stackKey}' has no services defined");
        }

        return await BuildDeploymentPlanAsync(
            stackEntry.Services,
            stackEntry.Networks,
            stackEntry.Volumes,
            manifest.Metadata?.ProductVersion ?? stackName,
            resolvedVariables,
            stackName);
    }

    private async Task<DeploymentPlan> BuildDeploymentPlanAsync(
        Dictionary<string, RsgoService> services,
        Dictionary<string, RsgoNetwork>? networks,
        Dictionary<string, RsgoVolume>? volumes,
        string version,
        Dictionary<string, string> resolvedVariables,
        string stackName)
    {
        var plan = new DeploymentPlan
        {
            StackVersion = version,
            GlobalEnvVars = new Dictionary<string, string>(resolvedVariables)
        };

        // Process network definitions
        var networkMapping = new Dictionary<string, string>();

        if (networks != null && networks.Count > 0)
        {
            foreach (var (networkName, networkDef) in networks)
            {
                var isExternal = networkDef.External ?? false;
                var resolvedName = isExternal ? networkName : DockerNamingUtility.CreateNetworkName(stackName, networkName);

                plan.Networks[networkName] = new NetworkDefinition
                {
                    External = isExternal,
                    ResolvedName = resolvedName
                };
                networkMapping[networkName] = resolvedName;
            }
        }

        // Create default network if none defined
        var defaultNetwork = DockerNamingUtility.CreateNetworkName(stackName, "default");
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
        var serviceOrder = DetermineDeploymentOrder(services);
        int order = 0;

        foreach (var serviceName in serviceOrder)
        {
            var service = services[serviceName];

            var step = new DeploymentStep
            {
                ContextName = serviceName,
                Image = ResolveVariables(service.Image, resolvedVariables),
                Version = ExtractImageVersion(service.Image),
                ContainerName = ResolveVariables(
                    service.ContainerName ?? DockerNamingUtility.CreateContainerName(stackName, serviceName),
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

                        // Prefix named volumes with sanitized stack name
                        if (!volumeSource.StartsWith("/") &&
                            !volumeSource.StartsWith(".") &&
                            !volumeSource.StartsWith("~") &&
                            !volumeSource.Contains("\\") &&
                            !volumeSource.Contains("/"))
                        {
                            volumeSource = DockerNamingUtility.CreateVolumeName(stackName, volumeSource);
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

            // Note: 'version' field is optional and currently ignored.
            // Format is auto-detected based on structure (metadata, services, stacks).

            // Validate that manifest is a product (has productVersion) or is a valid fragment
            if (!manifest.IsProduct)
            {
                result.Warnings.Add("Manifest has no productVersion - it will only be loadable via include");
            }

            // Validate single-stack or multi-stack structure
            if (manifest.IsSingleStack)
            {
                // Single-stack: validate direct services
                ValidateServices(manifest.Services!, result);
            }
            else if (manifest.IsMultiStack)
            {
                // Multi-stack: validate each stack entry
                foreach (var (stackKey, stackEntry) in manifest.Stacks!)
                {
                    if (stackEntry.IsInclude)
                    {
                        // Include reference - can't validate content here
                        if (string.IsNullOrWhiteSpace(stackEntry.Include))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Stack '{stackKey}': Include path is empty");
                        }
                    }
                    else
                    {
                        // Inline stack - validate services
                        if (stackEntry.Services == null || stackEntry.Services.Count == 0)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Stack '{stackKey}': No services defined");
                        }
                        else
                        {
                            ValidateServices(stackEntry.Services, result, $"Stack '{stackKey}' > ");
                        }

                        // Validate stack variables
                        if (stackEntry.Variables != null)
                        {
                            ValidateVariables(stackEntry.Variables, result, $"Stack '{stackKey}' > ");
                        }
                    }
                }
            }
            else
            {
                result.IsValid = false;
                result.Errors.Add("Manifest must have either 'services' (single-stack) or 'stacks' (multi-stack) section");
            }

            // Validate shared variables
            if (manifest.SharedVariables != null)
            {
                ValidateVariables(manifest.SharedVariables, result, "SharedVariable ");
            }

            // Validate direct variables (single-stack)
            if (manifest.Variables != null)
            {
                ValidateVariables(manifest.Variables, result);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private void ValidateServices(Dictionary<string, RsgoService> services, RsgoManifestValidationResult result, string prefix = "")
    {
        foreach (var (serviceName, service) in services)
        {
            if (string.IsNullOrWhiteSpace(service.Image))
            {
                result.IsValid = false;
                result.Errors.Add($"{prefix}Service '{serviceName}': Image is required");
            }

            // Validate dependencies exist
            if (service.DependsOn != null)
            {
                foreach (var dep in service.DependsOn)
                {
                    if (!services.ContainsKey(dep))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"{prefix}Service '{serviceName}' depends on non-existent service '{dep}'");
                    }
                }
            }
        }
    }

    private void ValidateVariables(Dictionary<string, RsgoVariable> variables, RsgoManifestValidationResult result, string prefix = "")
    {
        foreach (var (varName, varDef) in variables)
        {
            // Validate Select type has options
            if (varDef.Type == VariableType.Select &&
                (varDef.Options == null || varDef.Options.Count == 0))
            {
                result.Warnings.Add($"{prefix}Variable '{varName}': Select type should have options defined");
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
                    result.Errors.Add($"{prefix}Variable '{varName}': Invalid regex pattern '{varDef.Pattern}'");
                }
            }

            // Validate min/max for Number type
            if (varDef.Type == VariableType.Number)
            {
                if (varDef.Min.HasValue && varDef.Max.HasValue && varDef.Min > varDef.Max)
                {
                    result.IsValid = false;
                    result.Errors.Add($"{prefix}Variable '{varName}': Min value cannot be greater than max value");
                }
            }
        }
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

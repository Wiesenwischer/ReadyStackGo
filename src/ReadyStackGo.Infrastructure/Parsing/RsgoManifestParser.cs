using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Docker;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using DeploymentNetworkDefinition = ReadyStackGo.Application.UseCases.Deployments.NetworkDefinition;

namespace ReadyStackGo.Infrastructure.Parsing;

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

            // Pre-process to extract services.include before deserialization
            yamlContent = ExtractServiceIncludes(yamlContent, out var serviceIncludes);

            var manifest = _yamlDeserializer.Deserialize<RsgoManifest>(yamlContent);

            if (manifest == null)
            {
                throw new InvalidOperationException("Failed to deserialize RSGo manifest");
            }

            // Set extracted service includes
            if (serviceIncludes != null && serviceIncludes.Count > 0)
            {
                manifest.ServiceIncludes = serviceIncludes;
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

        var baseDir = Path.GetDirectoryName(filePath) ?? ".";

        // Resolve stack includes if this is a multi-stack manifest
        if (manifest.IsMultiStack && manifest.Stacks != null)
        {
            await ResolveIncludesAsync(manifest, baseDir, cancellationToken);
        }

        // Resolve service includes if specified
        if (manifest.ServiceIncludes != null && manifest.ServiceIncludes.Count > 0)
        {
            await ResolveServiceIncludesAsync(manifest, baseDir, cancellationToken);
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
                    // Use ParseFromFileAsync to resolve nested service includes
                    var fragment = await ParseFromFileAsync(includePath, cancellationToken);

                    // Check if the included file is a multi-stack product (not a simple fragment)
                    if (fragment.IsMultiStack && fragment.Stacks != null && fragment.Stacks.Count > 0)
                    {
                        // Multi-stack products used as includes: flatten all services from all sub-stacks
                        _logger.LogWarning("Include {Path} is a multi-stack product - flattening {Count} sub-stacks into single stack '{StackKey}'",
                            includePath, fragment.Stacks.Count, stackKey);

                        var allServices = new Dictionary<string, RsgoService>();
                        var allVolumes = new Dictionary<string, RsgoVolume>();
                        var allNetworks = new Dictionary<string, RsgoNetwork>();

                        // Collect services from all sub-stacks
                        foreach (var (_, subStack) in fragment.Stacks)
                        {
                            if (subStack.Services != null)
                            {
                                foreach (var (serviceName, service) in subStack.Services)
                                {
                                    allServices[serviceName] = service;
                                }
                            }

                            if (subStack.Volumes != null)
                            {
                                foreach (var (volumeName, volume) in subStack.Volumes)
                                {
                                    allVolumes[volumeName] = volume;
                                }
                            }

                            if (subStack.Networks != null)
                            {
                                foreach (var (networkName, network) in subStack.Networks)
                                {
                                    allNetworks[networkName] = network;
                                }
                            }
                        }

                        stackEntry.Metadata = fragment.Metadata != null
                            ? new RsgoStackMetadata
                            {
                                Name = fragment.Metadata.Name,
                                Description = fragment.Metadata.Description,
                                Category = fragment.Metadata.Category,
                                Tags = fragment.Metadata.Tags
                            }
                            : null;
                        stackEntry.Variables = fragment.SharedVariables; // Use sharedVariables from multi-stack product
                        stackEntry.Services = allServices.Count > 0 ? allServices : null;
                        stackEntry.Volumes = allVolumes.Count > 0 ? allVolumes : null;
                        stackEntry.Networks = allNetworks.Count > 0 ? allNetworks : null;

                        _logger.LogInformation("Flattened multi-stack include '{StackKey}': collected {ServiceCount} services from {StackCount} sub-stacks",
                            stackKey, allServices.Count, fragment.Stacks.Count);
                    }
                    else
                    {
                        // Normal single-stack fragment - copy as-is
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
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve include for stack '{StackKey}' from {Path}", stackKey, includePath);
                }
            }
        }
    }

    /// <summary>
    /// Resolves service includes by loading services from referenced files and merging them into the manifest.
    /// </summary>
    /// <summary>
    /// Extracts services.include from YAML content before deserialization.
    /// YamlDotNet can't handle mixed dictionary entries (service definitions + include list),
    /// so we extract the include list manually and remove it from the YAML.
    /// </summary>
    private string ExtractServiceIncludes(string yamlContent, out List<string>? serviceIncludes)
    {
        serviceIncludes = null;

        using var stringReader = new StringReader(yamlContent);
        var yaml = new YamlStream();

        try
        {
            yaml.Load(stringReader);
        }
        catch
        {
            // If parsing fails, return original content
            return yamlContent;
        }

        if (yaml.Documents.Count == 0)
            return yamlContent;

        var root = yaml.Documents[0].RootNode as YamlDotNet.RepresentationModel.YamlMappingNode;
        if (root == null)
            return yamlContent;

        // Look for services node
        var servicesKey = new YamlDotNet.RepresentationModel.YamlScalarNode("services");
        if (!root.Children.ContainsKey(servicesKey))
            return yamlContent;

        var servicesNode = root.Children[servicesKey] as YamlDotNet.RepresentationModel.YamlMappingNode;
        if (servicesNode == null)
            return yamlContent;

        // Look for include key within services
        var includeKey = new YamlDotNet.RepresentationModel.YamlScalarNode("include");
        if (!servicesNode.Children.ContainsKey(includeKey))
            return yamlContent;

        var includeNode = servicesNode.Children[includeKey] as YamlDotNet.RepresentationModel.YamlSequenceNode;
        if (includeNode != null)
        {
            // Extract the include list
            serviceIncludes = includeNode.Children
                .OfType<YamlDotNet.RepresentationModel.YamlScalarNode>()
                .Select(n => n.Value ?? "")
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            _logger.LogDebug("Extracted {Count} service includes from YAML", serviceIncludes.Count);
        }

        // Remove include from services node using regex (simpler than rebuilding YAML tree)
        // Match "  include:\n    - file1\n    - file2\n" pattern under services
        var includePattern = new Regex(
            @"^(\s+)include:\s*\r?\n(\s+-\s+.+\r?\n?)+",
            RegexOptions.Multiline);

        yamlContent = includePattern.Replace(yamlContent, "");

        return yamlContent;
    }

    private async Task ResolveServiceIncludesAsync(RsgoManifest manifest, string baseDir, CancellationToken cancellationToken)
    {
        if (manifest.ServiceIncludes == null || manifest.ServiceIncludes.Count == 0)
            return;

        // Initialize Services dictionary if it doesn't exist
        manifest.Services ??= new Dictionary<string, RsgoService>();

        foreach (var includeFile in manifest.ServiceIncludes)
        {
            var includePath = Path.Combine(baseDir, includeFile);

            if (!File.Exists(includePath))
            {
                _logger.LogWarning("Service include file not found: {Path}", includePath);
                continue;
            }

            try
            {
                var includeContent = await File.ReadAllTextAsync(includePath, cancellationToken);
                var fragment = await ParseAsync(includeContent);

                // Merge services from fragment into main manifest
                if (fragment.Services != null)
                {
                    foreach (var (serviceName, service) in fragment.Services)
                    {
                        if (manifest.Services.ContainsKey(serviceName))
                        {
                            _logger.LogWarning("Service '{ServiceName}' from {Path} conflicts with existing service - skipping",
                                serviceName, includeFile);
                            continue;
                        }

                        manifest.Services[serviceName] = service;
                    }

                    _logger.LogDebug("Merged {Count} services from {Path}",
                        fragment.Services.Count, includeFile);
                }

                // Also merge volumes and networks if needed
                if (fragment.Volumes != null)
                {
                    manifest.Volumes ??= new Dictionary<string, RsgoVolume>();
                    foreach (var (volumeName, volume) in fragment.Volumes)
                    {
                        manifest.Volumes.TryAdd(volumeName, volume);
                    }
                }

                if (fragment.Networks != null)
                {
                    manifest.Networks ??= new Dictionary<string, RsgoNetwork>();
                    foreach (var (networkName, network) in fragment.Networks)
                    {
                        manifest.Networks.TryAdd(networkName, network);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve service include from {Path}", includeFile);
            }
        }

        _logger.LogInformation("Resolved {IncludeCount} service includes, total services: {ServiceCount}",
            manifest.ServiceIncludes.Count, manifest.Services.Count);
    }

    public Task<List<Variable>> ExtractVariablesAsync(RsgoManifest manifest)
    {
        var variables = new Dictionary<string, Variable>();

        // First add shared variables (for multi-stack manifests)
        if (manifest.SharedVariables != null)
        {
            foreach (var (name, def) in manifest.SharedVariables)
            {
                variables[name] = ConvertToVariable(name, def);
            }
        }

        // Then add direct variables (for single-stack manifests)
        if (manifest.Variables != null)
        {
            foreach (var (name, def) in manifest.Variables)
            {
                variables[name] = ConvertToVariable(name, def);
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
                        variables[name] = ConvertToVariable(name, def);
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

    public Task<List<Variable>> ExtractStackVariablesAsync(RsgoManifest manifest, string stackKey)
    {
        var variables = new Dictionary<string, Variable>();

        // First add shared variables
        if (manifest.SharedVariables != null)
        {
            foreach (var (name, def) in manifest.SharedVariables)
            {
                variables[name] = ConvertToVariable(name, def);
            }
        }

        // Then add stack-specific variables (override shared)
        if (manifest.Stacks != null && manifest.Stacks.TryGetValue(stackKey, out var stackEntry))
        {
            if (stackEntry.Variables != null)
            {
                foreach (var (name, def) in stackEntry.Variables)
                {
                    variables[name] = ConvertToVariable(name, def);
                }
            }
        }

        return Task.FromResult(variables.Values
            .OrderBy(v => v.Group ?? "")
            .ThenBy(v => v.Order)
            .ToList());
    }

    private static Variable ConvertToVariable(string name, RsgoVariable def)
    {
        var options = def.Options?.Select(o => new SelectOption(o.Value, o.Label, o.Description));

        return new Variable(
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
                manifest.Metadata?.ProductVersion ?? "unspecified",
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
            manifest.Metadata?.ProductVersion ?? "unspecified",
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

                plan.Networks[networkName] = new DeploymentNetworkDefinition
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
            plan.Networks["default"] = new DeploymentNetworkDefinition
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
                Order = order++,
                Lifecycle = service.Lifecycle
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

namespace ReadyStackGo.Domain.StackManagement.Stacks;

using System.Text.RegularExpressions;

/// <summary>
/// Domain service for resolving variables in stack definitions.
/// Handles variable substitution, defaults, and validation.
/// Works with structured ServiceTemplate data (not raw YAML).
/// </summary>
public class StackVariableResolver
{
    private static readonly Regex VariablePattern = new(
        @"\$\{([A-Za-z_][A-Za-z0-9_]*)(:-([^}]*))?\}|\$([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Resolves all variables in the stack definition with provided values.
    /// Returns new ServiceTemplates with resolved environment variables.
    /// </summary>
    /// <param name="stackDefinition">The stack definition containing service templates</param>
    /// <param name="providedValues">Variables provided by the user</param>
    /// <returns>Resolution result with resolved service templates and any errors</returns>
    public VariableResolutionResult Resolve(
        StackDefinition stackDefinition,
        IDictionary<string, string> providedValues)
    {
        ArgumentNullException.ThrowIfNull(stackDefinition);
        ArgumentNullException.ThrowIfNull(providedValues);

        var errors = new List<VariableResolutionError>();
        var resolvedVariables = new Dictionary<string, string>();

        // Build the effective values by merging defaults with provided values
        var effectiveValues = BuildEffectiveValues(stackDefinition, providedValues);

        // Validate all required variables are provided
        foreach (var variable in stackDefinition.GetRequiredVariables())
        {
            if (!effectiveValues.TryGetValue(variable.Name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new VariableResolutionError(
                    variable.Name,
                    VariableResolutionErrorType.RequiredVariableMissing,
                    $"Required variable '{variable.Label ?? variable.Name}' is not provided."));
            }
        }

        // Validate all provided values against their constraints
        foreach (var variable in stackDefinition.Variables)
        {
            if (effectiveValues.TryGetValue(variable.Name, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                var validationResult = variable.Validate(value);
                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        errors.Add(new VariableResolutionError(
                            variable.Name,
                            VariableResolutionErrorType.ValidationFailed,
                            error));
                    }
                }
                resolvedVariables[variable.Name] = value;
            }
        }

        // If there are errors, return early
        if (errors.Count > 0)
        {
            return new VariableResolutionResult(
                false,
                stackDefinition.Services,
                resolvedVariables,
                errors);
        }

        // Resolve variables in service templates
        var resolvedServices = ResolveServicesVariables(stackDefinition.Services, effectiveValues, errors);

        return new VariableResolutionResult(
            errors.Count == 0,
            resolvedServices,
            resolvedVariables,
            errors);
    }

    /// <summary>
    /// Extracts all variable references from a string value.
    /// </summary>
    public IEnumerable<ExtractedVariable> ExtractVariablesFromString(string content)
    {
        if (string.IsNullOrEmpty(content))
            yield break;

        var matches = VariablePattern.Matches(content);
        var seen = new HashSet<string>();

        foreach (Match match in matches)
        {
            // ${VAR} or ${VAR:-default} format
            var varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[4].Value;
            var defaultValue = match.Groups[3].Success ? match.Groups[3].Value : null;

            if (!seen.Contains(varName))
            {
                seen.Add(varName);
                yield return new ExtractedVariable(varName, defaultValue);
            }
        }
    }

    /// <summary>
    /// Extracts all variable references from a stack definition's services.
    /// </summary>
    public IEnumerable<ExtractedVariable> ExtractVariables(StackDefinition stackDefinition)
    {
        var seen = new HashSet<string>();
        var result = new List<ExtractedVariable>();

        foreach (var service in stackDefinition.Services)
        {
            // Extract from environment values
            foreach (var envValue in service.Environment.Values)
            {
                foreach (var variable in ExtractVariablesFromString(envValue))
                {
                    if (seen.Add(variable.Name))
                    {
                        result.Add(variable);
                    }
                }
            }

            // Extract from image
            foreach (var variable in ExtractVariablesFromString(service.Image))
            {
                if (seen.Add(variable.Name))
                {
                    result.Add(variable);
                }
            }

            // Extract from command
            if (service.Command != null)
            {
                foreach (var variable in ExtractVariablesFromString(service.Command))
                {
                    if (seen.Add(variable.Name))
                    {
                        result.Add(variable);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a preview of how variables will be resolved.
    /// </summary>
    public VariablePreview Preview(
        StackDefinition stackDefinition,
        IDictionary<string, string> providedValues)
    {
        ArgumentNullException.ThrowIfNull(stackDefinition);
        ArgumentNullException.ThrowIfNull(providedValues);

        var effectiveValues = BuildEffectiveValues(stackDefinition, providedValues);
        var preview = new List<VariablePreviewItem>();

        foreach (var variable in stackDefinition.Variables)
        {
            var hasProvidedValue = providedValues.TryGetValue(variable.Name, out var providedValue) &&
                                   !string.IsNullOrWhiteSpace(providedValue);

            var effectiveValue = effectiveValues.TryGetValue(variable.Name, out var ev) ? ev : null;

            preview.Add(new VariablePreviewItem(
                variable.Name,
                variable.Label,
                variable.DefaultValue,
                hasProvidedValue ? providedValue : null,
                effectiveValue,
                variable.IsRequired,
                DetermineValueSource(variable, hasProvidedValue)));
        }

        return new VariablePreview(preview);
    }

    /// <summary>
    /// Validates that all required variables are provided without resolving.
    /// </summary>
    public bool AreAllRequiredVariablesProvided(
        StackDefinition stackDefinition,
        IDictionary<string, string> providedValues)
    {
        foreach (var variable in stackDefinition.GetRequiredVariables())
        {
            if (!providedValues.TryGetValue(variable.Name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
        }
        return true;
    }

    private Dictionary<string, string> BuildEffectiveValues(
        StackDefinition stackDefinition,
        IDictionary<string, string> providedValues)
    {
        var effectiveValues = new Dictionary<string, string>();

        // First, apply defaults
        foreach (var variable in stackDefinition.Variables)
        {
            if (!string.IsNullOrEmpty(variable.DefaultValue))
            {
                effectiveValues[variable.Name] = variable.DefaultValue;
            }
        }

        // Then override with provided values
        foreach (var (key, value) in providedValues)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                effectiveValues[key] = value;
            }
        }

        return effectiveValues;
    }

    private IReadOnlyList<ServiceTemplate> ResolveServicesVariables(
        IReadOnlyList<ServiceTemplate> services,
        Dictionary<string, string> effectiveValues,
        List<VariableResolutionError> errors)
    {
        var resolvedServices = new List<ServiceTemplate>();

        foreach (var service in services)
        {
            var resolvedEnv = new Dictionary<string, string>();
            foreach (var (key, value) in service.Environment)
            {
                resolvedEnv[key] = ResolveString(value, effectiveValues, errors);
            }

            var resolvedService = service with
            {
                Image = ResolveString(service.Image, effectiveValues, errors),
                Environment = resolvedEnv,
                Command = service.Command != null ? ResolveString(service.Command, effectiveValues, errors) : null,
                Entrypoint = service.Entrypoint != null ? ResolveString(service.Entrypoint, effectiveValues, errors) : null
            };

            resolvedServices.Add(resolvedService);
        }

        return resolvedServices.AsReadOnly();
    }

    private string ResolveString(
        string content,
        Dictionary<string, string> effectiveValues,
        List<VariableResolutionError> errors)
    {
        return VariablePattern.Replace(content, match =>
        {
            // ${VAR} or ${VAR:-default} format
            var varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[4].Value;
            var inlineDefault = match.Groups[3].Success ? match.Groups[3].Value : null;

            // Try to get value from effective values
            if (effectiveValues.TryGetValue(varName, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            // Use inline default if available
            if (inlineDefault != null)
            {
                return inlineDefault;
            }

            // Variable not resolved - keep original for debugging
            errors.Add(new VariableResolutionError(
                varName,
                VariableResolutionErrorType.UnresolvedVariable,
                $"Variable '{varName}' could not be resolved."));

            return match.Value;
        });
    }

    private static VariableValueSource DetermineValueSource(
        Variable variable,
        bool hasProvidedValue)
    {
        if (hasProvidedValue)
            return VariableValueSource.Provided;

        if (!string.IsNullOrEmpty(variable.DefaultValue))
            return VariableValueSource.Default;

        return VariableValueSource.Missing;
    }
}

/// <summary>
/// Result of variable resolution.
/// Contains resolved service templates with substituted variable values.
/// </summary>
public record VariableResolutionResult(
    bool IsSuccess,
    IReadOnlyList<ServiceTemplate> ResolvedServices,
    IReadOnlyDictionary<string, string> ResolvedVariables,
    IReadOnlyList<VariableResolutionError> Errors);

/// <summary>
/// A variable resolution error.
/// </summary>
public record VariableResolutionError(
    string VariableName,
    VariableResolutionErrorType Type,
    string Message);

/// <summary>
/// Types of variable resolution errors.
/// </summary>
public enum VariableResolutionErrorType
{
    RequiredVariableMissing,
    ValidationFailed,
    UnresolvedVariable
}

/// <summary>
/// A variable extracted from content.
/// </summary>
public record ExtractedVariable(string Name, string? InlineDefault);

/// <summary>
/// Preview of how variables will be resolved.
/// </summary>
public record VariablePreview(IReadOnlyList<VariablePreviewItem> Items)
{
    /// <summary>
    /// Gets variables that are missing values.
    /// </summary>
    public IEnumerable<VariablePreviewItem> GetMissing() =>
        Items.Where(i => i.ValueSource == VariableValueSource.Missing);

    /// <summary>
    /// Gets variables using default values.
    /// </summary>
    public IEnumerable<VariablePreviewItem> GetUsingDefaults() =>
        Items.Where(i => i.ValueSource == VariableValueSource.Default);

    /// <summary>
    /// Gets variables with provided values.
    /// </summary>
    public IEnumerable<VariablePreviewItem> GetProvided() =>
        Items.Where(i => i.ValueSource == VariableValueSource.Provided);
}

/// <summary>
/// A single variable in the preview.
/// </summary>
public record VariablePreviewItem(
    string Name,
    string? Label,
    string? DefaultValue,
    string? ProvidedValue,
    string? EffectiveValue,
    bool IsRequired,
    VariableValueSource ValueSource);

/// <summary>
/// Source of a variable's value.
/// </summary>
public enum VariableValueSource
{
    /// <summary>Value was provided by user.</summary>
    Provided,
    /// <summary>Using default value from stack definition.</summary>
    Default,
    /// <summary>No value available (variable is missing).</summary>
    Missing
}

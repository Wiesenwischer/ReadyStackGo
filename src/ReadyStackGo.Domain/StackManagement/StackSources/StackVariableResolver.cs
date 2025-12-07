namespace ReadyStackGo.Domain.StackManagement.StackSources;

using System.Text.RegularExpressions;

/// <summary>
/// Domain service for resolving variables in stack definitions.
/// Handles variable substitution, defaults, and validation.
/// </summary>
public class StackVariableResolver
{
    private static readonly Regex VariablePattern = new(
        @"\$\{([A-Za-z_][A-Za-z0-9_]*)(:-([^}]*))?\}|\$([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Resolves all variables in the YAML content with provided values.
    /// </summary>
    /// <param name="stackDefinition">The stack definition containing the template</param>
    /// <param name="providedValues">Variables provided by the user</param>
    /// <returns>Resolution result with resolved content and any errors</returns>
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

        // If there are errors, don't resolve the content
        if (errors.Count > 0)
        {
            return new VariableResolutionResult(
                false,
                stackDefinition.YamlContent,
                resolvedVariables,
                errors);
        }

        // Resolve variables in the YAML content
        var resolvedContent = ResolveContent(stackDefinition.YamlContent, effectiveValues, errors);

        // Also resolve variables in additional files
        var resolvedAdditionalFiles = new Dictionary<string, string>();
        foreach (var (fileName, content) in stackDefinition.AdditionalFileContents)
        {
            resolvedAdditionalFiles[fileName] = ResolveContent(content, effectiveValues, errors);
        }

        return new VariableResolutionResult(
            errors.Count == 0,
            resolvedContent,
            resolvedVariables,
            errors,
            resolvedAdditionalFiles);
    }

    /// <summary>
    /// Extracts all variable references from YAML content.
    /// </summary>
    public IEnumerable<ExtractedVariable> ExtractVariables(string yamlContent)
    {
        if (string.IsNullOrEmpty(yamlContent))
            yield break;

        var matches = VariablePattern.Matches(yamlContent);
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

    private string ResolveContent(
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
        StackVariable variable,
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
/// </summary>
public record VariableResolutionResult(
    bool IsSuccess,
    string ResolvedContent,
    IReadOnlyDictionary<string, string> ResolvedVariables,
    IReadOnlyList<VariableResolutionError> Errors,
    IReadOnlyDictionary<string, string>? ResolvedAdditionalFiles = null);

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
/// A variable extracted from YAML content.
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

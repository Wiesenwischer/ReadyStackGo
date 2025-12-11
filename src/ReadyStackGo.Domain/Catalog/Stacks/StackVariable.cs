using ReadyStackGo.Domain.Catalog.Manifests;

namespace ReadyStackGo.Domain.Catalog.Stacks;

/// <summary>
/// Represents an environment variable in a stack definition.
/// Extended with Type, Pattern validation, and Select options.
/// </summary>
public record StackVariable
{
    /// <summary>
    /// Variable name (e.g., "DATABASE_URL").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable label for UI display.
    /// </summary>
    public string? Label { get; }

    /// <summary>
    /// Default value if specified.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// Whether this variable is required (no default value).
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Optional description for UI display.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Variable type for validation and UI rendering.
    /// </summary>
    public VariableType Type { get; }

    /// <summary>
    /// Regex pattern for validation (String type only).
    /// </summary>
    public string? Pattern { get; }

    /// <summary>
    /// Error message when pattern validation fails.
    /// </summary>
    public string? PatternError { get; }

    /// <summary>
    /// Options for Select type variables.
    /// </summary>
    public IReadOnlyList<SelectOption>? Options { get; }

    /// <summary>
    /// Minimum value (for Number type).
    /// </summary>
    public double? Min { get; }

    /// <summary>
    /// Maximum value (for Number type).
    /// </summary>
    public double? Max { get; }

    /// <summary>
    /// Placeholder text for input field.
    /// </summary>
    public string? Placeholder { get; }

    /// <summary>
    /// Group name for organizing variables in UI.
    /// </summary>
    public string? Group { get; }

    /// <summary>
    /// Display order within group.
    /// </summary>
    public int Order { get; }

    public StackVariable(string name, string? defaultValue = null, string? description = null)
        : this(name, defaultValue, description, VariableType.String) { }

    public StackVariable(
        string name,
        string? defaultValue,
        string? description,
        VariableType type,
        string? label = null,
        string? pattern = null,
        string? patternError = null,
        IEnumerable<SelectOption>? options = null,
        double? min = null,
        double? max = null,
        string? placeholder = null,
        string? group = null,
        int order = 0,
        bool? isRequired = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty.", nameof(name));

        Name = name;
        Label = label;
        DefaultValue = defaultValue;
        IsRequired = isRequired ?? defaultValue == null;
        Description = description;
        Type = type;
        Pattern = pattern;
        PatternError = patternError;
        Options = options?.ToList().AsReadOnly();
        Min = min;
        Max = max;
        Placeholder = placeholder;
        Group = group;
        Order = order;
    }

    /// <summary>
    /// Creates a copy with a new default value.
    /// </summary>
    public StackVariable WithDefaultValue(string? defaultValue)
    {
        return new StackVariable(
            Name, defaultValue, Description, Type, Label,
            Pattern, PatternError, Options, Min, Max,
            Placeholder, Group, Order, IsRequired);
    }

    /// <summary>
    /// Validates a value against this variable's constraints.
    /// </summary>
    public ValidationResult Validate(string? value)
    {
        var errors = new List<string>();

        // Check required
        if (IsRequired && string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{Label ?? Name} is required.");
            return new ValidationResult(false, errors);
        }

        // Skip further validation if empty and not required
        if (string.IsNullOrWhiteSpace(value))
            return new ValidationResult(true, errors);

        switch (Type)
        {
            case VariableType.Number:
            case VariableType.Port:
                if (!double.TryParse(value, out var numValue))
                {
                    errors.Add($"{Label ?? Name} must be a valid number.");
                }
                else
                {
                    if (Min.HasValue && numValue < Min.Value)
                        errors.Add($"{Label ?? Name} must be at least {Min.Value}.");
                    if (Max.HasValue && numValue > Max.Value)
                        errors.Add($"{Label ?? Name} must be at most {Max.Value}.");
                    if (Type == VariableType.Port && (numValue < 1 || numValue > 65535))
                        errors.Add($"{Label ?? Name} must be a valid port (1-65535).");
                }
                break;

            case VariableType.Boolean:
                if (!bool.TryParse(value, out _) &&
                    !value.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                    !value.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                    value != "1" && value != "0")
                {
                    errors.Add($"{Label ?? Name} must be true or false.");
                }
                break;

            case VariableType.Select:
                if (Options != null && Options.Count > 0)
                {
                    if (!Options.Any(o => o.Value == value))
                    {
                        errors.Add($"{Label ?? Name} must be one of: {string.Join(", ", Options.Select(o => o.Value))}.");
                    }
                }
                break;

            case VariableType.String:
            case VariableType.Password:
                if (!string.IsNullOrEmpty(Pattern))
                {
                    try
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(value, Pattern))
                        {
                            errors.Add(PatternError ?? $"{Label ?? Name} does not match the required pattern.");
                        }
                    }
                    catch (System.Text.RegularExpressions.RegexParseException)
                    {
                        // Invalid regex pattern - skip validation
                    }
                }
                break;

            case VariableType.ConnectionString:
            case VariableType.SqlServerConnectionString:
            case VariableType.PostgresConnectionString:
            case VariableType.MySqlConnectionString:
            case VariableType.EventStoreConnectionString:
            case VariableType.MongoConnectionString:
            case VariableType.RedisConnectionString:
                // Connection strings are validated by the builder UI and test connection feature
                // Basic validation: just ensure it's not obviously malformed
                // Detailed validation happens server-side when testing the connection
                break;
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Option for Select type variables.
/// </summary>
public record SelectOption(string Value, string? Label = null, string? Description = null);

/// <summary>
/// Result of variable validation.
/// </summary>
public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

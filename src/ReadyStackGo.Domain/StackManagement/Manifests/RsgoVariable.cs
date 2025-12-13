namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Variable definition with type, validation, and UI hints.
/// </summary>
public class RsgoVariable
{
    /// <summary>
    /// Human-readable label for the variable.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Description shown as help text in UI.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Variable type for validation and UI rendering.
    /// </summary>
    public VariableType Type { get; set; } = VariableType.String;

    /// <summary>
    /// Default value.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Whether the variable is required.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Regex pattern for validation (only for String type).
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Error message when pattern validation fails.
    /// </summary>
    public string? PatternError { get; set; }

    /// <summary>
    /// Options for Select type variables.
    /// </summary>
    public List<RsgoSelectOption>? Options { get; set; }

    /// <summary>
    /// Minimum value (for Number type).
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Maximum value (for Number type).
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Placeholder text for input field.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Group name for organizing variables in UI.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Display order within group.
    /// </summary>
    public int Order { get; set; } = 0;
}

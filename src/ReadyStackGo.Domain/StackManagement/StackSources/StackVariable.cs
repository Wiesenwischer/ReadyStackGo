namespace ReadyStackGo.Domain.StackManagement.StackSources;

/// <summary>
/// Represents an environment variable in a stack definition.
/// </summary>
public record StackVariable
{
    /// <summary>
    /// Variable name (e.g., "DATABASE_URL").
    /// </summary>
    public string Name { get; }

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

    public StackVariable(string name, string? defaultValue = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty.", nameof(name));

        Name = name;
        DefaultValue = defaultValue;
        IsRequired = defaultValue == null;
        Description = description;
    }

    /// <summary>
    /// Creates a copy with a new default value.
    /// </summary>
    public StackVariable WithDefaultValue(string? defaultValue)
    {
        return new StackVariable(Name, defaultValue, Description);
    }
}

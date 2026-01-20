namespace ReadyStackGo.Domain.StackManagement.Stacks;

/// <summary>
/// Value object representing a description text.
/// Uses Null Object Pattern - Empty is used instead of null.
/// </summary>
public record Description
{
    /// <summary>
    /// Empty description (Null Object).
    /// </summary>
    public static readonly Description Empty = new(string.Empty);

    /// <summary>
    /// The description text.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Whether this description has content.
    /// </summary>
    public bool HasValue => !string.IsNullOrEmpty(Value);

    private Description(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a Description from a string value.
    /// Returns Empty if value is null or whitespace.
    /// </summary>
    public static Description From(string? value)
        => string.IsNullOrWhiteSpace(value) ? Empty : new Description(value);

    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion to string for convenience.
    /// </summary>
    public static implicit operator string(Description description) => description.Value;
}

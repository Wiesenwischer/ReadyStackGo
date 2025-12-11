namespace ReadyStackGo.Domain.Catalog.Sources;

/// <summary>
/// Strongly-typed identifier for a StackSource.
/// </summary>
public record StackSourceId
{
    public string Value { get; }

    public StackSourceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("StackSourceId cannot be empty.", nameof(value));

        Value = value;
    }

    public static StackSourceId Create(string value) => new(value);
    public static StackSourceId NewId() => new(Guid.NewGuid().ToString("N")[..8]);

    public override string ToString() => Value;

    public static implicit operator string(StackSourceId id) => id.Value;
}

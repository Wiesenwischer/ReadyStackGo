namespace ReadyStackGo.Domain.StackManagement.Stacks;

/// <summary>
/// Unique identifier for a stack, composed of its structural components.
/// </summary>
public record StackId(
    string SourceId,
    ProductId ProductId,
    string? Version,
    string StackName)
{
    /// <summary>
    /// Returns the string representation of the stack ID.
    /// Format: sourceId:productId:stackName or sourceId:productId:version:stackName (when versioned).
    /// </summary>
    public string Value => string.IsNullOrEmpty(Version)
        ? $"{SourceId}:{ProductId.Value}:{StackName}"
        : $"{SourceId}:{ProductId.Value}:{Version}:{StackName}";

    public override string ToString() => Value;

    /// <summary>
    /// Attempts to parse a stack ID string into its components.
    /// Supported formats:
    /// - 3 parts: sourceId:productId:stackName (unversioned)
    /// - 4 parts: sourceId:productId:version:stackName (versioned)
    /// </summary>
    /// <param name="stackIdString">The string to parse.</param>
    /// <param name="result">The parsed StackId if successful, null otherwise.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string? stackIdString, out StackId? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(stackIdString))
            return false;

        var parts = stackIdString.Split(':');

        result = parts.Length switch
        {
            3 => new StackId(parts[0], ProductId.FromName(parts[1]), null, parts[2]),
            4 => new StackId(parts[0], ProductId.FromName(parts[1]), parts[2], parts[3]),
            _ => null
        };

        return result != null;
    }
}

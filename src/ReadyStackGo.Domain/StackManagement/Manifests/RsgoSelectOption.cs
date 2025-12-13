namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Option for Select type variables.
/// </summary>
public class RsgoSelectOption
{
    /// <summary>
    /// Value to use when this option is selected.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Label to display in the UI.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Description or help text for this option.
    /// </summary>
    public string? Description { get; set; }
}

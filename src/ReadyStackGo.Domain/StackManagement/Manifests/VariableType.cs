namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Type of a stack variable for input validation and UI rendering.
/// v0.10: RSGo Manifest Format - Type Validation
/// </summary>
public enum VariableType
{
    /// <summary>
    /// Free-form text input (default).
    /// </summary>
    String = 0,

    /// <summary>
    /// Numeric input (integer or decimal).
    /// </summary>
    Number = 1,

    /// <summary>
    /// Boolean toggle (true/false).
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// Selection from predefined options.
    /// </summary>
    Select = 3,

    /// <summary>
    /// Password input (masked in UI, never logged).
    /// </summary>
    Password = 4,

    /// <summary>
    /// Port number (validated range 1-65535).
    /// </summary>
    Port = 5
}

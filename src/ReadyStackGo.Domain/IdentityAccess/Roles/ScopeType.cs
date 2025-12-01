namespace ReadyStackGo.Domain.IdentityAccess.Roles;

/// <summary>
/// Defines the scope level for role assignments.
/// </summary>
public enum ScopeType
{
    /// <summary>
    /// Global scope - applies to entire system.
    /// </summary>
    Global = 1,

    /// <summary>
    /// Organization scope - applies to a specific organization and its environments.
    /// </summary>
    Organization = 2,

    /// <summary>
    /// Environment scope - applies to a specific environment only.
    /// </summary>
    Environment = 4
}

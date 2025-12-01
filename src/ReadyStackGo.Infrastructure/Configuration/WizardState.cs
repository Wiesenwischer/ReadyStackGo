namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Represents the state of the setup wizard.
/// v0.4: Simplified from 4 steps to 3 steps (ConnectionsSet removed).
/// </summary>
public enum WizardState
{
    /// <summary>
    /// Initial state - no configuration exists
    /// </summary>
    NotStarted,

    /// <summary>
    /// Admin user has been created (Step 1 complete)
    /// </summary>
    AdminCreated,

    /// <summary>
    /// Organization has been set (Step 2 complete)
    /// </summary>
    OrganizationSet,

    /// <summary>
    /// Wizard completed - system is ready to use (Step 3 complete)
    /// </summary>
    Installed
}

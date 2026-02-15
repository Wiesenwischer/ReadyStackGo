namespace ReadyStackGo.Application.Services;

/// <summary>
/// Represents the state of the setup wizard.
/// v0.26: Simplified to 2 states only. Admin creation (Phase 1) transitions
/// directly to Installed. Organization setup moved to post-login onboarding.
/// </summary>
public enum WizardState
{
    /// <summary>
    /// Initial state - no admin user exists yet (Phase 1: unauthenticated setup)
    /// </summary>
    NotStarted,

    /// <summary>
    /// Admin created, wizard complete. Organization and further setup
    /// happen via the authenticated onboarding checklist (Phase 2).
    /// </summary>
    Installed
}

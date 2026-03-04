namespace ReadyStackGo.Application.Services;

/// <summary>
/// Provides the setup wizard (onboarding) step definitions for the current distribution.
/// Distributions implement this to customize which onboarding steps appear after admin login.
/// The admin-creation wizard (Phase 1) remains unchanged — this controls Phase 2 (post-login onboarding).
/// </summary>
public interface ISetupWizardDefinitionProvider
{
    SetupWizardDefinition GetDefinition();
}

/// <summary>
/// Defines the onboarding wizard for a specific distribution.
/// </summary>
public sealed class SetupWizardDefinition
{
    /// <summary>
    /// Distribution identifier, e.g. "generic", "ams-project".
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Ordered list of onboarding steps for this distribution.
    /// </summary>
    public required IReadOnlyList<WizardStepDefinition> Steps { get; init; }
}

/// <summary>
/// Defines a single step in the onboarding wizard.
/// </summary>
public sealed class WizardStepDefinition
{
    /// <summary>
    /// Unique step identifier, e.g. "organization", "environment", "stack-sources", "registries".
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display title for the step.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Display description for the step.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// UI component type that renders this step, e.g. "OrganizationStep", "RegistryTokenStep".
    /// </summary>
    public required string ComponentType { get; init; }

    /// <summary>
    /// Whether this step is required before onboarding can be dismissed.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Display order (lower = first).
    /// </summary>
    public int Order { get; init; }
}

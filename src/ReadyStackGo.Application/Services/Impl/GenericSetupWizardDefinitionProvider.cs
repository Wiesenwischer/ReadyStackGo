namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Default wizard definition for the generic RSGO distribution.
/// Mirrors the 4 existing hardcoded onboarding steps.
/// </summary>
public sealed class GenericSetupWizardDefinitionProvider : ISetupWizardDefinitionProvider
{
    public SetupWizardDefinition GetDefinition() => new()
    {
        Id = "generic",
        Steps =
        [
            new()
            {
                Id = "organization",
                Title = "Set Up Organization",
                Description = "Create your organization to manage environments and deployments.",
                ComponentType = "OrganizationStep",
                Required = true,
                Order = 1
            },
            new()
            {
                Id = "environment",
                Title = "Configure Environment",
                Description = "Set up a Docker environment for deployments.",
                ComponentType = "EnvironmentStep",
                Required = false,
                Order = 2
            },
            new()
            {
                Id = "stack-sources",
                Title = "Add Stack Sources",
                Description = "Add stack sources to browse and deploy stacks.",
                ComponentType = "StackSourcesStep",
                Required = false,
                Order = 3
            },
            new()
            {
                Id = "registries",
                Title = "Container Registries",
                Description = "Configure container registry credentials for image pulls.",
                ComponentType = "RegistriesStep",
                Required = false,
                Order = 4
            }
        ]
    };
}

using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.Application.Wizard;

/// <summary>
/// Service for managing the setup wizard flow
/// </summary>
public interface IWizardService
{
    /// <summary>
    /// Step 1: Create admin user with hashed password
    /// </summary>
    Task<CreateAdminResponse> CreateAdminAsync(CreateAdminRequest request);

    /// <summary>
    /// Step 2: Set organization information
    /// </summary>
    Task<SetOrganizationResponse> SetOrganizationAsync(SetOrganizationRequest request);

    /// <summary>
    /// Step 3: Set connection strings (Simple mode)
    /// </summary>
    Task<SetConnectionsResponse> SetConnectionsAsync(SetConnectionsRequest request);

    /// <summary>
    /// Step 4: Install stack from manifest
    /// </summary>
    Task<InstallStackResponse> InstallStackAsync(InstallStackRequest request);

    /// <summary>
    /// Get current wizard state
    /// </summary>
    Task<WizardStatusResponse> GetWizardStatusAsync();
}

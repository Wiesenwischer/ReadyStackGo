using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Manifests;
using ReadyStackGo.Domain.Organizations;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using ReadyStackGo.Infrastructure.Manifests;

namespace ReadyStackGo.Infrastructure.Wizard;

/// <summary>
/// Wizard service that orchestrates the 3-step setup process (v0.4+).
/// v0.4: Simplified from 4 steps to 3 steps - ConnectionsSet removed.
/// </summary>
public class WizardService : IWizardService
{
    private readonly IConfigStore _configStore;
    private readonly IManifestProvider _manifestProvider;
    private readonly IDeploymentEngine _deploymentEngine;
    private readonly ILogger<WizardService> _logger;

    public WizardService(
        IConfigStore configStore,
        IManifestProvider manifestProvider,
        IDeploymentEngine deploymentEngine,
        ILogger<WizardService> logger)
    {
        _configStore = configStore;
        _manifestProvider = manifestProvider;
        _deploymentEngine = deploymentEngine;
        _logger = logger;
    }

    public async Task<CreateAdminResponse> CreateAdminAsync(CreateAdminRequest request)
    {
        try
        {
            _logger.LogInformation("Creating admin user: {Username}", request.Username);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            // Validate wizard state
            if (systemConfig.WizardState != WizardState.NotStarted)
            {
                return new CreateAdminResponse
                {
                    Success = false,
                    Message = $"Wizard is in state {systemConfig.WizardState}. Admin can only be created when wizard is NotStarted."
                };
            }

            // Generate salt and hash password using BCrypt
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, salt);

            // Create security config with admin user
            var securityConfig = new SecurityConfig
            {
                LocalAdmin = new AdminUser
                {
                    Username = request.Username,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    Role = "admin"
                },
                LocalAdminFallbackEnabled = true
            };

            await _configStore.SaveSecurityConfigAsync(securityConfig);

            // Update wizard state
            systemConfig.WizardState = WizardState.AdminCreated;
            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Admin user created successfully: {Username}", request.Username);

            return new CreateAdminResponse
            {
                Success = true,
                Message = "Admin user created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create admin user");
            return new CreateAdminResponse
            {
                Success = false,
                Message = $"Failed to create admin user: {ex.Message}"
            };
        }
    }

    public async Task<SetOrganizationResponse> SetOrganizationAsync(SetOrganizationRequest request)
    {
        try
        {
            _logger.LogInformation("Setting organization: {OrgId} - {OrgName}", request.Id, request.Name);

            var systemConfig = await _configStore.GetSystemConfigAsync();

            // Validate wizard state
            if (systemConfig.WizardState != WizardState.AdminCreated)
            {
                return new SetOrganizationResponse
                {
                    Success = false,
                    Message = $"Wizard is in state {systemConfig.WizardState}. Organization can only be set after admin creation."
                };
            }

            // Set organization using factory method (v0.4: no environments created during wizard)
            systemConfig.Organization = Organization.Create(request.Id, request.Name);

            // Update wizard state
            systemConfig.WizardState = WizardState.OrganizationSet;
            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Organization set successfully: {OrgId} - {OrgName}", request.Id, request.Name);

            return new SetOrganizationResponse
            {
                Success = true,
                Message = "Organization set successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set organization");
            return new SetOrganizationResponse
            {
                Success = false,
                Message = $"Failed to set organization: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// DEPRECATED in v0.4: Global connection strings are replaced by stack-specific configuration.
    /// This method is kept for backwards compatibility and will be removed in v0.5.
    /// </summary>
    [Obsolete("Use stack-specific deployment configuration instead. Will be removed in v0.5.")]
    public async Task<SetConnectionsResponse> SetConnectionsAsync(SetConnectionsRequest request)
    {
        _logger.LogWarning("SetConnectionsAsync is deprecated in v0.4. Connection strings should be configured per stack deployment.");

        // Return success but do nothing - this step is no longer part of the wizard
        return await Task.FromResult(new SetConnectionsResponse
        {
            Success = false,
            Message = "This wizard step has been removed in v0.4. Connection strings are now configured per stack deployment."
        });
    }

    public async Task<InstallStackResponse> InstallStackAsync(InstallStackRequest request)
    {
        try
        {
            _logger.LogInformation("Completing wizard setup");

            var systemConfig = await _configStore.GetSystemConfigAsync();

            // Validate wizard state (v0.4: after OrganizationSet, not ConnectionsSet)
            if (systemConfig.WizardState != WizardState.OrganizationSet)
            {
                return new InstallStackResponse
                {
                    Success = false,
                    Errors = new List<string>
                    {
                        $"Wizard is in state {systemConfig.WizardState}. Setup can only be completed after organization is set."
                    }
                };
            }

            // Mark wizard as completed
            systemConfig.WizardState = WizardState.Installed;
            systemConfig.InstalledVersion = "v0.4.0";
            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Wizard setup completed successfully. System is ready to use. Environments can be created via Settings.");

            return new InstallStackResponse
            {
                Success = true,
                StackVersion = "v0.4.0",
                DeployedContexts = new List<string>(),
                Errors = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete wizard setup");
            return new InstallStackResponse
            {
                Success = false,
                Errors = new List<string> { $"Setup completion failed: {ex.Message}" }
            };
        }
    }

    public async Task<WizardStatusResponse> GetWizardStatusAsync()
    {
        try
        {
            var systemConfig = await _configStore.GetSystemConfigAsync();

            return new WizardStatusResponse
            {
                WizardState = systemConfig.WizardState.ToString(),
                IsCompleted = systemConfig.WizardState == WizardState.Installed,
                DefaultDockerSocketPath = GetDefaultDockerSocketPath()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get wizard status");
            return new WizardStatusResponse
            {
                WizardState = "Unknown",
                IsCompleted = false,
                DefaultDockerSocketPath = GetDefaultDockerSocketPath()
            };
        }
    }

    /// <summary>
    /// Returns the default Docker socket path based on the server's operating system.
    /// </summary>
    private static string GetDefaultDockerSocketPath()
    {
        return OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
    }
}

using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Application.Wizard.DTOs;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Manifests;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using ReadyStackGo.Infrastructure.Manifests;

namespace ReadyStackGo.Infrastructure.Wizard;

/// <summary>
/// Wizard service that orchestrates the 4-step setup process
/// Implements the wizard flow from specification chapter 7
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

            // Set organization
            systemConfig.Organization = new Organization
            {
                Id = request.Id,
                Name = request.Name
            };

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

    public async Task<SetConnectionsResponse> SetConnectionsAsync(SetConnectionsRequest request)
    {
        try
        {
            _logger.LogInformation("Setting connections in Simple mode");

            var systemConfig = await _configStore.GetSystemConfigAsync();

            // Validate wizard state
            if (systemConfig.WizardState != WizardState.OrganizationSet)
            {
                return new SetConnectionsResponse
                {
                    Success = false,
                    Message = $"Wizard is in state {systemConfig.WizardState}. Connections can only be set after organization setup."
                };
            }

            // Create connections config in Simple mode
            var connectionsConfig = new ContextsConfig
            {
                Mode = ConnectionMode.Simple,
                GlobalConnections = new GlobalConnections
                {
                    Transport = request.Transport,
                    Persistence = request.Persistence,
                    EventStore = request.EventStore ?? string.Empty
                }
            };

            await _configStore.SaveContextsConfigAsync(connectionsConfig);

            // Update wizard state
            systemConfig.WizardState = WizardState.ConnectionsSet;
            await _configStore.SaveSystemConfigAsync(systemConfig);

            _logger.LogInformation("Connections set successfully in Simple mode");

            return new SetConnectionsResponse
            {
                Success = true,
                Message = "Connections configured successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set connections");
            return new SetConnectionsResponse
            {
                Success = false,
                Message = $"Failed to set connections: {ex.Message}"
            };
        }
    }

    public async Task<InstallStackResponse> InstallStackAsync(InstallStackRequest request)
    {
        try
        {
            _logger.LogInformation("Installing stack via wizard");

            var systemConfig = await _configStore.GetSystemConfigAsync();

            // Validate wizard state
            if (systemConfig.WizardState != WizardState.ConnectionsSet)
            {
                return new InstallStackResponse
                {
                    Success = false,
                    Errors = new List<string>
                    {
                        $"Wizard is in state {systemConfig.WizardState}. Stack can only be installed after connections are set."
                    }
                };
            }

            // Load manifest (use latest if not specified)
            ReleaseManifest? manifest;
            if (!string.IsNullOrWhiteSpace(request.ManifestPath))
            {
                manifest = await _manifestProvider.LoadManifestAsync(request.ManifestPath);
            }
            else
            {
                manifest = await _manifestProvider.GetLatestManifestAsync();
                if (manifest == null)
                {
                    return new InstallStackResponse
                    {
                        Success = false,
                        Errors = new List<string> { "No manifests found to install" }
                    };
                }

                _logger.LogInformation("Using latest manifest: {StackVersion}", manifest.StackVersion);
            }

            // Validate manifest
            var validation = await _manifestProvider.ValidateManifestAsync(manifest);
            if (!validation.IsValid)
            {
                return new InstallStackResponse
                {
                    Success = false,
                    Errors = validation.Errors
                };
            }

            // Deploy stack
            var deploymentResult = await _deploymentEngine.DeployStackAsync(manifest);

            if (deploymentResult.Success)
            {
                // Update wizard state to Installed
                systemConfig.WizardState = WizardState.Installed;
                await _configStore.SaveSystemConfigAsync(systemConfig);

                _logger.LogInformation("Stack installed successfully via wizard: {StackVersion}", manifest.StackVersion);
            }

            return new InstallStackResponse
            {
                Success = deploymentResult.Success,
                StackVersion = manifest.StackVersion,
                DeployedContexts = deploymentResult.DeployedContexts,
                Errors = deploymentResult.Errors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install stack via wizard");
            return new InstallStackResponse
            {
                Success = false,
                Errors = new List<string> { $"Installation failed: {ex.Message}" }
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
                IsCompleted = systemConfig.WizardState == WizardState.Installed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get wizard status");
            return new WizardStatusResponse
            {
                WizardState = "Unknown",
                IsCompleted = false
            };
        }
    }
}

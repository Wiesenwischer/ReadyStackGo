using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;

namespace ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

public class CreateEnvironmentHandler : IRequestHandler<CreateEnvironmentCommand, CreateEnvironmentResponse>
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly ILogger<CreateEnvironmentHandler> _logger;

    public CreateEnvironmentHandler(
        IEnvironmentRepository environmentRepository,
        IOrganizationRepository organizationRepository,
        ICredentialEncryptionService credentialEncryptionService,
        ILogger<CreateEnvironmentHandler> logger)
    {
        _environmentRepository = environmentRepository;
        _organizationRepository = organizationRepository;
        _credentialEncryptionService = credentialEncryptionService;
        _logger = logger;
    }

    public Task<CreateEnvironmentResponse> Handle(CreateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating environment: {Name} (Type: {Type})", request.Name, request.Type);

            // Resolve Organization from IdentityAccess context
            var organization = _organizationRepository.GetAll().FirstOrDefault();

            if (organization == null)
            {
                return Task.FromResult(new CreateEnvironmentResponse
                {
                    Success = false,
                    Message = "Organization not set. Complete the setup wizard first."
                });
            }

            // Convert to Deployment context OrganizationId (Anti-Corruption Layer)
            var deploymentOrgId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);

            // Check for duplicate name
            var existingByName = _environmentRepository.GetByName(deploymentOrgId, request.Name);
            if (existingByName != null)
            {
                return Task.FromResult(new CreateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment with name '{request.Name}' already exists."
                });
            }

            // Create environment via Domain factory method based on type
            var environmentId = _environmentRepository.NextIdentity();
            DomainEnvironment environment;

            if (request.Type == "SshTunnel")
            {
                environment = CreateSshTunnelEnvironment(environmentId, deploymentOrgId, request);
            }
            else
            {
                environment = DomainEnvironment.CreateDockerSocket(
                    environmentId,
                    deploymentOrgId,
                    request.Name,
                    null,
                    request.SocketPath ?? "/var/run/docker.sock");
            }

            // Set as default if this is the first environment
            var existingEnvironments = _environmentRepository.GetByOrganization(deploymentOrgId);
            if (!existingEnvironments.Any())
            {
                environment.SetAsDefault();
            }

            _environmentRepository.Add(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment created successfully: {Id} (Type: {Type})", environmentId, request.Type);

            return Task.FromResult(new CreateEnvironmentResponse
            {
                Success = true,
                Message = "Environment created successfully",
                Environment = EnvironmentMapper.ToResponse(environment)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create environment: {Name}", request.Name);
            return Task.FromResult(new CreateEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to create environment: {ex.Message}"
            });
        }
    }

    private DomainEnvironment CreateSshTunnelEnvironment(
        EnvironmentId environmentId,
        DeploymentOrganizationId deploymentOrgId,
        CreateEnvironmentCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.SshHost))
            throw new ArgumentException("SSH host is required for SSH tunnel environments.");
        if (string.IsNullOrWhiteSpace(request.SshUsername))
            throw new ArgumentException("SSH username is required for SSH tunnel environments.");
        if (string.IsNullOrWhiteSpace(request.SshSecret))
            throw new ArgumentException("SSH credential (password or private key) is required.");

        var authMethod = request.SshAuthMethod?.ToLowerInvariant() switch
        {
            "password" => SshAuthMethod.Password,
            _ => SshAuthMethod.PrivateKey
        };

        var sshConfig = SshTunnelConfig.Create(
            request.SshHost,
            request.SshPort ?? 22,
            request.SshUsername,
            authMethod,
            request.RemoteSocketPath ?? "/var/run/docker.sock");

        var encryptedSecret = _credentialEncryptionService.Encrypt(request.SshSecret);
        var sshCredential = SshCredential.Create(encryptedSecret, authMethod);

        return DomainEnvironment.CreateSshTunnel(
            environmentId,
            deploymentOrgId,
            request.Name,
            null,
            sshConfig,
            sshCredential);
    }
}

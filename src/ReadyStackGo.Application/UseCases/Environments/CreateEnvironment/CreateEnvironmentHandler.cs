using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;

namespace ReadyStackGo.Application.UseCases.Environments.CreateEnvironment;

public class CreateEnvironmentHandler : IRequestHandler<CreateEnvironmentCommand, CreateEnvironmentResponse>
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<CreateEnvironmentHandler> _logger;

    public CreateEnvironmentHandler(
        IEnvironmentRepository environmentRepository,
        IOrganizationRepository organizationRepository,
        ILogger<CreateEnvironmentHandler> logger)
    {
        _environmentRepository = environmentRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public Task<CreateEnvironmentResponse> Handle(CreateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Creating environment: {Name}", request.Name);

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

            // Create environment via Domain factory method
            var environmentId = _environmentRepository.NextIdentity();
            var environment = DomainEnvironment.CreateDockerSocket(
                environmentId,
                deploymentOrgId,
                request.Name,
                null,
                request.SocketPath);

            // Set as default if this is the first environment
            var existingEnvironments = _environmentRepository.GetByOrganization(deploymentOrgId);
            if (!existingEnvironments.Any())
            {
                environment.SetAsDefault();
            }

            _environmentRepository.Add(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment created successfully: {Id}", environmentId);

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
}

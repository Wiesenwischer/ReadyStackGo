using MediatR;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.Application.UseCases.Environments.ListEnvironments;

public class ListEnvironmentsHandler : IRequestHandler<ListEnvironmentsQuery, ListEnvironmentsResponse>
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public ListEnvironmentsHandler(
        IEnvironmentRepository environmentRepository,
        IOrganizationRepository organizationRepository)
    {
        _environmentRepository = environmentRepository;
        _organizationRepository = organizationRepository;
    }

    public Task<ListEnvironmentsResponse> Handle(ListEnvironmentsQuery request, CancellationToken cancellationToken)
    {
        // Resolve Organization from IdentityAccess context
        var organization = _organizationRepository.GetAll().FirstOrDefault();

        if (organization == null)
        {
            return Task.FromResult(new ListEnvironmentsResponse
            {
                Success = true,
                Environments = new List<EnvironmentResponse>()
            });
        }

        // Convert to Deployment context OrganizationId (Anti-Corruption Layer)
        var deploymentOrgId = DeploymentOrganizationId.FromIdentityAccess(organization.Id);

        // Query repository and map to DTOs
        var environments = _environmentRepository.GetByOrganization(deploymentOrgId)
            .Select(EnvironmentMapper.ToResponse)
            .ToList();

        return Task.FromResult(new ListEnvironmentsResponse
        {
            Success = true,
            Environments = environments
        });
    }
}

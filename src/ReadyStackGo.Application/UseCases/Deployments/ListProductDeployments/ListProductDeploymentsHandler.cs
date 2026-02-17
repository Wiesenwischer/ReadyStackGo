using MediatR;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.ListProductDeployments;

/// <summary>
/// Handles listing product deployments for an environment.
/// Excludes removed deployments.
/// </summary>
public class ListProductDeploymentsHandler
    : IRequestHandler<ListProductDeploymentsQuery, ListProductDeploymentsResponse>
{
    private readonly IProductDeploymentRepository _repository;

    public ListProductDeploymentsHandler(IProductDeploymentRepository repository)
    {
        _repository = repository;
    }

    public Task<ListProductDeploymentsResponse> Handle(
        ListProductDeploymentsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return Task.FromResult(new ListProductDeploymentsResponse());
        }

        var deployments = _repository.GetByEnvironment(new EnvironmentId(envGuid))
            .Where(pd => !pd.IsTerminal)
            .OrderByDescending(pd => pd.CreatedAt)
            .ToList();

        var response = new ListProductDeploymentsResponse
        {
            Success = true,
            ProductDeployments = deployments.Select(pd => new ProductDeploymentSummaryDto
            {
                ProductDeploymentId = pd.Id.Value.ToString(),
                ProductGroupId = pd.ProductGroupId,
                ProductName = pd.ProductName,
                ProductDisplayName = pd.ProductDisplayName,
                ProductVersion = pd.ProductVersion,
                Status = pd.Status.ToString(),
                CreatedAt = pd.CreatedAt,
                CompletedAt = pd.CompletedAt,
                ErrorMessage = pd.ErrorMessage,
                TotalStacks = pd.TotalStacks,
                CompletedStacks = pd.CompletedStacks,
                FailedStacks = pd.FailedStacks,
                CanUpgrade = pd.CanUpgrade,
                CanRemove = pd.CanRemove
            }).ToList()
        };

        return Task.FromResult(response);
    }
}

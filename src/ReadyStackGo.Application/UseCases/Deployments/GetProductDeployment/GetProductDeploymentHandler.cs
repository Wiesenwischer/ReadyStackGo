using MediatR;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;

/// <summary>
/// Handles retrieving a product deployment by ID.
/// </summary>
public class GetProductDeploymentHandler : IRequestHandler<GetProductDeploymentQuery, GetProductDeploymentResponse?>
{
    private readonly IProductDeploymentRepository _repository;

    public GetProductDeploymentHandler(IProductDeploymentRepository repository)
    {
        _repository = repository;
    }

    public Task<GetProductDeploymentResponse?> Handle(
        GetProductDeploymentQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProductDeploymentId, out var idGuid))
        {
            return Task.FromResult<GetProductDeploymentResponse?>(null);
        }

        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(idGuid));
        if (productDeployment == null)
        {
            return Task.FromResult<GetProductDeploymentResponse?>(null);
        }

        return Task.FromResult<GetProductDeploymentResponse?>(MapToResponse(productDeployment));
    }

    internal static GetProductDeploymentResponse MapToResponse(ProductDeployment pd)
    {
        return new GetProductDeploymentResponse
        {
            ProductDeploymentId = pd.Id.Value.ToString(),
            EnvironmentId = pd.EnvironmentId.Value.ToString(),
            ProductGroupId = pd.ProductGroupId,
            ProductId = pd.ProductId,
            ProductName = pd.ProductName,
            ProductDisplayName = pd.ProductDisplayName,
            ProductVersion = pd.ProductVersion,
            Status = pd.Status.ToString(),
            CreatedAt = pd.CreatedAt,
            CompletedAt = pd.CompletedAt,
            ErrorMessage = pd.ErrorMessage,
            ContinueOnError = pd.ContinueOnError,
            TotalStacks = pd.TotalStacks,
            CompletedStacks = pd.CompletedStacks,
            FailedStacks = pd.FailedStacks,
            PreviousVersion = pd.PreviousVersion,
            UpgradeCount = pd.UpgradeCount,
            CanUpgrade = pd.CanUpgrade,
            CanRemove = pd.CanRemove,
            DurationSeconds = pd.GetDuration()?.TotalSeconds,
            SharedVariables = new Dictionary<string, string>(pd.SharedVariables),
            Stacks = pd.GetStacksInDeployOrder().Select(s => new ProductStackDeploymentDto
            {
                StackName = s.StackName,
                StackDisplayName = s.StackDisplayName,
                StackId = s.StackId,
                DeploymentId = s.DeploymentId?.Value.ToString(),
                DeploymentStackName = s.DeploymentStackName,
                Status = s.Status.ToString(),
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                ErrorMessage = s.ErrorMessage,
                Order = s.Order,
                ServiceCount = s.ServiceCount,
                IsNewInUpgrade = s.IsNewInUpgrade
            }).ToList()
        };
    }
}

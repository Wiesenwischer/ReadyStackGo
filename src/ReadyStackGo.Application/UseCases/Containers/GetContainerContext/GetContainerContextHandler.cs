using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Containers.GetContainerContext;

public class GetContainerContextHandler : IRequestHandler<GetContainerContextQuery, GetContainerContextResult>
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductCache _productCache;

    public GetContainerContextHandler(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IProductCache productCache)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _productCache = productCache;
    }

    public async Task<GetContainerContextResult> Handle(GetContainerContextQuery request, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
                return GetContainerContextResult.Failed("Invalid environment ID format.");

            var environmentId = new EnvironmentId(envGuid);

            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);

            var stackNames = containers
                .Where(c => c.Labels.ContainsKey("rsgo.stack"))
                .Select(c => c.Labels["rsgo.stack"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var stacks = new Dictionary<string, StackContextInfo>();

            foreach (var stackName in stackNames)
            {
                var deployment = _deploymentRepository.GetByStackName(environmentId, stackName);

                string? productName = null;
                string? productDisplayName = null;

                if (deployment != null && StackId.TryParse(deployment.StackId, out var parsedStackId))
                {
                    var stackDef = _productCache.GetStack(parsedStackId!.Value);
                    productName = stackDef?.ProductName;
                    productDisplayName = stackDef?.ProductDisplayName;
                }

                stacks[stackName] = new StackContextInfo(
                    stackName,
                    deployment != null,
                    deployment?.Id.ToString(),
                    productName,
                    productDisplayName);
            }

            return new GetContainerContextResult(true, stacks);
        }
        catch (InvalidOperationException ex)
        {
            return GetContainerContextResult.Failed(ex.Message);
        }
    }
}

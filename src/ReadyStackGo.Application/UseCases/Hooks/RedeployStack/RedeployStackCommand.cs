using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Hooks.RedeployStack;

public record RedeployStackRequest
{
    public string? StackName { get; init; }
    public string? EnvironmentId { get; init; }
    public string? ProductId { get; init; }
    public string? StackDefinitionName { get; init; }
    public Dictionary<string, string>? Variables { get; init; }
}

public record RedeployStackResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? ProductDeploymentId { get; init; }
    public string? StackName { get; init; }
    public string? StackVersion { get; init; }

    public static RedeployStackResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record RedeployStackCommand(
    string? StackName,
    string EnvironmentId,
    Dictionary<string, string>? Variables = null,
    string? ProductId = null,
    string? StackDefinitionName = null
) : IRequest<RedeployStackResponse>;

public class RedeployStackHandler : IRequestHandler<RedeployStackCommand, RedeployStackResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RedeployStackHandler> _logger;

    public RedeployStackHandler(
        IDeploymentRepository deploymentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        IEnvironmentRepository environmentRepository,
        IMediator mediator,
        ILogger<RedeployStackHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productDeploymentRepository = productDeploymentRepository;
        _environmentRepository = environmentRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RedeployStackResponse> Handle(RedeployStackCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve environment (GUID or name)
        var (resolvedEnvId, envError) = EnvironmentResolver.Resolve(request.EnvironmentId, _environmentRepository);
        if (resolvedEnvId == null)
        {
            return RedeployStackResponse.Failed(envError!);
        }

        var environmentId = resolvedEnvId;

        // 2. Route: Product redeploy or standalone stack redeploy?
        if (!string.IsNullOrWhiteSpace(request.ProductId))
        {
            return await HandleProductRedeploy(request, environmentId, cancellationToken);
        }

        return await HandleStandaloneRedeploy(request, environmentId, cancellationToken);
    }

    private async Task<RedeployStackResponse> HandleProductRedeploy(
        RedeployStackCommand request, EnvironmentId environmentId, CancellationToken cancellationToken)
    {
        // 1. Find active ProductDeployment by ProductGroupId
        var productDeployment = _productDeploymentRepository
            .GetActiveByProductGroupId(environmentId, request.ProductId!);

        if (productDeployment == null)
        {
            return RedeployStackResponse.Failed(
                $"No active product deployment found for product '{request.ProductId}' in environment '{request.EnvironmentId}'.");
        }

        // 2. Determine stack selection
        List<string>? stackNames = null;
        if (!string.IsNullOrWhiteSpace(request.StackDefinitionName))
        {
            stackNames = new List<string> { request.StackDefinitionName };
        }

        _logger.LogInformation(
            "Starting product redeploy of '{ProductName}' (id: {ProductDeploymentId}) in environment {EnvironmentId}, stacks: {Stacks}",
            productDeployment.ProductName,
            productDeployment.Id.Value,
            request.EnvironmentId,
            stackNames != null ? string.Join(", ", stackNames) : "all");

        // 3. Dispatch RedeployProductCommand
        var result = await _mediator.Send(new RedeployProductCommand(
            request.EnvironmentId,
            productDeployment.Id.Value.ToString(),
            stackNames,
            request.Variables,
            null,
            true,
            null), cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Product redeploy of '{ProductName}' failed: {Message}",
                productDeployment.ProductName, result.Message);

            return RedeployStackResponse.Failed(result.Message ?? "Product redeploy failed.");
        }

        _logger.LogInformation(
            "Successfully triggered product redeploy of '{ProductName}'",
            productDeployment.ProductName);

        return new RedeployStackResponse
        {
            Success = true,
            Message = $"Successfully triggered redeploy of product '{productDeployment.ProductName}'.",
            ProductDeploymentId = result.ProductDeploymentId,
            StackName = productDeployment.ProductName,
            StackVersion = productDeployment.ProductVersion
        };
    }

    private async Task<RedeployStackResponse> HandleStandaloneRedeploy(
        RedeployStackCommand request, EnvironmentId environmentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StackName))
        {
            return RedeployStackResponse.Failed("Either stackName or productId is required.");
        }

        // 1. Find deployment by stack name + environment
        var deployment = _deploymentRepository.GetByStackName(environmentId, request.StackName);
        if (deployment == null)
        {
            return RedeployStackResponse.Failed(
                $"No deployment found for stack '{request.StackName}' in environment '{request.EnvironmentId}'.");
        }

        // 2. Validate: only running stacks can be redeployed
        if (deployment.Status != DeploymentStatus.Running)
        {
            return RedeployStackResponse.Failed(
                $"Only running deployments can be redeployed. Current status: {deployment.Status}");
        }

        // 3. Extract redeployment data from existing deployment
        var (stackId, stackVersion, storedVariables) = deployment.GetRedeploymentData();

        // 4. Merge variables: stored deployment values as base, webhook values as overrides
        var variables = new Dictionary<string, string>(storedVariables);
        if (request.Variables != null)
        {
            foreach (var kvp in request.Variables)
            {
                variables[kvp.Key] = kvp.Value;
            }
        }

        _logger.LogInformation(
            "Starting redeploy of stack '{StackName}' (version {Version}) in environment {EnvironmentId} with {VarCount} variables ({OverrideCount} overrides from webhook)",
            request.StackName, stackVersion, request.EnvironmentId, variables.Count, request.Variables?.Count ?? 0);

        // 5. Delegate to DeployStackCommand (same parameters, no SessionId for webhook)
        var deployResult = await _mediator.Send(new DeployStackCommand(
            request.EnvironmentId,
            stackId,
            deployment.StackName,
            variables,
            null), cancellationToken);

        if (!deployResult.Success)
        {
            _logger.LogWarning(
                "Redeploy of stack '{StackName}' failed: {Message}",
                request.StackName, deployResult.Message);

            return RedeployStackResponse.Failed(deployResult.Message ?? "Redeploy failed.");
        }

        _logger.LogInformation(
            "Successfully redeployed stack '{StackName}' (version {Version})",
            request.StackName, stackVersion);

        return new RedeployStackResponse
        {
            Success = true,
            Message = $"Successfully triggered redeploy of '{request.StackName}'.",
            DeploymentId = deployResult.DeploymentId,
            StackName = request.StackName,
            StackVersion = stackVersion
        };
    }
}

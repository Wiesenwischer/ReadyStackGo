using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Hooks.RedeployStack;

public record RedeployStackRequest
{
    public required string StackName { get; init; }
    public string? EnvironmentId { get; init; }
}

public record RedeployStackResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? StackName { get; init; }
    public string? StackVersion { get; init; }

    public static RedeployStackResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record RedeployStackCommand(
    string StackName,
    string EnvironmentId
) : IRequest<RedeployStackResponse>;

public class RedeployStackHandler : IRequestHandler<RedeployStackCommand, RedeployStackResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RedeployStackHandler> _logger;

    public RedeployStackHandler(
        IDeploymentRepository deploymentRepository,
        IMediator mediator,
        ILogger<RedeployStackHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RedeployStackResponse> Handle(RedeployStackCommand request, CancellationToken cancellationToken)
    {
        // 1. Parse environment ID
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return RedeployStackResponse.Failed("Invalid environment ID format.");
        }

        var environmentId = new EnvironmentId(envGuid);

        // 2. Find deployment by stack name + environment
        var deployment = _deploymentRepository.GetByStackName(environmentId, request.StackName);
        if (deployment == null)
        {
            return RedeployStackResponse.Failed(
                $"No deployment found for stack '{request.StackName}' in environment '{request.EnvironmentId}'.");
        }

        // 3. Validate: only running stacks can be redeployed
        if (deployment.Status != DeploymentStatus.Running)
        {
            return RedeployStackResponse.Failed(
                $"Only running deployments can be redeployed. Current status: {deployment.Status}");
        }

        // 4. Extract redeployment data from existing deployment
        var (stackId, stackVersion, variables) = deployment.GetRedeploymentData();

        _logger.LogInformation(
            "Starting redeploy of stack '{StackName}' (version {Version}) in environment {EnvironmentId}",
            request.StackName, stackVersion, request.EnvironmentId);

        // 5. Delegate to DeployStackCommand (same parameters, no SessionId for webhook)
        var deployResult = await _mediator.Send(new DeployStackCommand(
            request.EnvironmentId,
            stackId,
            deployment.StackName,
            new Dictionary<string, string>(variables),
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

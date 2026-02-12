using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Hooks.DeployStack;

public record DeployViaHookRequest
{
    public required string StackId { get; init; }
    public required string StackName { get; init; }
    public string? EnvironmentId { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
}

public record DeployViaHookResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? StackName { get; init; }
    public string? StackVersion { get; init; }
    public string? Action { get; init; }

    public static DeployViaHookResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record DeployViaHookCommand(
    string StackId,
    string StackName,
    string EnvironmentId,
    Dictionary<string, string> Variables
) : IRequest<DeployViaHookResponse>;

public class DeployViaHookHandler : IRequestHandler<DeployViaHookCommand, DeployViaHookResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<DeployViaHookHandler> _logger;

    public DeployViaHookHandler(
        IDeploymentRepository deploymentRepository,
        IMediator mediator,
        ILogger<DeployViaHookHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<DeployViaHookResponse> Handle(DeployViaHookCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate inputs
        if (string.IsNullOrWhiteSpace(request.StackId))
        {
            return DeployViaHookResponse.Failed("StackId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StackName))
        {
            return DeployViaHookResponse.Failed("StackName is required.");
        }

        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return DeployViaHookResponse.Failed("Invalid environment ID format.");
        }

        var environmentId = new EnvironmentId(envGuid);

        // 2. Check for existing deployment (idempotent behavior)
        var existing = _deploymentRepository.GetByStackName(environmentId, request.StackName);
        string action;
        string stackId;

        if (existing != null)
        {
            // Stack already deployed — redeploy if running, error otherwise
            if (existing.Status != DeploymentStatus.Running)
            {
                return DeployViaHookResponse.Failed(
                    $"Stack '{request.StackName}' exists but is not running (status: {existing.Status}). " +
                    "Only running deployments can be redeployed.");
            }

            var (existingStackId, _, _) = existing.GetRedeploymentData();
            stackId = existingStackId;
            action = "redeployed";

            _logger.LogInformation(
                "Stack '{StackName}' already running in environment {EnvironmentId}, triggering redeploy",
                request.StackName, request.EnvironmentId);
        }
        else
        {
            // Fresh deploy
            stackId = request.StackId;
            action = "deployed";

            _logger.LogInformation(
                "Deploying stack '{StackName}' ({StackId}) to environment {EnvironmentId}",
                request.StackName, request.StackId, request.EnvironmentId);
        }

        // 3. Delegate to DeployStackCommand
        var deployResult = await _mediator.Send(new DeployStackCommand(
            request.EnvironmentId,
            stackId,
            request.StackName,
            request.Variables,
            null), cancellationToken);

        if (!deployResult.Success)
        {
            _logger.LogWarning(
                "Deploy of stack '{StackName}' failed: {Message}",
                request.StackName, deployResult.Message);

            return DeployViaHookResponse.Failed(deployResult.Message ?? "Deploy failed.");
        }

        _logger.LogInformation(
            "Successfully {Action} stack '{StackName}' (version {Version})",
            action, request.StackName, deployResult.StackVersion);

        return new DeployViaHookResponse
        {
            Success = true,
            Message = $"Successfully {action} '{request.StackName}'.",
            DeploymentId = deployResult.DeploymentId,
            StackName = request.StackName,
            StackVersion = deployResult.StackVersion,
            Action = action
        };
    }
}

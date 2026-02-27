using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Hooks.DeployStack;

public record DeployViaHookRequest
{
    public string? StackId { get; init; }
    public required string StackName { get; init; }
    public string? EnvironmentId { get; init; }
    public string? ProductId { get; init; }
    public string? Version { get; init; }
    public string? StackDefinitionName { get; init; }
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
    string? StackId,
    string StackName,
    string EnvironmentId,
    Dictionary<string, string> Variables,
    string? ProductId = null,
    string? Version = null,
    string? StackDefinitionName = null
) : IRequest<DeployViaHookResponse>;

public class DeployViaHookHandler : IRequestHandler<DeployViaHookCommand, DeployViaHookResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly IMediator _mediator;
    private readonly ILogger<DeployViaHookHandler> _logger;

    public DeployViaHookHandler(
        IDeploymentRepository deploymentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        IProductSourceService productSourceService,
        IMediator mediator,
        ILogger<DeployViaHookHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productDeploymentRepository = productDeploymentRepository;
        _productSourceService = productSourceService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<DeployViaHookResponse> Handle(DeployViaHookCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate inputs
        if (string.IsNullOrWhiteSpace(request.StackId) && string.IsNullOrWhiteSpace(request.ProductId))
        {
            return DeployViaHookResponse.Failed("Either StackId or ProductId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StackName))
        {
            return DeployViaHookResponse.Failed("StackName is required.");
        }

        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return DeployViaHookResponse.Failed("Invalid environment ID format.");
        }

        // 1b. Resolve StackId from ProductId if not directly provided
        string resolvedStackId;
        string? resolvedStackDefinitionName = null;
        if (!string.IsNullOrWhiteSpace(request.StackId))
        {
            resolvedStackId = request.StackId;
        }
        else
        {
            var resolveResult = await ResolveStackIdFromProduct(
                request.ProductId!, request.Version, request.StackDefinitionName, cancellationToken);
            if (!resolveResult.Success)
            {
                return DeployViaHookResponse.Failed(resolveResult.Error!);
            }
            resolvedStackId = resolveResult.StackId!;
            resolvedStackDefinitionName = resolveResult.StackDefinitionName;
        }

        var environmentId = new EnvironmentId(envGuid);

        // 1c. If deploying via ProductId, check if stack belongs to an active ProductDeployment
        // and resolve the actual deployment stack name (e.g., "Analytics" → "ams-project-analytics")
        var deployStackName = request.StackName;
        if (!string.IsNullOrWhiteSpace(request.ProductId))
        {
            var productDeployment = _productDeploymentRepository
                .GetActiveByProductGroupId(environmentId, request.ProductId);

            if (productDeployment != null)
            {
                var stackDefName = resolvedStackDefinitionName ?? request.StackDefinitionName;
                if (!string.IsNullOrWhiteSpace(stackDefName))
                {
                    var productStack = productDeployment.Stacks.FirstOrDefault(s =>
                        string.Equals(s.StackName, stackDefName, StringComparison.OrdinalIgnoreCase));

                    if (productStack?.DeploymentStackName != null)
                    {
                        deployStackName = productStack.DeploymentStackName;
                        _logger.LogInformation(
                            "Stack is part of product deployment '{ProductName}', using deployment name '{DeploymentStackName}' instead of '{RequestStackName}'",
                            productDeployment.ProductName, deployStackName, request.StackName);
                    }
                }
            }
        }

        // 2. Check for existing deployment (idempotent behavior)
        var existing = _deploymentRepository.GetByStackName(environmentId, deployStackName);
        string action;
        string stackId;
        Dictionary<string, string> variables;

        if (existing != null)
        {
            // Stack already deployed — redeploy if running, error otherwise
            if (existing.Status != DeploymentStatus.Running)
            {
                return DeployViaHookResponse.Failed(
                    $"Stack '{request.StackName}' exists but is not running (status: {existing.Status}). " +
                    "Only running deployments can be redeployed.");
            }

            var (existingStackId, _, storedVariables) = existing.GetRedeploymentData();
            stackId = existingStackId;
            action = "redeployed";

            // Merge variables: stored deployment values as base, webhook values as overrides
            variables = new Dictionary<string, string>(storedVariables);
            foreach (var kvp in request.Variables)
            {
                variables[kvp.Key] = kvp.Value;
            }

            _logger.LogInformation(
                "Stack '{StackName}' already running in environment {EnvironmentId}, triggering redeploy with {VarCount} variables ({OverrideCount} overrides from webhook)",
                deployStackName, request.EnvironmentId, variables.Count, request.Variables.Count);
        }
        else
        {
            // Fresh deploy - use resolved stack ID
            stackId = resolvedStackId;
            action = "deployed";
            variables = request.Variables;

            _logger.LogInformation(
                "Deploying stack '{StackName}' ({StackId}) to environment {EnvironmentId}",
                deployStackName, resolvedStackId, request.EnvironmentId);
        }

        // 3. Delegate to DeployStackCommand
        var deployResult = await _mediator.Send(new DeployStackCommand(
            request.EnvironmentId,
            stackId,
            deployStackName,
            variables,
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

    private record StackIdResolutionResult(bool Success, string? StackId, string? StackDefinitionName, string? Error);

    private async Task<StackIdResolutionResult> ResolveStackIdFromProduct(
        string productId, string? version, string? stackDefinitionName, CancellationToken ct)
    {
        // 1. Find product by GroupId (metadata.productId)
        var product = await _productSourceService.GetProductAsync(productId, ct);
        if (product == null)
        {
            return new(false, null, null, $"Product '{productId}' not found in catalog.");
        }

        // 2. If specific version requested, find that version
        if (!string.IsNullOrWhiteSpace(version))
        {
            var versions = (await _productSourceService.GetProductVersionsAsync(product.GroupId, ct)).ToList();
            var versionMatch = versions.FirstOrDefault(v =>
                string.Equals(v.ProductVersion, version, StringComparison.OrdinalIgnoreCase));

            if (versionMatch == null)
            {
                var available = string.Join(", ", versions
                    .Where(v => v.ProductVersion != null)
                    .Select(v => v.ProductVersion));
                return new(false, null, null,
                    $"Version '{version}' not found for product '{productId}'. Available versions: {available}");
            }

            product = versionMatch;
        }

        // 3. Resolve stack within the product
        StackDefinition stack;
        if (!string.IsNullOrWhiteSpace(stackDefinitionName))
        {
            var found = product.GetStack(stackDefinitionName);
            if (found == null)
            {
                var availableStacks = string.Join(", ", product.Stacks.Select(s => s.Name));
                return new(false, null, null,
                    $"Stack '{stackDefinitionName}' not found in product '{productId}'. " +
                    $"Available stacks: {availableStacks}");
            }
            stack = found;
        }
        else
        {
            if (product.IsMultiStack)
            {
                var availableStacks = string.Join(", ", product.Stacks.Select(s => s.Name));
                return new(false, null, null,
                    $"Product '{productId}' contains multiple stacks. " +
                    $"Specify 'stackDefinitionName' to select one. Available stacks: {availableStacks}");
            }
            stack = product.DefaultStack;
        }

        _logger.LogInformation(
            "Resolved ProductId '{ProductId}' to StackId '{StackId}'",
            productId, stack.Id.Value);

        return new(true, stack.Id.Value, stack.Name, null);
    }
}

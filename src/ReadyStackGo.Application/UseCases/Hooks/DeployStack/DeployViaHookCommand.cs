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
    public string? EnvironmentName { get; init; }
    public string? ProductId { get; init; }
    public string? Version { get; init; }
    public string? StackDefinitionName { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();

    /// <summary>
    /// When true, only runs the deployment precheck without actually deploying.
    /// Returns the precheck result instead of starting a deployment.
    /// </summary>
    public bool DryRun { get; init; }
}

public record DeployViaHookResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? StackName { get; init; }
    public string? StackVersion { get; init; }
    public string? Action { get; init; }

    /// <summary>
    /// Precheck result (populated when dryRun=true or when precheck found errors).
    /// </summary>
    public PrecheckResultDto? Precheck { get; init; }

    public static DeployViaHookResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record PrecheckResultDto
{
    public bool CanDeploy { get; init; }
    public bool HasErrors { get; init; }
    public bool HasWarnings { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<PrecheckCheckItemDto> Checks { get; init; } = [];
}

public record PrecheckCheckItemDto
{
    public string Rule { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public string? ServiceName { get; init; }
}

public record DeployViaHookCommand(
    string? StackId,
    string StackName,
    string EnvironmentId,
    Dictionary<string, string> Variables,
    string? EnvironmentName = null,
    string? ProductId = null,
    string? Version = null,
    string? StackDefinitionName = null,
    bool DryRun = false
) : IRequest<DeployViaHookResponse>;

public class DeployViaHookHandler : IRequestHandler<DeployViaHookCommand, DeployViaHookResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<DeployViaHookHandler> _logger;

    public DeployViaHookHandler(
        IDeploymentRepository deploymentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        IProductSourceService productSourceService,
        IEnvironmentRepository environmentRepository,
        IMediator mediator,
        ILogger<DeployViaHookHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productDeploymentRepository = productDeploymentRepository;
        _productSourceService = productSourceService;
        _environmentRepository = environmentRepository;
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

        var (resolvedEnvId, envError) = EnvironmentResolver.Resolve(request.EnvironmentId, request.EnvironmentName, _environmentRepository);
        if (resolvedEnvId == null)
        {
            return DeployViaHookResponse.Failed(envError!);
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

        var environmentId = resolvedEnvId;

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
            // Stack already deployed — redeploy if running, failed, or stuck in upgrading
            // Installing and Removed are truly non-redeployable states
            if (existing.Status == DeploymentStatus.Installing || existing.Status == DeploymentStatus.Removed)
            {
                return DeployViaHookResponse.Failed(
                    $"Stack '{request.StackName}' exists but cannot be redeployed (status: {existing.Status}). " +
                    "Only running or failed deployments can be redeployed.");
            }

            var (existingStackId, _, storedVariables) = existing.GetRedeploymentData();
            stackId = existingStackId;
            action = existing.Status == DeploymentStatus.Running ? "redeployed" : "retried";

            // Merge variables: stored deployment values as base, webhook values as overrides
            variables = new Dictionary<string, string>(storedVariables);
            foreach (var kvp in request.Variables)
            {
                variables[kvp.Key] = kvp.Value;
            }

            _logger.LogInformation(
                "Stack '{StackName}' (status: {Status}) in environment {EnvironmentId}, triggering {Action} with {VarCount} variables ({OverrideCount} overrides from webhook)",
                deployStackName, existing.Status, request.EnvironmentId, action, variables.Count, request.Variables.Count);
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

        // 3. Run precheck before deploy
        var envIdString = resolvedEnvId.Value.ToString();
        var precheckResult = await _mediator.Send(new Deployments.Precheck.RunDeploymentPrecheckQuery(
            envIdString,
            stackId,
            deployStackName,
            variables), cancellationToken);

        var precheckDto = MapPrecheckResult(precheckResult);

        // DryRun: return precheck result without deploying
        if (request.DryRun)
        {
            return new DeployViaHookResponse
            {
                Success = precheckResult.CanDeploy,
                Message = precheckResult.Summary,
                StackName = request.StackName,
                Action = "precheck",
                Precheck = precheckDto
            };
        }

        // If precheck has errors, abort deployment
        if (precheckResult.HasErrors)
        {
            _logger.LogWarning(
                "Precheck failed for stack '{StackName}': {Summary}",
                deployStackName, precheckResult.Summary);

            return new DeployViaHookResponse
            {
                Success = false,
                Message = $"Deployment precheck failed: {precheckResult.Summary}",
                StackName = request.StackName,
                Action = "precheck-failed",
                Precheck = precheckDto
            };
        }

        // 4. Delegate to DeployStackCommand
        var deployResult = await _mediator.Send(new DeployStackCommand(
            envIdString,
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

    private static PrecheckResultDto MapPrecheckResult(Domain.Deployment.Precheck.PrecheckResult result) =>
        new()
        {
            CanDeploy = result.CanDeploy,
            HasErrors = result.HasErrors,
            HasWarnings = result.HasWarnings,
            Summary = result.Summary,
            Checks = result.Checks.Select(c => new PrecheckCheckItemDto
            {
                Rule = c.Rule,
                Severity = c.Severity.ToString().ToLowerInvariant(),
                Title = c.Title,
                Detail = c.Detail,
                ServiceName = c.ServiceName
            }).ToList()
        };

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

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.UpgradeStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Hooks.UpgradeViaHook;

public record UpgradeViaHookRequest
{
    public required string StackName { get; init; }
    public required string TargetVersion { get; init; }
    public string? EnvironmentId { get; init; }
    public Dictionary<string, string>? Variables { get; init; }
}

public record UpgradeViaHookResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }

    public static UpgradeViaHookResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record UpgradeViaHookCommand(
    string StackName,
    string TargetVersion,
    string EnvironmentId,
    Dictionary<string, string>? Variables = null
) : IRequest<UpgradeViaHookResponse>;

public class UpgradeViaHookHandler : IRequestHandler<UpgradeViaHookCommand, UpgradeViaHookResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly IMediator _mediator;
    private readonly ILogger<UpgradeViaHookHandler> _logger;

    public UpgradeViaHookHandler(
        IDeploymentRepository deploymentRepository,
        IProductSourceService productSourceService,
        IMediator mediator,
        ILogger<UpgradeViaHookHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productSourceService = productSourceService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<UpgradeViaHookResponse> Handle(UpgradeViaHookCommand request, CancellationToken cancellationToken)
    {
        // 1. Parse environment ID
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return UpgradeViaHookResponse.Failed("Invalid environment ID format.");
        }

        var environmentId = new EnvironmentId(envGuid);

        // 2. Find deployment by stack name + environment
        var deployment = _deploymentRepository.GetByStackName(environmentId, request.StackName);
        if (deployment == null)
        {
            return UpgradeViaHookResponse.Failed(
                $"No deployment found for stack '{request.StackName}' in environment '{request.EnvironmentId}'.");
        }

        // 3. Validate: only running stacks can be upgraded
        if (!deployment.CanUpgrade())
        {
            return UpgradeViaHookResponse.Failed(
                $"Only running deployments can be upgraded. Current status: {deployment.Status}");
        }

        // 4. Parse the stack ID to get the product lookup key
        if (!StackId.TryParse(deployment.StackId, out var parsedStackId) || parsedStackId == null)
        {
            return UpgradeViaHookResponse.Failed(
                "Deployment was not created from catalog. Upgrade via webhook not supported.");
        }

        // 5. Look up the product to get its GroupId
        var productLookupKey = $"{parsedStackId.SourceId}:{parsedStackId.ProductId.Value}";
        var product = await _productSourceService.GetProductAsync(productLookupKey, cancellationToken);
        if (product == null)
        {
            return UpgradeViaHookResponse.Failed(
                $"Product '{productLookupKey}' no longer available in catalog.");
        }

        // 6. Find the target version in the product group
        var versions = (await _productSourceService.GetProductVersionsAsync(
            product.GroupId, cancellationToken)).ToList();

        var targetProduct = versions.FirstOrDefault(v =>
            string.Equals(v.ProductVersion, request.TargetVersion, StringComparison.OrdinalIgnoreCase));

        if (targetProduct == null)
        {
            var available = string.Join(", ", versions
                .Where(v => v.ProductVersion != null)
                .Select(v => v.ProductVersion));
            return UpgradeViaHookResponse.Failed(
                $"Version '{request.TargetVersion}' not found in catalog. Available versions: {available}");
        }

        // 7. Get the target stack ID
        var targetStackId = targetProduct.DefaultStack.Id.Value;

        _logger.LogInformation(
            "Starting upgrade of stack '{StackName}' from {CurrentVersion} to {TargetVersion} via webhook",
            request.StackName, deployment.StackVersion, request.TargetVersion);

        // 8. Delegate to UpgradeStackCommand
        var upgradeResult = await _mediator.Send(new UpgradeStackCommand(
            request.EnvironmentId,
            deployment.Id.Value.ToString(),
            targetStackId,
            request.Variables,
            null), cancellationToken);

        if (!upgradeResult.Success)
        {
            _logger.LogWarning(
                "Upgrade of stack '{StackName}' to {TargetVersion} failed: {Message}",
                request.StackName, request.TargetVersion, upgradeResult.Message);

            return UpgradeViaHookResponse.Failed(upgradeResult.Message ?? "Upgrade failed.");
        }

        _logger.LogInformation(
            "Successfully upgraded stack '{StackName}' from {PreviousVersion} to {NewVersion}",
            request.StackName, upgradeResult.PreviousVersion, upgradeResult.NewVersion);

        return new UpgradeViaHookResponse
        {
            Success = true,
            Message = $"Successfully upgraded '{request.StackName}' from {upgradeResult.PreviousVersion} to {upgradeResult.NewVersion}.",
            DeploymentId = upgradeResult.DeploymentId,
            PreviousVersion = upgradeResult.PreviousVersion,
            NewVersion = upgradeResult.NewVersion
        };
    }
}

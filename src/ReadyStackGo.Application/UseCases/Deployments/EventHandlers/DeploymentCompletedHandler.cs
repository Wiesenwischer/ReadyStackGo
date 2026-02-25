namespace ReadyStackGo.Application.UseCases.Deployments.EventHandlers;

using global::System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

/// <summary>
/// Handles DeploymentCompleted events to synchronize the ProductDeployment aggregate.
/// Ensures cross-aggregate consistency regardless of how a deployment was triggered
/// (UI, API, or CI/CD hook).
/// </summary>
public class DeploymentCompletedHandler
    : INotificationHandler<DomainEventNotification<DeploymentCompleted>>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<DeploymentCompletedHandler> _logger;

    public DeploymentCompletedHandler(
        IDeploymentRepository deploymentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        IProductSourceService productSourceService,
        ILogger<DeploymentCompletedHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productDeploymentRepository = productDeploymentRepository;
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task Handle(
        DomainEventNotification<DeploymentCompleted> notification,
        CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;

        var deployment = _deploymentRepository.Get(evt.DeploymentId);
        if (deployment is null)
        {
            _logger.LogWarning("DeploymentCompleted: Deployment {DeploymentId} not found", evt.DeploymentId);
            return;
        }

        var productId = GetProductIdFromStackId(deployment.StackId);
        if (productId is null)
        {
            _logger.LogDebug("DeploymentCompleted: Could not extract product ID from StackId {StackId}", deployment.StackId);
            return;
        }

        // Resolve product to get the canonical GroupId for consistent lookup.
        // GetProductIdFromStackId returns sourceId:productName from the stackId, but
        // product.GroupId may differ (e.g., explicit productId without source prefix).
        // Using product.GroupId ensures the lookup matches how ProductDeployments are stored.
        var product = await _productSourceService.GetProductAsync(productId, cancellationToken);
        var groupId = product?.GroupId ?? productId;

        var productDeployment = _productDeploymentRepository
            .GetActiveByProductGroupId(deployment.EnvironmentId, groupId);

        if (productDeployment is not null)
        {
            HandleExistingProductDeployment(productDeployment, deployment, evt);
        }
        else
        {
            CreateProductDeployment(deployment, product, evt);
        }
    }

    private void HandleExistingProductDeployment(
        ProductDeployment productDeployment,
        Deployment deployment,
        DeploymentCompleted evt)
    {
        if (productDeployment.IsInProgress)
        {
            _logger.LogDebug(
                "DeploymentCompleted: ProductDeployment {ProductDeploymentId} is {Status}, skipping sync",
                productDeployment.Id, productDeployment.Status);
            return;
        }

        var stackStatus = MapToStackStatus(evt.Status);
        var existingStack = productDeployment.Stacks
            .FirstOrDefault(s => s.DeploymentId == evt.DeploymentId);

        if (existingStack is not null)
        {
            productDeployment.SyncStackHealth(existingStack.StackName, stackStatus, evt.ErrorMessage);
        }
        else
        {
            var stackName = ExtractStackName(deployment.StackId);
            var alreadyTrackedByName = productDeployment.Stacks
                .Any(s => s.StackName.Equals(stackName, StringComparison.OrdinalIgnoreCase));

            if (alreadyTrackedByName)
            {
                productDeployment.SyncStackHealth(stackName, stackStatus, evt.ErrorMessage);
            }
            else
            {
                productDeployment.RegisterExternalStack(
                    stackName,
                    deployment.StackName,
                    deployment.StackId,
                    evt.DeploymentId,
                    deployment.StackName,
                    deployment.Services.Count);

                if (stackStatus == StackDeploymentStatus.Failed)
                {
                    productDeployment.SyncStackHealth(stackName, stackStatus, evt.ErrorMessage);
                }
            }
        }

        productDeployment.RecalculateProductStatus();
        _productDeploymentRepository.Update(productDeployment);
        _productDeploymentRepository.SaveChanges();
    }

    private void CreateProductDeployment(
        Deployment deployment,
        ProductDefinition? product,
        DeploymentCompleted evt)
    {
        if (evt.Status != DeploymentStatus.Running)
        {
            _logger.LogDebug(
                "DeploymentCompleted: Skipping auto-create for non-running status {Status}", evt.Status);
            return;
        }

        if (product is null)
        {
            _logger.LogWarning(
                "DeploymentCompleted: Product not found in catalog for StackId {StackId}, cannot auto-create",
                deployment.StackId);
            return;
        }

        var stackName = ExtractStackName(deployment.StackId);
        var deploymentName = ToKebab(product.Name);

        var productDeployment = ProductDeployment.CreateFromExternalDeployment(
            _productDeploymentRepository.NextIdentity(),
            deployment.EnvironmentId,
            product.GroupId,
            product.Id,
            product.Name,
            product.DisplayName,
            product.ProductVersion ?? "unknown",
            deployment.DeployedBy,
            deploymentName,
            stackName,
            deployment.StackName,
            deployment.StackId,
            evt.DeploymentId,
            deployment.StackName,
            deployment.Services.Count);

        _productDeploymentRepository.Add(productDeployment);
        _productDeploymentRepository.SaveChanges();

        _logger.LogInformation(
            "DeploymentCompleted: Auto-created ProductDeployment {ProductDeploymentId} for product {ProductName}",
            productDeployment.Id, product.Name);
    }

    private static StackDeploymentStatus MapToStackStatus(DeploymentStatus status) => status switch
    {
        DeploymentStatus.Running => StackDeploymentStatus.Running,
        DeploymentStatus.Failed => StackDeploymentStatus.Failed,
        DeploymentStatus.Removed => StackDeploymentStatus.Removed,
        _ => StackDeploymentStatus.Deploying
    };

    private static string? GetProductIdFromStackId(string stackId)
    {
        var parts = stackId.Split(':');
        if (parts.Length >= 2)
        {
            return parts.Length >= 3
                ? $"{parts[0]}:{parts[1]}"
                : stackId;
        }
        return null;
    }

    private static string ExtractStackName(string stackId)
    {
        var parts = stackId.Split(':');
        return parts[^1];
    }

    private static string ToKebab(string input) =>
        Regex.Replace(input.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
}

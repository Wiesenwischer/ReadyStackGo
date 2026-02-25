namespace ReadyStackGo.Application.UseCases.Deployments.EventHandlers;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Handles DeploymentRemoved events to synchronize the ProductDeployment aggregate.
/// When a stack is removed externally, the corresponding ProductDeployment stack
/// status is updated to Removed and the product status is recalculated.
/// </summary>
public class DeploymentRemovedHandler
    : INotificationHandler<DomainEventNotification<DeploymentRemoved>>
{
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly ILogger<DeploymentRemovedHandler> _logger;

    public DeploymentRemovedHandler(
        IProductDeploymentRepository productDeploymentRepository,
        ILogger<DeploymentRemovedHandler> logger)
    {
        _productDeploymentRepository = productDeploymentRepository;
        _logger = logger;
    }

    public Task Handle(
        DomainEventNotification<DeploymentRemoved> notification,
        CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;

        var productDeployment = _productDeploymentRepository.GetByStackDeploymentId(evt.DeploymentId);
        if (productDeployment is null)
        {
            _logger.LogDebug("DeploymentRemoved: No ProductDeployment found for DeploymentId {DeploymentId}",
                evt.DeploymentId);
            return Task.CompletedTask;
        }

        if (productDeployment.IsInProgress)
        {
            _logger.LogDebug(
                "DeploymentRemoved: ProductDeployment {ProductDeploymentId} is {Status}, skipping sync",
                productDeployment.Id, productDeployment.Status);
            return Task.CompletedTask;
        }

        var stack = productDeployment.Stacks.FirstOrDefault(s => s.DeploymentId == evt.DeploymentId);
        if (stack is null)
        {
            return Task.CompletedTask;
        }

        productDeployment.SyncStackHealth(stack.StackName, StackDeploymentStatus.Removed);
        productDeployment.RecalculateProductStatus();
        _productDeploymentRepository.Update(productDeployment);
        _productDeploymentRepository.SaveChanges();

        _logger.LogInformation(
            "DeploymentRemoved: Synced stack '{StackName}' removal to ProductDeployment {ProductDeploymentId}",
            stack.StackName, productDeployment.Id);

        return Task.CompletedTask;
    }
}

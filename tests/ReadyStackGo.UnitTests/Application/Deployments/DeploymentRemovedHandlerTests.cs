using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.EventHandlers;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class DeploymentRemovedHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _productDeploymentRepoMock;
    private readonly DeploymentRemovedHandler _handler;

    private static readonly EnvironmentId TestEnvId = EnvironmentId.NewId();

    public DeploymentRemovedHandlerTests()
    {
        _productDeploymentRepoMock = new Mock<IProductDeploymentRepository>();

        _handler = new DeploymentRemovedHandler(
            _productDeploymentRepoMock.Object,
            new Mock<ILogger<DeploymentRemovedHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ExistingProductDeployment_SyncsRemovedStatus()
    {
        var deploymentId = DeploymentId.NewId();
        var productDeployment = CreateRunningProductDeployment(deploymentId, "web");

        _productDeploymentRepoMock
            .Setup(r => r.GetByStackDeploymentId(deploymentId))
            .Returns(productDeployment);

        var evt = new DeploymentRemoved(deploymentId);
        await HandleEvent(evt);

        var webStack = productDeployment.Stacks.First(s => s.StackName == "web");
        webStack.Status.Should().Be(StackDeploymentStatus.Removed);
        _productDeploymentRepoMock.Verify(r => r.Update(productDeployment), Times.Once);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_NoProductDeployment_DoesNothing()
    {
        var deploymentId = DeploymentId.NewId();

        _productDeploymentRepoMock
            .Setup(r => r.GetByStackDeploymentId(deploymentId))
            .Returns((ProductDeployment?)null);

        var evt = new DeploymentRemoved(deploymentId);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    [Fact]
    public async Task Handle_ProductDeploymentIsRemoving_SkipsSync()
    {
        var deploymentId = DeploymentId.NewId();
        var productDeployment = CreateRunningProductDeployment(deploymentId, "web");
        productDeployment.StartRemoval();

        _productDeploymentRepoMock
            .Setup(r => r.GetByStackDeploymentId(deploymentId))
            .Returns(productDeployment);

        var evt = new DeploymentRemoved(deploymentId);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    [Fact]
    public async Task Handle_TwoStackProduct_OneRemoved_RecalculatesStatus()
    {
        var webDeploymentId = DeploymentId.NewId();
        var dbDeploymentId = DeploymentId.NewId();

        var productDeployment = ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), TestEnvId,
            "src:myproduct", "src:myproduct:1.0.0",
            "myproduct", "My Product", "1.0.0", UserId.NewId(),
            "test-deployment",
            "web", "Web", "src:myproduct:web",
            webDeploymentId, "product-web", 2);
        productDeployment.RegisterExternalStack("db", "Database", "src:myproduct:db",
            dbDeploymentId, "product-db", 1);

        _productDeploymentRepoMock
            .Setup(r => r.GetByStackDeploymentId(webDeploymentId))
            .Returns(productDeployment);

        var evt = new DeploymentRemoved(webDeploymentId);
        await HandleEvent(evt);

        var webStack = productDeployment.Stacks.First(s => s.StackName == "web");
        webStack.Status.Should().Be(StackDeploymentStatus.Removed);
        _productDeploymentRepoMock.Verify(r => r.Update(productDeployment), Times.Once);
    }

    #region Helpers

    private Task HandleEvent(DeploymentRemoved evt)
    {
        return _handler.Handle(
            new DomainEventNotification<DeploymentRemoved>(evt),
            CancellationToken.None);
    }

    private static ProductDeployment CreateRunningProductDeployment(
        DeploymentId deploymentId, string stackName)
    {
        return ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), TestEnvId,
            "src:myproduct", "src:myproduct:1.0.0",
            "myproduct", "My Product", "1.0.0", UserId.NewId(),
            "test-deployment",
            stackName, stackName, $"src:myproduct:{stackName}",
            deploymentId, $"product-{stackName}", 2);
    }

    #endregion
}

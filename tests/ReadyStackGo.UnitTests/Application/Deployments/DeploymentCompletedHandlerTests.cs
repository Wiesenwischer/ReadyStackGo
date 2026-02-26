using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.EventHandlers;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class DeploymentCompletedHandlerTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IProductDeploymentRepository> _productDeploymentRepoMock;
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly DeploymentCompletedHandler _handler;

    private static readonly EnvironmentId TestEnvId = EnvironmentId.NewId();

    public DeploymentCompletedHandlerTests()
    {
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _productDeploymentRepoMock = new Mock<IProductDeploymentRepository>();
        _productSourceMock = new Mock<IProductSourceService>();

        _productDeploymentRepoMock
            .Setup(r => r.NextIdentity())
            .Returns(ProductDeploymentId.NewId());

        _handler = new DeploymentCompletedHandler(
            _deploymentRepoMock.Object,
            _productDeploymentRepoMock.Object,
            _productSourceMock.Object,
            new Mock<ILogger<DeploymentCompletedHandler>>().Object);
    }

    #region Sync Existing ProductDeployment

    [Fact]
    public async Task Handle_ExistingProductDeployment_SyncsStackStatus()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        var productDeployment = CreateRunningProductDeployment(deploymentId, "web");

        SetupDeployment(deployment);
        SetupProductDeployment("src:myproduct", productDeployment);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Failed, "Container OOM");
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(productDeployment), Times.Once);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingProductDeployment_StackNotTracked_RegistersExternalStack()
    {
        var existingDeploymentId = DeploymentId.NewId();
        var newDeploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(newDeploymentId, "src:myproduct:db");
        deployment.MarkAsRunning();

        // Product deployment only tracks "web" stack
        var productDeployment = CreateRunningProductDeployment(existingDeploymentId, "web");

        SetupDeployment(deployment);
        SetupProductDeployment("src:myproduct", productDeployment);

        var evt = new DeploymentCompleted(newDeploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        productDeployment.TotalStacks.Should().Be(2);
        productDeployment.Stacks.Should().Contain(s => s.StackName == "db");
        _productDeploymentRepoMock.Verify(r => r.Update(productDeployment), Times.Once);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductDeploymentIsDeploying_SkipsSync()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        var productDeployment = CreateDeployingProductDeployment();

        SetupDeployment(deployment);
        SetupProductDeployment("src:myproduct", productDeployment);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    [Fact]
    public async Task Handle_ProductDeploymentIsUpgrading_SkipsSync()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        var existing = CreateRunningProductDeployment(deploymentId, "web");
        var upgrading = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), TestEnvId,
            "src:myproduct", "src:myproduct:2.0.0",
            "myproduct", "My Product", "2.0.0",
            UserId.NewId(), "test-deployment", existing,
            new List<StackDeploymentConfig>
            {
                new("web", "Web", "src:myproduct:web", 2, new Dictionary<string, string>())
            },
            new Dictionary<string, string>());

        SetupDeployment(deployment);
        SetupProductDeployment("src:myproduct", upgrading);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeploymentCompletedWithFailed_SyncsFailedStatus()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        var productDeployment = CreateRunningProductDeployment(deploymentId, "web");

        SetupDeployment(deployment);
        SetupProductDeployment("src:myproduct", productDeployment);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Failed, "OOM killed");
        await HandleEvent(evt);

        var webStack = productDeployment.Stacks.First(s => s.StackName == "web");
        webStack.Status.Should().Be(StackDeploymentStatus.Failed);
        _productDeploymentRepoMock.Verify(r => r.Update(productDeployment), Times.Once);
    }

    #endregion

    #region Auto-Create ProductDeployment

    [Fact]
    public async Task Handle_NoProductDeployment_RunningStatus_AutoCreates()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        var product = CreateTestProductDefinition();

        SetupDeployment(deployment);
        SetupNoProductDeployment("src:myproduct");
        _productSourceMock
            .Setup(s => s.GetProductAsync("src:myproduct", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(
            r => r.Add(It.Is<ProductDeployment>(pd =>
                pd.Status == ProductDeploymentStatus.Running &&
                pd.ProductGroupId == "src:myproduct" &&
                pd.TotalStacks == 1)),
            Times.Once);
        _productDeploymentRepoMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_NoProductDeployment_FailedStatus_DoesNotAutoCreate()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsFailed("Install failed");

        SetupDeployment(deployment);
        SetupNoProductDeployment("src:myproduct");

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Failed, "Install failed");
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoProductDeployment_ProductNotInCatalog_DoesNotAutoCreate()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        SetupDeployment(deployment);
        SetupNoProductDeployment("src:myproduct");
        _productSourceMock
            .Setup(s => s.GetProductAsync("src:myproduct", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_DeploymentNotFound_DoesNothing()
    {
        var deploymentId = DeploymentId.NewId();
        _deploymentRepoMock.Setup(r => r.Get(deploymentId)).Returns((Deployment?)null);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidStackIdFormat_DoesNothing()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "single-part-id");
        deployment.MarkAsRunning();

        SetupDeployment(deployment);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Update(It.IsAny<ProductDeployment>()), Times.Never);
        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TwoPartStackId_ExtractsProductId()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:singlestack");
        deployment.MarkAsRunning();

        var product = CreateTestProductDefinition("singlestack", "src");

        SetupDeployment(deployment);
        SetupNoProductDeployment("src:singlestack");
        _productSourceMock
            .Setup(s => s.GetProductAsync("src:singlestack", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StackAlreadyTrackedByName_SyncsInstead()
    {
        var oldDeploymentId = DeploymentId.NewId();
        var newDeploymentId = DeploymentId.NewId();

        // ProductDeployment tracks "web" with old deployment ID
        var productDeployment = CreateRunningProductDeployment(oldDeploymentId, "web");

        // New deployment has different ID but same stack name in StackId
        var deployment = CreateTestDeployment(newDeploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        SetupDeployment(deployment);
        SetupProductDeployment("src:myproduct", productDeployment);

        var evt = new DeploymentCompleted(newDeploymentId, DeploymentStatus.Failed, "Crashed");
        await HandleEvent(evt);

        // Should sync by name, not try to register new stack
        _productDeploymentRepoMock.Verify(r => r.Update(productDeployment), Times.Once);
        productDeployment.TotalStacks.Should().Be(1); // No new stack added
    }

    #endregion

    #region GroupId Resolution

    [Fact]
    public async Task Handle_ExplicitProductId_UsesGroupIdForLookup()
    {
        // Product has an explicit productId that differs from sourceId:productName
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:io.company.myproduct:web");
        deployment.MarkAsRunning();

        var product = CreateTestProductDefinition("myproduct", "src", explicitProductId: "io.company.myproduct");

        // The existing ProductDeployment was created with product.GroupId = "io.company.myproduct"
        var existingPd = ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), TestEnvId,
            "io.company.myproduct", "src:io.company.myproduct:1.0.0",
            "myproduct", "myproduct", "1.0.0", UserId.NewId(),
            "test-deployment",
            "api", "api", "src:io.company.myproduct:api",
            DeploymentId.NewId(), "product-api", 2);

        SetupDeployment(deployment);
        _productSourceMock
            .Setup(s => s.GetProductAsync("src:io.company.myproduct", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        // The lookup must use product.GroupId ("io.company.myproduct"), NOT "src:io.company.myproduct"
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), "io.company.myproduct"))
            .Returns(existingPd);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        // Should register as external stack on the existing PD, not create a new one
        existingPd.TotalStacks.Should().Be(2);
        existingPd.Stacks.Should().Contain(s => s.StackName == "web");
        _productDeploymentRepoMock.Verify(r => r.Update(existingPd), Times.Once);
        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExplicitProductId_AutoCreateUsesGroupId()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:io.company.myproduct:web");
        deployment.MarkAsRunning();

        var product = CreateTestProductDefinition("myproduct", "src", explicitProductId: "io.company.myproduct");

        SetupDeployment(deployment);
        _productSourceMock
            .Setup(s => s.GetProductAsync("src:io.company.myproduct", It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), "io.company.myproduct"))
            .Returns((ProductDeployment?)null);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        // Auto-created PD should use product.GroupId
        _productDeploymentRepoMock.Verify(
            r => r.Add(It.Is<ProductDeployment>(pd =>
                pd.ProductGroupId == "io.company.myproduct")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ProductNotInCatalog_FallsBackToStackIdDerivedProductId()
    {
        var deploymentId = DeploymentId.NewId();
        var deployment = CreateTestDeployment(deploymentId, "src:myproduct:web");
        deployment.MarkAsRunning();

        SetupDeployment(deployment);
        // Product not in catalog
        _productSourceMock
            .Setup(s => s.GetProductAsync("src:myproduct", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        // Falls back to stackId-derived "src:myproduct"
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), "src:myproduct"))
            .Returns((ProductDeployment?)null);

        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);
        await HandleEvent(evt);

        // No product → no auto-create
        _productDeploymentRepoMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Never);
    }

    #endregion

    #region Helpers

    private Task HandleEvent(DeploymentCompleted evt)
    {
        return _handler.Handle(
            new DomainEventNotification<DeploymentCompleted>(evt),
            CancellationToken.None);
    }

    private void SetupDeployment(Deployment deployment)
    {
        _deploymentRepoMock.Setup(r => r.Get(deployment.Id)).Returns(deployment);
    }

    private void SetupProductDeployment(string productGroupId, ProductDeployment pd)
    {
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), productGroupId))
            .Returns(pd);
    }

    private void SetupNoProductDeployment(string productGroupId)
    {
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), productGroupId))
            .Returns((ProductDeployment?)null);
    }

    private static Deployment CreateTestDeployment(DeploymentId id, string stackId)
    {
        var deployment = Deployment.StartInstallation(
            id, TestEnvId, stackId, "test-stack", "test-project", UserId.NewId());
        return deployment;
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

    private static ProductDeployment CreateDeployingProductDeployment()
    {
        return ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), TestEnvId,
            "src:myproduct", "src:myproduct:1.0.0",
            "myproduct", "My Product", "1.0.0", UserId.NewId(),
            "test-deployment",
            new List<StackDeploymentConfig>
            {
                new("web", "Web", "src:myproduct:web", 2, new Dictionary<string, string>())
            },
            new Dictionary<string, string>());
    }

    private static ProductDefinition CreateTestProductDefinition(
        string name = "myproduct", string sourceId = "src", string? explicitProductId = null)
    {
        var productId = ProductId.FromName(explicitProductId ?? name);
        var stack = new StackDefinition(
            sourceId, "web", productId,
            productName: name, productVersion: "1.0.0");

        return new ProductDefinition(
            sourceId, name, name,
            new List<StackDefinition> { stack },
            productVersion: "1.0.0",
            productId: explicitProductId);
    }

    #endregion
}

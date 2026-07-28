using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Application.Maintenance;

/// <summary>
/// Unit tests for MaintenanceObserverService — when RSGO is allowed to read a product's database.
///
/// The rule that matters operationally: RSGO must not connect to a product it is not able to act on.
/// A product maintenance routine that waits for every session to close before taking its database
/// exclusively would otherwise wait on RSGO indefinitely.
/// </summary>
public class MaintenanceObserverServiceTests
{
    private readonly Mock<IMaintenanceObserverFactory> _factory = new();
    private readonly Mock<IMaintenanceObserver> _observer = new();
    private readonly Mock<IProductDeploymentRepository> _productRepository = new();
    private readonly Mock<IHealthSnapshotRepository> _healthSnapshots = new();
    private readonly Mock<IHealthNotificationService> _notifications = new();
    private readonly Mock<ISender> _mediator = new();
    private readonly MaintenanceObserverStateStore _store = new();

    private readonly ProductDeploymentId _productDeploymentId = new(Guid.NewGuid());
    private readonly EnvironmentId _environmentId = new(Guid.NewGuid());

    public MaintenanceObserverServiceTests()
    {
        _observer.SetupGet(o => o.Type).Returns(ObserverType.SqlExtendedProperty);
        _observer
            .Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("0"));

        _factory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>())).Returns(_observer.Object);

        _notifications
            .Setup(n => n.NotifyObserverResultAsync(
                It.IsAny<DeploymentId>(), It.IsAny<string>(), It.IsAny<ObserverResultDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mediator
            .Setup(m => m.Send(It.IsAny<ChangeProductOperationModeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChangeProductOperationModeResponse.Ok(
                _productDeploymentId.Value.ToString(), OperationMode.Normal.Name, OperationMode.Maintenance.Name));
    }

    private MaintenanceObserverService CreateService() => new(
        _factory.Object,
        _store,
        _productRepository.Object,
        _healthSnapshots.Object,
        _notifications.Object,
        _mediator.Object,
        new Mock<ILogger<MaintenanceObserverService>>().Object);

    private static MaintenanceObserverConfig ObserverConfig(int intervalSeconds = 30)
        => MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(intervalSeconds),
            maintenanceValue: "1",
            normalValue: "0",
            SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "Server=sql01;Database=AppDb"));

    private ProductDeployment CreateProductDeployment(bool running = true)
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("api", "API Server", "source:product:api:1.0", 1, new Dictionary<string, string>())
        };

        var pd = ProductDeployment.InitiateDeployment(
            _productDeploymentId,
            _environmentId,
            "product-group",
            "product-id",
            "test-product",
            "Test Product",
            "1.0.0",
            UserId.NewId(),
            "test-deploy",
            stackConfigs,
            new Dictionary<string, string>());

        if (running)
        {
            pd.StartStack("api", new DeploymentId(Guid.NewGuid()));
            pd.CompleteStack("api");
        }

        _productRepository.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(pd);
        return pd;
    }

    #region Manual maintenance

    [Fact]
    public async Task CheckProductObserverAsync_ManualMaintenance_DoesNotTouchTheProduct()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig());
        pd.EnterMaintenance(MaintenanceTrigger.Manual("operator is working on the database"));

        var result = await CreateService().CheckProductObserverAsync(_productDeploymentId);

        result.Should().BeNull();
        _factory.Verify(f => f.Create(It.IsAny<MaintenanceObserverConfig>()), Times.Never);
        _observer.Verify(o => o.CheckAsync(It.IsAny<CancellationToken>()), Times.Never,
            "the observer cannot end manually activated maintenance, so reading the database would " +
            "only keep a session open that the product may be waiting on");
    }

    [Fact]
    public async Task CheckProductObserverAsync_ManualMaintenance_SendsNoModeChange()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig());
        pd.EnterMaintenance(MaintenanceTrigger.Manual("scheduled window"));

        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        _mediator.Verify(
            m => m.Send(It.IsAny<ChangeProductOperationModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Observer-triggered maintenance

    [Fact]
    public async Task CheckProductObserverAsync_ObserverTriggeredMaintenance_KeepsChecking()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig());
        pd.EnterMaintenance(MaintenanceTrigger.Observer("flag set by the product"));

        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        _observer.Verify(o => o.CheckAsync(It.IsAny<CancellationToken>()), Times.Once,
            "the observer owns this maintenance and is the only way out of it");
    }

    [Fact]
    public async Task CheckProductObserverAsync_ObserverTriggeredMaintenance_ClearedFlagExitsMaintenance()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig());
        pd.EnterMaintenance(MaintenanceTrigger.Observer("flag set by the product"));

        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        _mediator.Verify(
            m => m.Send(
                It.Is<ChangeProductOperationModeCommand>(c => c.NewMode == OperationMode.Normal.Name),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckProductObserverAsync_FlagSetWhileNormal_EntersMaintenance()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig());
        _observer
            .Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.MaintenanceRequired("1"));

        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        _mediator.Verify(
            m => m.Send(
                It.Is<ChangeProductOperationModeCommand>(c =>
                    c.NewMode == OperationMode.Maintenance.Name && c.Source == "Observer"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckProductObserverAsync_ExclusiveDatabaseWhileInMaintenance_StaysInMaintenance()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig());
        pd.EnterMaintenance(MaintenanceTrigger.Observer("flag set by the product"));
        _observer
            .Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.MaintenanceRequired("database-exclusive (ONLINE/SINGLE_USER)"));

        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        _mediator.Verify(
            m => m.Send(It.IsAny<ChangeProductOperationModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "the product is still working on its database — nothing to change");
    }

    #endregion

    #region Polling interval across scopes

    [Fact]
    public async Task CheckProductObserverAsync_WithinInterval_DoesNotRecheckInALaterScope()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig(intervalSeconds: 3600));

        // Two service instances, one shared store — this is what the background service does every
        // cycle. State on the service itself would make every cycle look like a first check.
        await CreateService().CheckProductObserverAsync(_productDeploymentId);
        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        _observer.Verify(o => o.CheckAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckProductObserverAsync_ResultSurvivesTheScope()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig(intervalSeconds: 3600));

        await CreateService().CheckProductObserverAsync(_productDeploymentId);
        var lastResult = await CreateService().GetLastResultAsync(_productDeploymentId);

        lastResult.Should().NotBeNull("the observer status endpoint reads this in a different scope");
        lastResult!.ObservedValue.Should().Be("0");
    }

    #endregion

    #region Nothing to observe

    [Fact]
    public async Task CheckProductObserverAsync_NoObserverConfigured_DoesNothing()
    {
        CreateProductDeployment();

        var result = await CreateService().CheckProductObserverAsync(_productDeploymentId);

        result.Should().BeNull();
        _factory.Verify(f => f.Create(It.IsAny<MaintenanceObserverConfig>()), Times.Never);
    }

    [Fact]
    public async Task CheckProductObserverAsync_NotOperational_DoesNothing()
    {
        var pd = CreateProductDeployment(running: false);
        pd.SetMaintenanceObserverConfig(ObserverConfig());

        var result = await CreateService().CheckProductObserverAsync(_productDeploymentId);

        result.Should().BeNull();
        _observer.Verify(o => o.CheckAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckProductObserverAsync_UnknownProduct_ForgetsItsState()
    {
        var pd = CreateProductDeployment();
        pd.SetMaintenanceObserverConfig(ObserverConfig(intervalSeconds: 3600));
        await CreateService().CheckProductObserverAsync(_productDeploymentId);

        // The deployment disappears (removed) — the cached observer must go with it.
        _productRepository.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns((ProductDeployment?)null);
        var result = await CreateService().CheckProductObserverAsync(_productDeploymentId);

        result.Should().BeNull();
        _store.GetConfig(_productDeploymentId).Should().BeNull();
        _store.GetLastResult(_productDeploymentId).Should().BeNull();
    }

    #endregion
}

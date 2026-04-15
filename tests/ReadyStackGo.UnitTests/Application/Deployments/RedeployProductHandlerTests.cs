using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class RedeployProductHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IProductSourceService> _productSourceServiceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IDeploymentService> _deploymentServiceMock;
    private readonly Mock<ILogger<RedeployProductHandler>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RedeployProductHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public RedeployProductHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _productSourceServiceMock = new Mock<IProductSourceService>();
        _mediatorMock = new Mock<IMediator>();
        _deploymentServiceMock = new Mock<IDeploymentService>();
        _loggerMock = new Mock<ILogger<RedeployProductHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        // Default: removal succeeds
        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = true });

        // Default: deploy succeeds with a valid deployment ID
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse { Success = true, DeploymentId = Guid.NewGuid().ToString() });

        _handler = new RedeployProductHandler(
            _repositoryMock.Object,
            _productSourceServiceMock.Object,
            _mediatorMock.Object,
            _deploymentServiceMock.Object,
            _loggerMock.Object,
            timeProvider: _timeProvider);
    }

    #region Helpers

    private static ProductDeployment CreateRunningDeployment(int stackCount = 2, string deploymentName = "test-deploy")
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), deploymentName,
            CreateStackConfigs(stackCount),
            new Dictionary<string, string>());

        for (var i = 0; i < stackCount; i++)
        {
            pd.StartStack($"stack-{i}", DeploymentId.NewId());
            pd.CompleteStack($"stack-{i}");
        }

        return pd;
    }

    private static List<StackDeploymentConfig> CreateStackConfigs(int count)
    {
        var configs = new List<StackDeploymentConfig>();
        for (var i = 0; i < count; i++)
        {
            configs.Add(new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"sid:{i}", 2,
                new Dictionary<string, string> { { $"VAR_{i}", $"value_{i}" } }));
        }
        return configs;
    }

    private RedeployProductCommand CreateCommand(
        string productDeploymentId,
        List<string>? stackNames = null,
        bool continueOnError = false)
    {
        return new RedeployProductCommand(
            TestEnvironmentId,
            productDeploymentId,
            stackNames,
            null,
            null,
            continueOnError);
    }

    private void SetupDeploymentFound(ProductDeployment pd)
    {
        _repositoryMock
            .Setup(r => r.Get(It.IsAny<ProductDeploymentId>()))
            .Returns(pd);
    }

    #endregion

    #region Input Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFailed()
    {
        var command = CreateCommand("not-a-guid");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid product deployment ID");
    }

    [Fact]
    public async Task Handle_ProductDeploymentNotFound_ReturnsFailed()
    {
        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns((ProductDeployment?)null);
        var command = CreateCommand(Guid.NewGuid().ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeploymentCannotRedeploy_ReturnsFailed()
    {
        // A deployment in Deploying state cannot be redeployed
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), "test-deploy",
            CreateStackConfigs(1),
            new Dictionary<string, string>());
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot be redeployed");
    }

    #endregion

    #region Remove Before Redeploy

    [Fact]
    public async Task Handle_AllStacksRedeploy_CallsRemoveForEachStack()
    {
        var pd = CreateRunningDeployment(2);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        await _handler.Handle(command, CancellationToken.None);

        _deploymentServiceMock.Verify(
            d => d.RemoveDeploymentAsync(TestEnvironmentId, It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_AllStacksRedeploy_CallsRemoveBeforeDeploy()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var callOrder = new List<string>();
        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, _) => callOrder.Add("remove"))
            .ReturnsAsync(new DeployComposeResponse { Success = true });
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<DeployStackResponse>, CancellationToken>((_, _) => callOrder.Add("deploy"))
            .ReturnsAsync(new DeployStackResponse { Success = true, DeploymentId = Guid.NewGuid().ToString() });

        await _handler.Handle(command, CancellationToken.None);

        callOrder.Should().Equal("remove", "deploy");
    }

    [Fact]
    public async Task Handle_RemoveFails_FallsBackToMarkAsRemoved()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = false, Message = "Docker error" });

        await _handler.Handle(command, CancellationToken.None);

        _deploymentServiceMock.Verify(
            d => d.MarkDeploymentAsRemovedAsync(TestEnvironmentId, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RemoveFails_ContinuesWithDeploy()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = false, Message = "Docker error" });

        var result = await _handler.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SelectiveRedeploy_OnlyRemovesTargetedStacks()
    {
        var pd = CreateRunningDeployment(2);
        SetupDeploymentFound(pd);
        // Only redeploy stack-0; stack-1 stays Running
        var command = CreateCommand(pd.Id.Value.ToString(), stackNames: new List<string> { "stack-0" });

        await _handler.Handle(command, CancellationToken.None);

        // Remove called once (only for stack-0)
        _deploymentServiceMock.Verify(
            d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        // Deploy called once (only for stack-0)
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Success / Failure Outcomes

    [Fact]
    public async Task Handle_AllStacksSucceed_ReturnsSuccessWithRunningStatus()
    {
        var pd = CreateRunningDeployment(2);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ProductDeploymentStatus.Running.ToString());
        result.StackResults.Should().HaveCount(2);
        result.StackResults.Should().AllSatisfy(s => s.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_DeployFails_ContinueOnErrorFalse_AbortsRemainingStacks()
    {
        var pd = CreateRunningDeployment(3);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString(), continueOnError: false);

        var callCount = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new DeployStackResponse { Success = false, Message = "First stack failed" }
                    : new DeployStackResponse { Success = true, DeploymentId = Guid.NewGuid().ToString() };
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeployFails_ContinueOnErrorTrue_DeploysAllStacks()
    {
        var pd = CreateRunningDeployment(3);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString(), continueOnError: true);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse { Success = false, Message = "Stack failed" });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ReturnsSessionId_WhenProvidedInCommand()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = new RedeployProductCommand(
            TestEnvironmentId,
            pd.Id.Value.ToString(),
            null, null,
            SessionId: "my-session",
            ContinueOnError: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.SessionId.Should().Be("my-session");
    }

    [Fact]
    public async Task Handle_GeneratesSessionId_WhenNotProvided()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.SessionId.Should().StartWith("product-redeploy-");
    }

    #endregion

    #region Maintenance Observer Refresh

    [Fact]
    public async Task Handle_ReResolvesMaintenanceObserverFromCatalog()
    {
        var pd = CreateRunningDeploymentWithSharedVars(new Dictionary<string, string>
        {
            ["DB_SERVER"] = "sqldev2017",
            ["DB_NAME"] = "dev-amsproject"
        });
        SetupDeploymentFound(pd);

        _productSourceServiceMock
            .Setup(s => s.GetProductAsync(pd.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCatalogProductWithObserver(pd.ProductId));

        var command = CreateCommand(pd.Id.Value.ToString());
        await _handler.Handle(command, CancellationToken.None);

        pd.MaintenanceObserverConfig.Should().NotBeNull(
            "redeploy must re-read maintenance observer from the catalog so stack.yaml edits take effect");
        pd.MaintenanceObserverConfig!.Type.Value.Should().Be("sqlExtendedProperty");
    }

    [Fact]
    public async Task Handle_ObserverRemovedFromCatalog_ClearsConfig()
    {
        var pd = CreateRunningDeploymentWithSharedVars(new Dictionary<string, string>());
        // Pre-seed an existing observer — simulating the old catalog state.
        pd.SetMaintenanceObserverConfig(global::ReadyStackGo.Domain.Deployment.Observers.MaintenanceObserverConfig.Create(
            global::ReadyStackGo.Domain.Deployment.Observers.ObserverType.File,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            global::ReadyStackGo.Domain.Deployment.Observers.FileObserverSettings.ForExistence("/tmp/x")));
        SetupDeploymentFound(pd);

        // Catalog returns product without observer.
        _productSourceServiceMock
            .Setup(s => s.GetProductAsync(pd.ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCatalogProductWithoutObserver(pd.ProductId));

        var command = CreateCommand(pd.Id.Value.ToString());
        await _handler.Handle(command, CancellationToken.None);

        pd.MaintenanceObserverConfig.Should().BeNull(
            "observer removed from the catalog must also be removed from the ProductDeployment");
    }

    private static ProductDeployment CreateRunningDeploymentWithSharedVars(Dictionary<string, string> sharedVars)
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), "test-deploy",
            CreateStackConfigs(1),
            sharedVars);

        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        return pd;
    }

    private static global::ReadyStackGo.Domain.StackManagement.Stacks.ProductDefinition CreateCatalogProductWithObserver(string productId)
    {
        var stack = new global::ReadyStackGo.Domain.StackManagement.Stacks.StackDefinition(
            "stacks",
            "stack-0",
            new global::ReadyStackGo.Domain.StackManagement.Stacks.ProductId(productId),
            services: new[]
            {
                new global::ReadyStackGo.Domain.StackManagement.Stacks.ServiceTemplate
                {
                    Name = "svc", Image = "test:latest"
                }
            },
            variables: Array.Empty<global::ReadyStackGo.Domain.StackManagement.Stacks.Variable>(),
            productName: "testproduct",
            productDisplayName: "Test Product",
            productVersion: "1.0.0");

        return new global::ReadyStackGo.Domain.StackManagement.Stacks.ProductDefinition(
            sourceId: "stacks",
            name: "testproduct",
            displayName: "Test Product",
            stacks: new[] { stack },
            productVersion: "1.0.0",
            maintenanceObserver: new global::ReadyStackGo.Domain.StackManagement.Manifests.RsgoMaintenanceObserver
            {
                Type = "sqlExtendedProperty",
                PropertyName = "ams-MaintenanceMode",
                ConnectionString = "Server=${DB_SERVER};Database=${DB_NAME};",
                MaintenanceValue = "1",
                NormalValue = "0",
                PollingInterval = "30s"
            },
            productId: productId);
    }

    private static global::ReadyStackGo.Domain.StackManagement.Stacks.ProductDefinition CreateCatalogProductWithoutObserver(string productId)
    {
        var stack = new global::ReadyStackGo.Domain.StackManagement.Stacks.StackDefinition(
            "stacks",
            "stack-0",
            new global::ReadyStackGo.Domain.StackManagement.Stacks.ProductId(productId),
            services: new[]
            {
                new global::ReadyStackGo.Domain.StackManagement.Stacks.ServiceTemplate
                {
                    Name = "svc", Image = "test:latest"
                }
            },
            variables: Array.Empty<global::ReadyStackGo.Domain.StackManagement.Stacks.Variable>(),
            productName: "testproduct",
            productDisplayName: "Test Product",
            productVersion: "1.0.0");

        return new global::ReadyStackGo.Domain.StackManagement.Stacks.ProductDefinition(
            sourceId: "stacks",
            name: "testproduct",
            displayName: "Test Product",
            stacks: new[] { stack },
            productVersion: "1.0.0",
            productId: productId);
    }

    #endregion
}

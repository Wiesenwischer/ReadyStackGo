using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class ChangeProductOperationModeHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IHealthNotificationService> _healthNotificationMock;
    private readonly Mock<ILogger<ChangeProductOperationModeHandler>> _loggerMock;
    private readonly ChangeProductOperationModeHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public ChangeProductOperationModeHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _dockerServiceMock = new Mock<IDockerService>();
        _healthNotificationMock = new Mock<IHealthNotificationService>();
        _loggerMock = new Mock<ILogger<ChangeProductOperationModeHandler>>();

        _handler = new ChangeProductOperationModeHandler(
            _repositoryMock.Object,
            _dockerServiceMock.Object,
            _healthNotificationMock.Object,
            _loggerMock.Object);
    }

    #region Test Helpers

    private static ProductDeployment CreateRunningDeployment(
        int stackCount = 2, string name = "test-product")
    {
        var stackConfigs = Enumerable.Range(0, stackCount).Select(i =>
            new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"source:product:stack-{i}:1.0",
                i + 1, new Dictionary<string, string>()))
            .ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "product-group", "product-id",
            name, "Test Product", "1.0.0",
            UserId.Create(),
            "test-deploy",
            stackConfigs,
            new Dictionary<string, string>());

        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId());
            deployment.CompleteStack(stack.StackName);
        }

        return deployment;
    }

    private void SetupDeploymentFound(ProductDeployment deployment)
    {
        _repositoryMock
            .Setup(r => r.Get(It.Is<ProductDeploymentId>(id => id == deployment.Id)))
            .Returns(deployment);
    }

    private ChangeProductOperationModeCommand CreateCommand(
        ProductDeployment deployment,
        string mode = "Maintenance",
        string? reason = null,
        string source = "Manual")
    {
        return new ChangeProductOperationModeCommand(
            TestEnvironmentId,
            deployment.Id.Value.ToString(),
            mode,
            reason,
            source);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFail()
    {
        var command = new ChangeProductOperationModeCommand(
            TestEnvironmentId, "not-a-guid", "Maintenance");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid product deployment ID");
    }

    [Fact]
    public async Task Handle_ProductDeploymentNotFound_ReturnsFail()
    {
        _repositoryMock
            .Setup(r => r.Get(It.IsAny<ProductDeploymentId>()))
            .Returns((ProductDeployment?)null);

        var command = new ChangeProductOperationModeCommand(
            TestEnvironmentId, Guid.NewGuid().ToString(), "Maintenance");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_InvalidMode_ReturnsFail()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "InvalidMode"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid operation mode");
        result.Message.Should().Contain("InvalidMode");
    }

    [Fact]
    public async Task Handle_DeploymentNotOperational_ReturnsFail()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("s", "S", "source:s:1.0", 1, new Dictionary<string, string>())
        };
        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "g", "p", "test", "Test", "1.0.0",
            UserId.Create(), "deploy", stackConfigs, new Dictionary<string, string>());

        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot change operation mode");
        result.Message.Should().Contain("Deploying");
    }

    #endregion

    #region Enter Maintenance

    [Fact]
    public async Task Handle_EnterMaintenance_Manual_Succeeds()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance", reason: "Scheduled window"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PreviousMode.Should().Be("Normal");
        result.NewMode.Should().Be("Maintenance");
        result.TriggerSource.Should().Be("Manual");
        deployment.OperationMode.Should().Be(OperationMode.Maintenance);
        deployment.MaintenanceTrigger!.IsManual.Should().BeTrue();
        deployment.MaintenanceTrigger.Reason.Should().Be("Scheduled window");
    }

    [Fact]
    public async Task Handle_EnterMaintenance_Observer_Succeeds()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance", reason: "DB maintenance", source: "Observer"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TriggerSource.Should().Be("Observer");
        deployment.MaintenanceTrigger!.IsObserver.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EnterMaintenance_StopsAllRunningStacks()
    {
        var deployment = CreateRunningDeployment(3);
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
            TestEnvironmentId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_EnterMaintenance_DoesNotStartContainers()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        _dockerServiceMock.Verify(d => d.StartStackContainersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_EnterMaintenance_SavesChanges()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        _repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_EnterMaintenance_AlreadyInMaintenance_ReturnsOkAsNoOp()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Already in"));
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        // Same-mode is a no-op success (handler short-circuits before domain call)
        result.Success.Should().BeTrue();
        result.PreviousMode.Should().Be("Maintenance");
        result.NewMode.Should().Be("Maintenance");
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    #endregion

    #region Exit Maintenance

    [Fact]
    public async Task Handle_ExitMaintenance_Manual_FromManual_Succeeds()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Test"));
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Manual"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PreviousMode.Should().Be("Maintenance");
        result.NewMode.Should().Be("Normal");
        deployment.OperationMode.Should().Be(OperationMode.Normal);
        deployment.MaintenanceTrigger.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExitMaintenance_Observer_FromObserver_Succeeds()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Observer("External"));
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Observer"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ExitMaintenance_StartsAllRunningStacks()
    {
        var deployment = CreateRunningDeployment(3);
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Test"));
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Manual"),
            CancellationToken.None);

        _dockerServiceMock.Verify(d => d.StartStackContainersAsync(
            TestEnvironmentId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ExitMaintenance_DoesNotStopContainers()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Test"));
        SetupDeploymentFound(deployment);

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "c1" });

        await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Manual"),
            CancellationToken.None);

        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Ownership Rules — Blocking

    [Fact]
    public async Task Handle_ManualExitFromObserverMaintenance_ReturnsFail()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Observer("DB maintenance"));
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Manual"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("observer");
    }

    [Fact]
    public async Task Handle_ObserverExitFromManualMaintenance_ReturnsFail()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("User maintenance"));
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Observer"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("manually activated");
    }

    [Fact]
    public async Task Handle_ExitWhenNotInMaintenance_ReturnsOkAsNoOp()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Normal", source: "Manual"),
            CancellationToken.None);

        // Same-mode is a no-op success (handler short-circuits before domain call)
        result.Success.Should().BeTrue();
        result.PreviousMode.Should().Be("Normal");
        result.NewMode.Should().Be("Normal");
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    #endregion

    #region Same Mode — No-Op

    [Fact]
    public async Task Handle_SameMode_Normal_ReturnsOkWithoutChanges()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Normal"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PreviousMode.Should().Be("Normal");
        result.NewMode.Should().Be("Normal");

        _repositoryMock.Verify(r => r.SaveChanges(), Times.Never);
        _dockerServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_SameMode_Maintenance_ReturnsOkWithoutChanges()
    {
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual());
        SetupDeploymentFound(deployment);

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Never);
    }

    #endregion

    #region Container Lifecycle — Edge Cases

    [Fact]
    public async Task Handle_ContainerStopFails_StillSucceeds()
    {
        var deployment = CreateRunningDeployment(2);
        SetupDeploymentFound(deployment);

        var callCount = 0;
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, CancellationToken _) =>
            {
                if (callCount++ == 0)
                    throw new InvalidOperationException("Docker error");
                return (IReadOnlyList<string>)new List<string> { "c1" };
            });

        var result = await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        // Mode change succeeds even if container operations fail
        result.Success.Should().BeTrue();
        deployment.OperationMode.Should().Be(OperationMode.Maintenance);
    }

    [Fact]
    public async Task Handle_SkipsNonRunningStacks()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("db", "Database", "source:db:1.0", 1, new Dictionary<string, string>()),
            new("api", "API", "source:api:1.0", 2, new Dictionary<string, string>())
        };

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "g", "p", "test", "Test", "1.0.0",
            UserId.Create(), "deploy", stackConfigs, new Dictionary<string, string>());

        // Only complete the first stack; second is still deploying
        deployment.StartStack("db", DeploymentId.NewId());
        deployment.CompleteStack("db");
        deployment.StartStack("api", DeploymentId.NewId());
        deployment.FailStack("api", "Failed");
        deployment.MarkAsPartiallyRunning("1 of 2 failed");

        SetupDeploymentFound(deployment);

        await _handler.Handle(
            CreateCommand(deployment, mode: "Maintenance"), CancellationToken.None);

        // Only the Running stack (db) should have containers stopped
        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Source Parsing

    [Fact]
    public async Task Handle_SourceObserver_CaseInsensitive()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var command = new ChangeProductOperationModeCommand(
            TestEnvironmentId, deployment.Id.Value.ToString(),
            "Maintenance", "Test", "observer");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        deployment.MaintenanceTrigger!.IsObserver.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownSource_TreatedAsManual()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentFound(deployment);

        var command = new ChangeProductOperationModeCommand(
            TestEnvironmentId, deployment.Id.Value.ToString(),
            "Maintenance", "Test", "ApiHook");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        deployment.MaintenanceTrigger!.IsManual.Should().BeTrue();
    }

    #endregion
}

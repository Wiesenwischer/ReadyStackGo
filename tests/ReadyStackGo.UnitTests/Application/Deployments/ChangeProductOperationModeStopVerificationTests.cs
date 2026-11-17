using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

/// <summary>
/// Entering maintenance must actually leave nothing running — that is the whole point of the mode,
/// and a product update that waits for all database sessions to close depends on it.
///
/// These tests cover the ways a container survived the transition: a stack whose recorded status is
/// not Running was skipped wholesale, a failed stop was never retried, and the transition reported
/// "All containers stopped" regardless of the outcome.
/// </summary>
public class ChangeProductOperationModeStopVerificationTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock = new();
    private readonly Mock<IDeploymentRepository> _deploymentRepositoryMock = new();
    private readonly Mock<IDockerService> _dockerServiceMock = new();
    private readonly Mock<IHealthNotificationService> _healthNotificationMock = new();
    private readonly Mock<IMaintenanceSetterService> _setterServiceMock = new();
    private readonly Mock<IDeploymentNotificationService> _deploymentNotificationMock = new();
    private readonly ChangeProductOperationModeHandler _handler;

    private readonly List<MaintenanceProgressNotification> _progress = new();

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private const string TestSessionId = "session-1";

    /// <summary>Container list the mocked Docker daemon currently reports. Tests mutate it.</summary>
    private List<ContainerDto> _containers = new();

    public ChangeProductOperationModeStopVerificationTests()
    {
        _setterServiceMock
            .Setup(s => s.ApplyAsync(It.IsAny<MaintenanceSetterConfig?>(), It.IsAny<MaintenanceState>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SetterResult.WasSkipped("no setter configured"));

        _deploymentNotificationMock
            .Setup(n => n.NotifyMaintenanceProgressAsync(It.IsAny<MaintenanceProgressNotification>(), It.IsAny<CancellationToken>()))
            .Callback<MaintenanceProgressNotification, CancellationToken>((n, _) => _progress.Add(n))
            .Returns(Task.CompletedTask);

        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _containers.ToList());

        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        _dockerServiceMock
            .Setup(d => d.StartStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        _handler = new ChangeProductOperationModeHandler(
            _repositoryMock.Object,
            _deploymentRepositoryMock.Object,
            _dockerServiceMock.Object,
            _healthNotificationMock.Object,
            _setterServiceMock.Object,
            _deploymentNotificationMock.Object,
            Mock.Of<ILogger<ChangeProductOperationModeHandler>>());
    }

    #region Helpers

    private static ProductDeployment InitiateDeployment(int stackCount)
    {
        var stackConfigs = Enumerable.Range(0, stackCount).Select(i =>
            new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"source:product:stack-{i}:1.0",
                i + 1, new Dictionary<string, string>()))
            .ToList();

        return ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "product-group", "product-id",
            "test-product", "Test Product", "1.0.0",
            UserId.Create(),
            "test-deploy",
            stackConfigs,
            new Dictionary<string, string>());
    }

    /// <summary>A product where every stack came up: all stacks Running, product Running.</summary>
    private ProductDeployment RunningDeployment(int stackCount = 1)
    {
        var deployment = InitiateDeployment(stackCount);

        foreach (var stack in deployment.GetStacksInDeployOrder().ToList())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId());
            deployment.CompleteStack(stack.StackName);
        }

        Register(deployment);
        return deployment;
    }

    /// <summary>
    /// A product where the second stack failed — the realistic case behind the bug: one container
    /// never became healthy, so the whole stack is recorded as Failed while its other containers
    /// are up and running.
    /// </summary>
    private ProductDeployment PartiallyRunningDeploymentWithFailedStack()
    {
        var deployment = InitiateDeployment(2);
        var stacks = deployment.GetStacksInDeployOrder().ToList();

        deployment.StartStack(stacks[0].StackName, DeploymentId.NewId());
        deployment.CompleteStack(stacks[0].StackName);

        deployment.StartStack(stacks[1].StackName, DeploymentId.NewId());
        deployment.FailStack(stacks[1].StackName, "container never became healthy");

        deployment.MarkAsPartiallyRunning("one stack failed");

        Register(deployment);
        return deployment;
    }

    /// <summary>
    /// A product where the second stack was never started: status Pending, and therefore no
    /// deployment stack name. Nothing to stop, and nothing that may be dereferenced.
    /// </summary>
    private ProductDeployment PartiallyRunningDeploymentWithPendingStack()
    {
        var deployment = InitiateDeployment(2);
        var stacks = deployment.GetStacksInDeployOrder().ToList();

        deployment.StartStack(stacks[0].StackName, DeploymentId.NewId());
        deployment.CompleteStack(stacks[0].StackName);
        deployment.MarkAsPartiallyRunning("second stack not attempted");

        Register(deployment);
        return deployment;
    }

    private void Register(ProductDeployment deployment)
        => _repositoryMock.Setup(r => r.Get(It.Is<ProductDeploymentId>(id => id == deployment.Id)))
                          .Returns(deployment);

    private static string StackNameOf(ProductDeployment deployment, StackDeploymentStatus status)
        => deployment.Stacks.First(s => s.Status == status).DeploymentStackName!;

    private static ContainerDto Container(
        string name, string stackName, string state = "running", string? maintenance = null)
    {
        var labels = new Dictionary<string, string> { ["rsgo.stack"] = stackName };
        if (maintenance != null) labels["rsgo.maintenance"] = maintenance;

        return new ContainerDto
        {
            Id = $"id-{name}",
            Name = name,
            Image = "test:1.0",
            State = state,
            Status = state,
            Labels = labels
        };
    }

    private Task<ChangeProductOperationModeResponse> EnterMaintenance(ProductDeployment deployment)
        => _handler.Handle(
            new ChangeProductOperationModeCommand(
                TestEnvironmentId, deployment.Id.Value.ToString(), "Maintenance",
                Reason: "test", Source: "Manual", SessionId: TestSessionId),
            CancellationToken.None);

    private Task<ChangeProductOperationModeResponse> ExitMaintenance(ProductDeployment deployment)
        => _handler.Handle(
            new ChangeProductOperationModeCommand(
                TestEnvironmentId, deployment.Id.Value.ToString(), "Normal",
                Reason: "test", Source: "Manual", SessionId: TestSessionId),
            CancellationToken.None);

    /// <summary>Makes the mocked daemon report the container as stopped after N bulk stop calls.</summary>
    private void StopTakesEffectAfter(int bulkStopCalls)
    {
        var calls = 0;
        _dockerServiceMock
            .Setup(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                if (calls >= bulkStopCalls)
                {
                    _containers = _containers.Select(c => c with { State = "exited" }).ToList();
                }

                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            });
    }

    private MaintenanceProgressNotification TerminalUpdate
        => _progress.Last();

    #endregion

    #region Stacks that are not Running (gap: whole stack skipped)

    [Fact]
    public async Task EnterMaintenance_FailedStack_StopsItsContainersToo()
    {
        // A stack recorded as Failed still has running containers holding database connections.
        var deployment = PartiallyRunningDeploymentWithFailedStack();
        var failedStack = StackNameOf(deployment, StackDeploymentStatus.Failed);

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
                TestEnvironmentId, failedStack,
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "containers of a Failed stack keep running and must be stopped as well");
    }

    [Fact]
    public async Task EnterMaintenance_FailedStack_IsCountedInProgress()
    {
        var deployment = PartiallyRunningDeploymentWithFailedStack();

        await EnterMaintenance(deployment);

        _progress.Should().Contain(p => p.TotalStacks == 2,
            "the progress denominator must include the Failed stack, otherwise the user cannot see it was handled");
    }

    [Fact]
    public async Task EnterMaintenance_PendingStack_IsSkippedWithoutFailing()
    {
        // A Pending stack has no deployment stack name — it must not be dereferenced.
        var deployment = PartiallyRunningDeploymentWithPendingStack();
        var runningStack = StackNameOf(deployment, StackDeploymentStatus.Running);

        var response = await EnterMaintenance(deployment);

        response.Success.Should().BeTrue();
        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
                TestEnvironmentId, runningStack,
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _dockerServiceMock.Verify(d => d.StopStackContainersAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "the Pending stack has nothing deployed, so exactly one stack is stopped");
    }

    [Fact]
    public async Task ExitMaintenance_FailedStack_StartsItsContainersAgain()
    {
        // Symmetry: whatever maintenance stopped, exiting maintenance has to start again.
        var deployment = PartiallyRunningDeploymentWithFailedStack();
        var failedStack = StackNameOf(deployment, StackDeploymentStatus.Failed);
        await EnterMaintenance(deployment);

        await ExitMaintenance(deployment);

        _dockerServiceMock.Verify(d => d.StartStackContainersAsync(
                TestEnvironmentId, failedStack,
                It.IsAny<Func<StackContainerProgress, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Verification, retry and kill escalation

    [Fact]
    public async Task EnterMaintenance_ContainerStillRunningAfterBulkStop_IsRetriedIndividually()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack)];
        StopTakesEffectAfter(bulkStopCalls: int.MaxValue); // bulk stop never takes effect

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.StopContainerAsync(TestEnvironmentId, "id-web-1", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "a container still running after the bulk stop must be stopped again, individually");
    }

    [Fact]
    public async Task EnterMaintenance_ContainerNeverStops_IsKilled()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack)];

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.KillContainerAsync(TestEnvironmentId, "id-web-1", It.IsAny<CancellationToken>()),
            Times.Once,
            "a container that survives stop and retry must be killed — maintenance may not leave it running");
    }

    [Fact]
    public async Task EnterMaintenance_RestartingContainer_IsTreatedAsStillRunning()
    {
        // The field case: restart: always plus a crash loop means state 'restarting', which the old
        // 'state == running' check ignored completely.
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack, state: "restarting")];

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.KillContainerAsync(TestEnvironmentId, "id-web-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnterMaintenance_StopTakesEffect_DoesNotRetryOrKill()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack)];
        StopTakesEffectAfter(bulkStopCalls: 1);

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.StopContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _dockerServiceMock.Verify(
            d => d.KillContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnterMaintenance_AlreadyExitedContainer_IsNotTouched()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack, state: "exited")];

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.StopContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _dockerServiceMock.Verify(
            d => d.KillContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnterMaintenance_ExemptContainer_IsNeitherRetriedNorKilled()
    {
        // rsgo.maintenance=ignore containers (edge proxy, maintenance page) must survive untouched.
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("edge", stack, maintenance: "ignore")];

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.StopContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _dockerServiceMock.Verify(
            d => d.KillContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnterMaintenance_ContainerOfAnotherStack_IsNotTouched()
    {
        var deployment = RunningDeployment();
        _containers = [Container("other", "some-foreign-stack")];

        await EnterMaintenance(deployment);

        _dockerServiceMock.Verify(
            d => d.KillContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnterMaintenance_KillFails_DoesNotThrow()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack)];
        _dockerServiceMock
            .Setup(d => d.KillContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("daemon gone"));

        var response = await EnterMaintenance(deployment);

        response.Success.Should().BeTrue("a failed kill is reported, not thrown at the caller");
        TerminalUpdate.Phase.Should().Be("Failed");
    }

    #endregion

    #region Honest reporting

    [Fact]
    public async Task EnterMaintenance_ContainersRemainRunning_ReportsFailedPhase()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack), Container("web-2", stack)];

        await EnterMaintenance(deployment);

        TerminalUpdate.Phase.Should().Be("Failed",
            "reporting Completed while containers are still running hides the failure completely");
        TerminalUpdate.Message.Should().Contain("2");
    }

    [Fact]
    public async Task EnterMaintenance_EverythingStopped_ReportsCompletedPhase()
    {
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack)];
        StopTakesEffectAfter(bulkStopCalls: 1);

        await EnterMaintenance(deployment);

        TerminalUpdate.Phase.Should().Be("Completed");
        TerminalUpdate.Message.Should().Contain("stopped");
    }

    [Fact]
    public async Task EnterMaintenance_NoContainersAtAll_ReportsCompletedPhase()
    {
        var deployment = RunningDeployment();
        _containers = [];

        await EnterMaintenance(deployment);

        TerminalUpdate.Phase.Should().Be("Completed");
    }

    [Fact]
    public async Task EnterMaintenance_VerificationFails_ReportsFailedPhase()
    {
        // If the container list cannot be read, nothing is known about the stack. Claiming success
        // without having looked is precisely what kept the original bug invisible.
        var deployment = RunningDeployment();
        _dockerServiceMock
            .Setup(d => d.ListContainersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("docker daemon unreachable"));

        var response = await EnterMaintenance(deployment);

        response.Success.Should().BeTrue();
        TerminalUpdate.Phase.Should().Be("Failed");
        TerminalUpdate.Message.Should().Contain("verify");
    }

    [Fact]
    public async Task EnterMaintenance_ContainersRemainRunning_StillReportsModeChange()
    {
        // The mode has already been propagated to the product; rolling it back would be more
        // surprising than reporting the failed stop. The caller still learns the mode changed.
        var deployment = RunningDeployment();
        var stack = StackNameOf(deployment, StackDeploymentStatus.Running);
        _containers = [Container("web-1", stack)];

        var response = await EnterMaintenance(deployment);

        response.Success.Should().BeTrue();
        response.NewMode.Should().Be("Maintenance");
        deployment.OperationMode.Should().Be(OperationMode.Maintenance);
    }

    #endregion
}

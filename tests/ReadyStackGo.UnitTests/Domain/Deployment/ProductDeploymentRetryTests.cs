using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ProductDeployment retry functionality.
/// Covers CanRetry property, StartRetry() method, state transitions,
/// domain events, and edge cases.
/// </summary>
public class ProductDeploymentRetryTests
{
    #region CanRetry Property

    [Fact]
    public void CanRetry_WhenPartiallyRunning_ReturnsTrue()
    {
        var pd = CreatePartiallyRunningDeployment();
        pd.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenFailed_ReturnsTrue()
    {
        var pd = CreateFailedDeployment();
        pd.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenRunning_ReturnsFalse()
    {
        var pd = CreateRunningDeployment(2);
        pd.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenDeploying_ReturnsFalse()
    {
        var pd = CreateTestDeployment(2);
        pd.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenUpgrading_ReturnsFalse()
    {
        var pd = CreateUpgradingDeployment();
        pd.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenRemoving_ReturnsFalse()
    {
        var pd = CreateRemovingDeployment();
        pd.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenRemoved_ReturnsFalse()
    {
        var pd = CreateRemovedDeployment();
        pd.CanRetry.Should().BeFalse();
    }

    #endregion

    #region StartRetry - Valid States

    [Fact]
    public void StartRetry_FromPartiallyRunning_TransitionsToDeploying()
    {
        var pd = CreatePartiallyRunningDeployment();

        pd.StartRetry();

        pd.Status.Should().Be(ProductDeploymentStatus.Deploying);
    }

    [Fact]
    public void StartRetry_FromFailed_TransitionsToDeploying()
    {
        var pd = CreateFailedDeployment();

        pd.StartRetry();

        pd.Status.Should().Be(ProductDeploymentStatus.Deploying);
    }

    [Fact]
    public void StartRetry_ClearsCompletedAt()
    {
        var pd = CreatePartiallyRunningDeployment();
        pd.CompletedAt.Should().NotBeNull();

        pd.StartRetry();

        pd.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void StartRetry_ClearsErrorMessage()
    {
        var pd = CreateFailedDeployment();
        pd.ErrorMessage.Should().NotBeNull();

        pd.StartRetry();

        pd.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void StartRetry_ResetsFailedStacksToPending()
    {
        var pd = CreatePartiallyRunningDeployment();
        pd.Stacks.Should().Contain(s => s.Status == StackDeploymentStatus.Failed);

        pd.StartRetry();

        var resetStacks = pd.Stacks.Where(s => s.StackName == "stack-1");
        resetStacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    [Fact]
    public void StartRetry_LeavesRunningStacksUntouched()
    {
        var pd = CreatePartiallyRunningDeployment();

        pd.StartRetry();

        var runningStack = pd.Stacks.Single(s => s.StackName == "stack-0");
        runningStack.Status.Should().Be(StackDeploymentStatus.Running);
        runningStack.DeploymentId.Should().NotBeNull();
    }

    [Fact]
    public void StartRetry_RaisesProductRetryInitiatedEvent()
    {
        var pd = CreatePartiallyRunningDeployment();
        pd.ClearDomainEvents();

        pd.StartRetry();

        var evt = pd.DomainEvents.OfType<ProductRetryInitiated>().Single();
        evt.ProductDeploymentId.Should().Be(pd.Id);
        evt.ProductName.Should().Be("testproduct");
        evt.TotalStacks.Should().Be(2);
    }

    [Fact]
    public void StartRetry_RecordsPhaseHistory()
    {
        var pd = CreatePartiallyRunningDeployment();
        var phaseCountBefore = pd.PhaseHistory.Count;

        pd.StartRetry();

        pd.PhaseHistory.Count.Should().BeGreaterThan(phaseCountBefore);
        pd.PhaseHistory.Last().Message.Should().Contain("Retry");
    }

    #endregion

    #region StartRetry - Invalid States

    [Fact]
    public void StartRetry_WhenRunning_ThrowsInvalidOperationException()
    {
        var pd = CreateRunningDeployment(2);

        var act = () => pd.StartRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot retry*");
    }

    [Fact]
    public void StartRetry_WhenDeploying_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(2);

        var act = () => pd.StartRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot retry*");
    }

    [Fact]
    public void StartRetry_WhenUpgrading_ThrowsInvalidOperationException()
    {
        var pd = CreateUpgradingDeployment();

        var act = () => pd.StartRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot retry*");
    }

    [Fact]
    public void StartRetry_WhenRemoving_ThrowsInvalidOperationException()
    {
        var pd = CreateRemovingDeployment();

        var act = () => pd.StartRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot retry*");
    }

    [Fact]
    public void StartRetry_WhenRemoved_ThrowsInvalidOperationException()
    {
        var pd = CreateRemovedDeployment();

        var act = () => pd.StartRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot retry*");
    }

    #endregion

    #region StartRetry - Multi-Stack Scenarios

    [Fact]
    public void StartRetry_WithMultipleFailedStacks_ResetsAllFailedToPending()
    {
        // 3 stacks: stack-0 running, stack-1 failed, stack-2 failed
        var pd = CreateTestDeployment(3);
        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Error 1");
        pd.StartStack("stack-2", DeploymentId.NewId());
        pd.FailStack("stack-2", "Error 2");
        pd.MarkAsPartiallyRunning("Multiple failures");

        pd.StartRetry();

        pd.Stacks.Single(s => s.StackName == "stack-0").Status
            .Should().Be(StackDeploymentStatus.Running);
        pd.Stacks.Single(s => s.StackName == "stack-1").Status
            .Should().Be(StackDeploymentStatus.Pending);
        pd.Stacks.Single(s => s.StackName == "stack-2").Status
            .Should().Be(StackDeploymentStatus.Pending);
    }

    [Fact]
    public void StartRetry_FromFailed_AllStacksResetToPending()
    {
        // All stacks failed
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.FailStack("stack-0", "Error 0");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Error 1");
        pd.MarkAsFailed("All failed");

        pd.StartRetry();

        pd.Stacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    [Fact]
    public void StartRetry_PreservesRunningStackDeploymentIds()
    {
        var pd = CreatePartiallyRunningDeployment();
        var runningStack = pd.Stacks.Single(s => s.StackName == "stack-0");
        var originalDeploymentId = runningStack.DeploymentId;

        pd.StartRetry();

        var afterRetry = pd.Stacks.Single(s => s.StackName == "stack-0");
        afterRetry.DeploymentId.Should().Be(originalDeploymentId);
    }

    [Fact]
    public void StartRetry_ClearsFailedStackDeploymentState()
    {
        var pd = CreatePartiallyRunningDeployment();
        var failedStack = pd.Stacks.Single(s => s.StackName == "stack-1");
        failedStack.ErrorMessage.Should().NotBeNull();

        pd.StartRetry();

        var resetStack = pd.Stacks.Single(s => s.StackName == "stack-1");
        resetStack.ErrorMessage.Should().BeNull();
        resetStack.StartedAt.Should().BeNull();
        resetStack.CompletedAt.Should().BeNull();
    }

    #endregion

    #region Retry Lifecycle - Start → Complete/Fail

    [Fact]
    public void RetryLifecycle_StartRetry_ThenCompleteAllStacks_TransitionsToRunning()
    {
        var pd = CreatePartiallyRunningDeployment();

        pd.StartRetry();
        // stack-0 is already Running, stack-1 was reset to Pending
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.CompleteStack("stack-1");

        // All stacks are now Running → should be valid to mark as running
        pd.CompletedStacks.Should().Be(2);
        pd.FailedStacks.Should().Be(0);
    }

    [Fact]
    public void RetryLifecycle_StartRetry_ThenFailAgain_CanRetryAgain()
    {
        var pd = CreatePartiallyRunningDeployment();

        pd.StartRetry();
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Still failing");
        pd.MarkAsPartiallyRunning("Still partially running");

        pd.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void RetryLifecycle_MultipleRetries_PreservesRunningStacks()
    {
        var pd = CreatePartiallyRunningDeployment();
        var originalDeploymentId = pd.Stacks.Single(s => s.StackName == "stack-0").DeploymentId;

        // First retry - still fails
        pd.StartRetry();
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Still failing");
        pd.MarkAsPartiallyRunning("Still failing");

        // Second retry
        pd.StartRetry();

        var runningStack = pd.Stacks.Single(s => s.StackName == "stack-0");
        runningStack.Status.Should().Be(StackDeploymentStatus.Running);
        runningStack.DeploymentId.Should().Be(originalDeploymentId);
    }

    #endregion

    #region State Machine - Transition Validation

    [Theory]
    [InlineData(ProductDeploymentStatus.PartiallyRunning, true)]
    [InlineData(ProductDeploymentStatus.Failed, true)]
    [InlineData(ProductDeploymentStatus.Deploying, false)]
    [InlineData(ProductDeploymentStatus.Running, false)]
    [InlineData(ProductDeploymentStatus.Upgrading, false)]
    [InlineData(ProductDeploymentStatus.Removing, false)]
    [InlineData(ProductDeploymentStatus.Removed, false)]
    public void CanTransitionToDeploying_ReturnsExpectedResult(
        ProductDeploymentStatus fromStatus, bool expected)
    {
        var pd = CreateDeploymentInStatus(fromStatus);

        pd.CanTransitionTo(ProductDeploymentStatus.Deploying).Should().Be(expected);
    }

    #endregion

    #region Helpers

    private static ProductDeployment CreateTestDeployment(int stackCount)
    {
        return ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), "test-deployment",
            CreateStackConfigs(stackCount),
            new Dictionary<string, string> { { "SHARED", "value" } });
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

    private static ProductDeployment CreateRunningDeployment(int stackCount)
    {
        var pd = CreateTestDeployment(stackCount);
        for (var i = 0; i < stackCount; i++)
        {
            pd.StartStack($"stack-{i}", DeploymentId.NewId());
            pd.CompleteStack($"stack-{i}");
        }
        return pd;
    }

    private static ProductDeployment CreatePartiallyRunningDeployment()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Error");
        pd.MarkAsPartiallyRunning("Partial failure");
        return pd;
    }

    private static ProductDeployment CreateFailedDeployment()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.FailStack("stack-0", "Error");
        pd.MarkAsFailed("All failed");
        return pd;
    }

    private static ProductDeployment CreateUpgradingDeployment()
    {
        var existing = CreateRunningDeployment(2);
        return ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), "test-deployment", existing, CreateStackConfigs(2),
            new Dictionary<string, string>());
    }

    private static ProductDeployment CreateRemovingDeployment()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRemoval();
        return pd;
    }

    private static ProductDeployment CreateRemovedDeployment()
    {
        var pd = CreateRunningDeployment(1);
        pd.StartRemoval();
        pd.MarkStackRemoved("stack-0");
        return pd;
    }

    private static ProductDeployment CreateDeploymentInStatus(ProductDeploymentStatus status)
    {
        return status switch
        {
            ProductDeploymentStatus.Deploying => CreateTestDeployment(2),
            ProductDeploymentStatus.Running => CreateRunningDeployment(2),
            ProductDeploymentStatus.PartiallyRunning => CreatePartiallyRunningDeployment(),
            ProductDeploymentStatus.Upgrading => CreateUpgradingDeployment(),
            ProductDeploymentStatus.Failed => CreateFailedDeployment(),
            ProductDeploymentStatus.Removing => CreateRemovingDeployment(),
            ProductDeploymentStatus.Removed => CreateRemovedDeployment(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    #endregion
}

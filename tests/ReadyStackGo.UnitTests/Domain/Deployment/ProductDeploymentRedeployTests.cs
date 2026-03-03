using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ProductDeployment redeploy functionality.
/// Covers CanRedeploy property, StartRedeploy() method, state transitions,
/// selective stack redeploy, domain events, and edge cases.
/// </summary>
public class ProductDeploymentRedeployTests
{
    #region CanRedeploy Property

    [Fact]
    public void CanRedeploy_WhenRunning_ReturnsTrue()
    {
        var pd = CreateRunningDeployment(2);
        pd.CanRedeploy.Should().BeTrue();
    }

    [Fact]
    public void CanRedeploy_WhenDeploying_ReturnsFalse()
    {
        var pd = CreateTestDeployment(2);
        pd.CanRedeploy.Should().BeFalse();
    }

    [Fact]
    public void CanRedeploy_WhenPartiallyRunning_ReturnsFalse()
    {
        var pd = CreatePartiallyRunningDeployment();
        pd.CanRedeploy.Should().BeFalse();
    }

    [Fact]
    public void CanRedeploy_WhenFailed_ReturnsFalse()
    {
        var pd = CreateFailedDeployment();
        pd.CanRedeploy.Should().BeFalse();
    }

    [Fact]
    public void CanRedeploy_WhenUpgrading_ReturnsFalse()
    {
        var pd = CreateUpgradingDeployment();
        pd.CanRedeploy.Should().BeFalse();
    }

    [Fact]
    public void CanRedeploy_WhenRemoving_ReturnsFalse()
    {
        var pd = CreateRemovingDeployment();
        pd.CanRedeploy.Should().BeFalse();
    }

    [Fact]
    public void CanRedeploy_WhenRemoved_ReturnsFalse()
    {
        var pd = CreateRemovedDeployment();
        pd.CanRedeploy.Should().BeFalse();
    }

    [Fact]
    public void CanRedeploy_WhenStopped_ReturnsFalse()
    {
        var pd = CreateStoppedDeployment();
        pd.CanRedeploy.Should().BeFalse();
    }

    #endregion

    #region StartRedeploy - All Stacks

    [Fact]
    public void StartRedeploy_AllStacks_TransitionsToRedeploying()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRedeploy();

        pd.Status.Should().Be(ProductDeploymentStatus.Redeploying);
    }

    [Fact]
    public void StartRedeploy_AllStacks_ResetsAllStacksToPending()
    {
        var pd = CreateRunningDeployment(3);

        pd.StartRedeploy();

        pd.Stacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    [Fact]
    public void StartRedeploy_AllStacks_ClearsCompletedAt()
    {
        var pd = CreateRunningDeployment(2);
        pd.CompletedAt.Should().NotBeNull();

        pd.StartRedeploy();

        pd.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void StartRedeploy_AllStacks_ClearsErrorMessage()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRedeploy();

        pd.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void StartRedeploy_AllStacks_RaisesProductRedeployInitiatedEvent()
    {
        var pd = CreateRunningDeployment(3);
        pd.ClearDomainEvents();

        pd.StartRedeploy();

        var evt = pd.DomainEvents.OfType<ProductRedeployInitiated>().Single();
        evt.ProductDeploymentId.Should().Be(pd.Id);
        evt.ProductName.Should().Be("testproduct");
        evt.RedeployStackCount.Should().Be(3);
        evt.TotalStacks.Should().Be(3);
    }

    [Fact]
    public void StartRedeploy_AllStacks_RecordsPhaseHistory()
    {
        var pd = CreateRunningDeployment(2);
        var phaseCountBefore = pd.PhaseHistory.Count;

        pd.StartRedeploy();

        pd.PhaseHistory.Count.Should().BeGreaterThan(phaseCountBefore);
        pd.PhaseHistory.Last().Message.Should().Contain("Redeploy initiated for all stacks");
    }

    [Fact]
    public void StartRedeploy_WithNullStackNames_RedeploysAllStacks()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRedeploy(null);

        pd.Stacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    [Fact]
    public void StartRedeploy_WithEmptyStackNames_RedeploysAllStacks()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRedeploy(new List<string>());

        pd.Stacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    #endregion

    #region StartRedeploy - Selective Stacks

    [Fact]
    public void StartRedeploy_SelectedStacks_ResetsOnlyNamedStacks()
    {
        var pd = CreateRunningDeployment(3);

        pd.StartRedeploy(new List<string> { "stack-1" });

        pd.Stacks.Single(s => s.StackName == "stack-0").Status
            .Should().Be(StackDeploymentStatus.Running);
        pd.Stacks.Single(s => s.StackName == "stack-1").Status
            .Should().Be(StackDeploymentStatus.Pending);
        pd.Stacks.Single(s => s.StackName == "stack-2").Status
            .Should().Be(StackDeploymentStatus.Running);
    }

    [Fact]
    public void StartRedeploy_SelectedStacks_RaisesEventWithCorrectStackCount()
    {
        var pd = CreateRunningDeployment(3);
        pd.ClearDomainEvents();

        pd.StartRedeploy(new List<string> { "stack-0", "stack-2" });

        var evt = pd.DomainEvents.OfType<ProductRedeployInitiated>().Single();
        evt.RedeployStackCount.Should().Be(2);
        evt.TotalStacks.Should().Be(3);
    }

    [Fact]
    public void StartRedeploy_SelectedStacks_RecordsPhaseHistoryWithStackNames()
    {
        var pd = CreateRunningDeployment(3);

        pd.StartRedeploy(new List<string> { "stack-0", "stack-2" });

        pd.PhaseHistory.Last().Message.Should().Contain("stack-0");
        pd.PhaseHistory.Last().Message.Should().Contain("stack-2");
    }

    [Fact]
    public void StartRedeploy_SelectedStacks_IsCaseInsensitive()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRedeploy(new List<string> { "STACK-0" });

        pd.Stacks.Single(s => s.StackName == "stack-0").Status
            .Should().Be(StackDeploymentStatus.Pending);
        pd.Stacks.Single(s => s.StackName == "stack-1").Status
            .Should().Be(StackDeploymentStatus.Running);
    }

    [Fact]
    public void StartRedeploy_WithUnknownStackName_ThrowsInvalidOperationException()
    {
        var pd = CreateRunningDeployment(2);

        var act = () => pd.StartRedeploy(new List<string> { "nonexistent" });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void StartRedeploy_WithMixedValidAndInvalidNames_ThrowsBeforeResetting()
    {
        var pd = CreateRunningDeployment(2);

        var act = () => pd.StartRedeploy(new List<string> { "stack-0", "nonexistent" });

        act.Should().Throw<InvalidOperationException>();
        // stack-0 should remain Running because validation happens before reset
        pd.Stacks.Single(s => s.StackName == "stack-0").Status
            .Should().Be(StackDeploymentStatus.Running);
        pd.Status.Should().Be(ProductDeploymentStatus.Running);
    }

    #endregion

    #region StartRedeploy - Invalid States

    [Fact]
    public void StartRedeploy_WhenDeploying_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(2);

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    [Fact]
    public void StartRedeploy_WhenPartiallyRunning_ThrowsInvalidOperationException()
    {
        var pd = CreatePartiallyRunningDeployment();

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    [Fact]
    public void StartRedeploy_WhenFailed_ThrowsInvalidOperationException()
    {
        var pd = CreateFailedDeployment();

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    [Fact]
    public void StartRedeploy_WhenUpgrading_ThrowsInvalidOperationException()
    {
        var pd = CreateUpgradingDeployment();

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    [Fact]
    public void StartRedeploy_WhenRemoving_ThrowsInvalidOperationException()
    {
        var pd = CreateRemovingDeployment();

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    [Fact]
    public void StartRedeploy_WhenRemoved_ThrowsInvalidOperationException()
    {
        var pd = CreateRemovedDeployment();

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    [Fact]
    public void StartRedeploy_WhenStopped_ThrowsInvalidOperationException()
    {
        var pd = CreateStoppedDeployment();

        var act = () => pd.StartRedeploy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot redeploy*");
    }

    #endregion

    #region State Machine - Redeploying Transitions

    [Fact]
    public void CanTransitionTo_FromRunning_ToRedeploying_ReturnsTrue()
    {
        var pd = CreateRunningDeployment(2);
        pd.CanTransitionTo(ProductDeploymentStatus.Redeploying).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromRedeploying_ToRunning_ReturnsTrue()
    {
        var pd = CreateRedeployingDeployment();
        pd.CanTransitionTo(ProductDeploymentStatus.Running).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromRedeploying_ToPartiallyRunning_ReturnsTrue()
    {
        var pd = CreateRedeployingDeployment();
        pd.CanTransitionTo(ProductDeploymentStatus.PartiallyRunning).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromRedeploying_ToFailed_ReturnsTrue()
    {
        var pd = CreateRedeployingDeployment();
        pd.CanTransitionTo(ProductDeploymentStatus.Failed).Should().BeTrue();
    }

    [Fact]
    public void CanTransitionTo_FromRedeploying_ToRemoving_ReturnsFalse()
    {
        var pd = CreateRedeployingDeployment();
        pd.CanTransitionTo(ProductDeploymentStatus.Removing).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromRedeploying_ToUpgrading_ReturnsFalse()
    {
        var pd = CreateRedeployingDeployment();
        pd.CanTransitionTo(ProductDeploymentStatus.Upgrading).Should().BeFalse();
    }

    #endregion

    #region IsInProgress - Includes Redeploying

    [Fact]
    public void IsInProgress_WhenRedeploying_ReturnsTrue()
    {
        var pd = CreateRedeployingDeployment();
        pd.IsInProgress.Should().BeTrue();
    }

    #endregion

    #region Redeploy Lifecycle - Start → Complete/Fail

    [Fact]
    public void RedeployLifecycle_AllStacks_ThenCompleteAll_TransitionsToRunning()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy();

        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.CompleteStack("stack-1");

        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.CompletedStacks.Should().Be(2);
    }

    [Fact]
    public void RedeployLifecycle_SelectedStack_ThenComplete_AllStacksRunning()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy(new List<string> { "stack-1" });

        // stack-0 is still Running, stack-1 was reset to Pending
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.CompleteStack("stack-1");

        // Both stacks are now Running → deployment completes
        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.CompletedStacks.Should().Be(2);
    }

    [Fact]
    public void RedeployLifecycle_AllStacks_ThenFailOne_CanMarkPartiallyRunning()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy();

        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Image pull failed");

        pd.MarkAsPartiallyRunning("One stack failed to redeploy");

        pd.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);
        pd.CompletedStacks.Should().Be(1);
        pd.FailedStacks.Should().Be(1);
    }

    [Fact]
    public void RedeployLifecycle_AllStacks_ThenFailAll_CanMarkFailed()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy();

        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.FailStack("stack-0", "Error");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Error");

        pd.MarkAsFailed("All stacks failed to redeploy");

        pd.Status.Should().Be(ProductDeploymentStatus.Failed);
    }

    [Fact]
    public void RedeployLifecycle_AfterFailedRedeploy_CanRetry()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy();

        pd.StartStack("stack-0", DeploymentId.NewId());
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId());
        pd.FailStack("stack-1", "Error");
        pd.MarkAsPartiallyRunning("Partial failure");

        pd.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void RedeployLifecycle_StackOperations_AllowedDuringRedeploying()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy();

        // StartStack should work in Redeploying status
        var act1 = () => pd.StartStack("stack-0", DeploymentId.NewId());
        act1.Should().NotThrow();

        // CompleteStack should work in Redeploying status
        var act2 = () => pd.CompleteStack("stack-0");
        act2.Should().NotThrow();

        // FailStack should work in Redeploying status
        pd.StartStack("stack-1", DeploymentId.NewId());
        var act3 = () => pd.FailStack("stack-1", "Error");
        act3.Should().NotThrow();
    }

    #endregion

    #region Redeploy - Clears Previous Stack State

    [Fact]
    public void StartRedeploy_ClearsStackStartedAt()
    {
        var pd = CreateRunningDeployment(2);
        pd.Stacks.Should().AllSatisfy(s => s.StartedAt.Should().NotBeNull());

        pd.StartRedeploy();

        pd.Stacks.Should().AllSatisfy(s => s.StartedAt.Should().BeNull());
    }

    [Fact]
    public void StartRedeploy_ClearsStackCompletedAt()
    {
        var pd = CreateRunningDeployment(2);
        pd.Stacks.Should().AllSatisfy(s => s.CompletedAt.Should().NotBeNull());

        pd.StartRedeploy();

        pd.Stacks.Should().AllSatisfy(s => s.CompletedAt.Should().BeNull());
    }

    [Fact]
    public void StartRedeploy_ClearsStackErrorMessage()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRedeploy();

        pd.Stacks.Should().AllSatisfy(s => s.ErrorMessage.Should().BeNull());
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

    private static ProductDeployment CreateStoppedDeployment()
    {
        var pd = CreateRunningDeployment(2);
        pd.MarkAsStopped("User requested stop");
        return pd;
    }

    private static ProductDeployment CreateRedeployingDeployment()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRedeploy();
        return pd;
    }

    #endregion
}

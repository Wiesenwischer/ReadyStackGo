using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ProductDeployment aggregate root.
/// Covers factory methods, state machine transitions, stack lifecycle,
/// domain events, computed properties, and edge cases.
/// </summary>
public class ProductDeploymentTests
{
    #region Factory Method - InitiateDeployment

    [Fact]
    public void InitiateDeployment_WithValidData_CreatesDeployment()
    {
        // Arrange
        var id = ProductDeploymentId.NewId();
        var envId = EnvironmentId.NewId();
        var userId = UserId.NewId();
        var configs = CreateStackConfigs(3);
        var sharedVars = new Dictionary<string, string> { { "LOG_LEVEL", "info" } };

        // Act
        var pd = ProductDeployment.InitiateDeployment(
            id, envId, "stacks:myproduct", "stacks:myproduct:1.0.0",
            "myproduct", "My Product", "1.0.0",
            userId, configs, sharedVars);

        // Assert
        pd.Id.Should().Be(id);
        pd.EnvironmentId.Should().Be(envId);
        pd.ProductGroupId.Should().Be("stacks:myproduct");
        pd.ProductId.Should().Be("stacks:myproduct:1.0.0");
        pd.ProductName.Should().Be("myproduct");
        pd.ProductDisplayName.Should().Be("My Product");
        pd.ProductVersion.Should().Be("1.0.0");
        pd.DeployedBy.Should().Be(userId);
        pd.Status.Should().Be(ProductDeploymentStatus.Deploying);
        pd.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        pd.CompletedAt.Should().BeNull();
        pd.ErrorMessage.Should().BeNull();
        pd.ContinueOnError.Should().BeTrue();
        pd.TotalStacks.Should().Be(3);
        pd.CompletedStacks.Should().Be(0);
        pd.FailedStacks.Should().Be(0);
        pd.SharedVariables.Should().ContainKey("LOG_LEVEL");
        pd.SharedVariables["LOG_LEVEL"].Should().Be("info");
        pd.PreviousVersion.Should().BeNull();
        pd.UpgradeCount.Should().Be(0);
        pd.IsInProgress.Should().BeTrue();
        pd.IsOperational.Should().BeFalse();
        pd.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void InitiateDeployment_SetsStackOrderFromConfigOrder()
    {
        var pd = CreateTestDeployment(3);

        pd.Stacks.Should().HaveCount(3);
        pd.Stacks[0].StackName.Should().Be("stack-0");
        pd.Stacks[0].Order.Should().Be(0);
        pd.Stacks[1].StackName.Should().Be("stack-1");
        pd.Stacks[1].Order.Should().Be(1);
        pd.Stacks[2].StackName.Should().Be("stack-2");
        pd.Stacks[2].Order.Should().Be(2);
    }

    [Fact]
    public void InitiateDeployment_AllStacksStartAsPending()
    {
        var pd = CreateTestDeployment(3);

        pd.Stacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    [Fact]
    public void InitiateDeployment_RaisesProductDeploymentInitiatedEvent()
    {
        var pd = CreateTestDeployment(3);

        var evt = pd.DomainEvents.OfType<ProductDeploymentInitiated>().Single();
        evt.ProductDeploymentId.Should().Be(pd.Id);
        evt.ProductName.Should().Be("testproduct");
        evt.ProductVersion.Should().Be("1.0.0");
        evt.TotalStacks.Should().Be(3);
    }

    [Fact]
    public void InitiateDeployment_WithContinueOnErrorFalse_SetsFlag()
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "gid", "pid", "name", "display", "1.0.0",
            UserId.NewId(), CreateStackConfigs(1),
            new Dictionary<string, string>(), continueOnError: false);

        pd.ContinueOnError.Should().BeFalse();
    }

    [Fact]
    public void InitiateDeployment_WithEmptyStackConfigs_ThrowsArgumentException()
    {
        var act = () => ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "gid", "pid", "name", "display", "1.0.0",
            UserId.NewId(), Array.Empty<StackDeploymentConfig>(),
            new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InitiateDeployment_WithNullProductName_ThrowsArgumentException()
    {
        var act = () => ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "gid", "pid", null!, "display", "1.0.0",
            UserId.NewId(), CreateStackConfigs(1),
            new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InitiateDeployment_WithEmptyProductVersion_ThrowsArgumentException()
    {
        var act = () => ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "gid", "pid", "name", "display", "",
            UserId.NewId(), CreateStackConfigs(1),
            new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InitiateDeployment_WithNullSharedVariables_DoesNotThrow()
    {
        var act = () => ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "gid", "pid", "name", "display", "1.0.0",
            UserId.NewId(), CreateStackConfigs(1), null!);

        act.Should().NotThrow();
    }

    [Fact]
    public void InitiateDeployment_RecordsPhaseHistory()
    {
        var pd = CreateTestDeployment(1);

        pd.PhaseHistory.Should().ContainSingle();
        pd.PhaseHistory.First().Message.Should().Contain("initiated");
    }

    #endregion

    #region Factory Method - InitiateUpgrade

    [Fact]
    public void InitiateUpgrade_WithValidData_CreatesUpgrade()
    {
        var existing = CreateRunningDeployment(2);
        var targetConfigs = CreateStackConfigs(2);

        var upgradeId = ProductDeploymentId.NewId();
        var pd = ProductDeployment.InitiateUpgrade(
            upgradeId, existing.EnvironmentId,
            existing.ProductGroupId, "stacks:testproduct:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, targetConfigs,
            new Dictionary<string, string>());

        pd.Id.Should().Be(upgradeId);
        pd.Status.Should().Be(ProductDeploymentStatus.Upgrading);
        pd.ProductVersion.Should().Be("2.0.0");
        pd.PreviousVersion.Should().Be("1.0.0");
        pd.UpgradeCount.Should().Be(1);
        pd.TotalStacks.Should().Be(2);
        pd.IsInProgress.Should().BeTrue();
    }

    [Fact]
    public void InitiateUpgrade_RaisesProductUpgradeInitiatedEvent()
    {
        var existing = CreateRunningDeployment(2);
        var targetConfigs = CreateStackConfigs(2);

        var pd = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, targetConfigs,
            new Dictionary<string, string>());

        var evt = pd.DomainEvents.OfType<ProductUpgradeInitiated>().Single();
        evt.PreviousVersion.Should().Be("1.0.0");
        evt.TargetVersion.Should().Be("2.0.0");
        evt.TotalStacks.Should().Be(2);
    }

    [Fact]
    public void InitiateUpgrade_MarksNewStacksInTarget()
    {
        var existing = CreateRunningDeployment(2); // stack-0, stack-1
        var targetConfigs = new List<StackDeploymentConfig>
        {
            new("stack-0", "Stack 0", "sid:0", 2, new Dictionary<string, string>()),
            new("stack-1", "Stack 1", "sid:1", 2, new Dictionary<string, string>()),
            new("stack-new", "Stack New", "sid:new", 1, new Dictionary<string, string>())
        };

        var pd = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, targetConfigs,
            new Dictionary<string, string>());

        pd.Stacks.Should().HaveCount(3);
        pd.Stacks.Single(s => s.StackName == "stack-new").IsNewInUpgrade.Should().BeTrue();
        pd.Stacks.Single(s => s.StackName == "stack-0").IsNewInUpgrade.Should().BeFalse();
        pd.Stacks.Single(s => s.StackName == "stack-1").IsNewInUpgrade.Should().BeFalse();
    }

    [Fact]
    public void InitiateUpgrade_WithNonOperationalExisting_ThrowsArgumentException()
    {
        var existing = CreateTestDeployment(2); // Still Deploying, not operational

        var act = () => ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, CreateStackConfigs(2),
            new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InitiateUpgrade_IncreasesUpgradeCountFromExisting()
    {
        // First upgrade
        var existing1 = CreateRunningDeployment(1);
        var upgrade1 = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing1.EnvironmentId,
            existing1.ProductGroupId, "pid:2.0.0",
            existing1.ProductName, existing1.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing1, CreateStackConfigs(1),
            new Dictionary<string, string>());

        // Complete the first upgrade to make it operational
        upgrade1.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        upgrade1.CompleteStack("stack-0");

        // Second upgrade
        var upgrade2 = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), upgrade1.EnvironmentId,
            upgrade1.ProductGroupId, "pid:3.0.0",
            upgrade1.ProductName, upgrade1.ProductDisplayName, "3.0.0",
            UserId.NewId(), upgrade1, CreateStackConfigs(1),
            new Dictionary<string, string>());

        upgrade2.UpgradeCount.Should().Be(2);
    }

    #endregion

    #region Stack Lifecycle - StartStack

    [Fact]
    public void StartStack_FromDeploying_TransitionsStackToDeploying()
    {
        var pd = CreateTestDeployment(2);
        var deploymentId = DeploymentId.NewId();

        pd.StartStack("stack-0", deploymentId, "myproduct-stack-0");

        var stack = pd.Stacks.Single(s => s.StackName == "stack-0");
        stack.Status.Should().Be(StackDeploymentStatus.Deploying);
        stack.DeploymentId.Should().Be(deploymentId);
        stack.DeploymentStackName.Should().Be("myproduct-stack-0");
        stack.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void StartStack_RaisesProductStackDeploymentStartedEvent()
    {
        var pd = CreateTestDeployment(2);
        pd.ClearDomainEvents();

        pd.StartStack("stack-0", DeploymentId.NewId(), "myproduct-stack-0");

        var evt = pd.DomainEvents.OfType<ProductStackDeploymentStarted>().Single();
        evt.ProductDeploymentId.Should().Be(pd.Id);
        evt.StackName.Should().Be("stack-0");
        evt.StackIndex.Should().Be(0);
        evt.TotalStacks.Should().Be(2);
    }

    [Fact]
    public void StartStack_WithUnknownStackName_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(1);

        var act = () => pd.StartStack("nonexistent", DeploymentId.NewId(), "test");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartStack_WhenNotDeployingOrUpgrading_ThrowsInvalidOperationException()
    {
        var pd = CreateRunningDeployment(1);

        var act = () => pd.StartStack("stack-0", DeploymentId.NewId(), "test");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartStack_AlreadyStarted_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");

        var act = () => pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0-again");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Stack Lifecycle - CompleteStack

    [Fact]
    public void CompleteStack_MarksStackAsRunning()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");

        pd.CompleteStack("stack-0");

        var stack = pd.Stacks.Single(s => s.StackName == "stack-0");
        stack.Status.Should().Be(StackDeploymentStatus.Running);
        stack.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void CompleteStack_RaisesProductStackDeploymentCompletedEvent()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        pd.ClearDomainEvents();

        pd.CompleteStack("stack-0");

        var evt = pd.DomainEvents.OfType<ProductStackDeploymentCompleted>().Single();
        evt.StackName.Should().Be("stack-0");
        evt.CompletedStacks.Should().Be(1);
        evt.TotalStacks.Should().Be(2);
    }

    [Fact]
    public void CompleteStack_WhenAllStacksComplete_CompletesDeployment()
    {
        var pd = CreateTestDeployment(2);

        // Complete all stacks
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "test-stack-1");
        pd.CompleteStack("stack-1");

        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.CompletedAt.Should().NotBeNull();
        pd.ErrorMessage.Should().BeNull();
        pd.CompletedStacks.Should().Be(2);
        pd.DomainEvents.OfType<ProductDeploymentCompleted>().Should().ContainSingle();
    }

    [Fact]
    public void CompleteStack_WhenNotAllComplete_DoesNotCompleteDeployment()
    {
        var pd = CreateTestDeployment(3);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        pd.CompleteStack("stack-0");

        pd.Status.Should().Be(ProductDeploymentStatus.Deploying);
        pd.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void CompleteStack_WhenNotStarted_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(1);

        var act = () => pd.CompleteStack("stack-0");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Stack Lifecycle - FailStack

    [Fact]
    public void FailStack_MarksStackAsFailed()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");

        pd.FailStack("stack-0", "Connection refused");

        var stack = pd.Stacks.Single(s => s.StackName == "stack-0");
        stack.Status.Should().Be(StackDeploymentStatus.Failed);
        stack.ErrorMessage.Should().Be("Connection refused");
        stack.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void FailStack_RaisesProductStackDeploymentFailedEvent()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");
        pd.ClearDomainEvents();

        pd.FailStack("stack-0", "Error");

        var evt = pd.DomainEvents.OfType<ProductStackDeploymentFailed>().Single();
        evt.StackName.Should().Be("stack-0");
        evt.ErrorMessage.Should().Be("Error");
    }

    [Fact]
    public void FailStack_WhenNotStarted_FailsFromPendingStatus()
    {
        var pd = CreateTestDeployment(1);

        pd.FailStack("stack-0", "Pre-deployment failure");

        pd.Stacks[0].Status.Should().Be(StackDeploymentStatus.Failed);
        pd.Stacks[0].ErrorMessage.Should().Be("Pre-deployment failure");
        pd.Stacks[0].CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void FailStack_FromPending_RaisesEvent()
    {
        var pd = CreateTestDeployment(1);
        pd.ClearDomainEvents();

        pd.FailStack("stack-0", "Stack not found");

        var evt = pd.DomainEvents.OfType<ProductStackDeploymentFailed>().Single();
        evt.StackName.Should().Be("stack-0");
        evt.ErrorMessage.Should().Be("Stack not found");
    }

    [Fact]
    public void FailStack_WithEmptyErrorMessage_ThrowsArgumentException()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "test-stack-0");

        var act = () => pd.FailStack("stack-0", "");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Product Lifecycle - MarkAsPartiallyRunning

    [Fact]
    public void MarkAsPartiallyRunning_WithSomeSucceededAndSomeFailed_Succeeds()
    {
        var pd = CreateTestDeployment(3);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "t-1");
        pd.FailStack("stack-1", "Error");

        pd.MarkAsPartiallyRunning("Stack stack-1 failed");

        pd.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);
        pd.CompletedAt.Should().NotBeNull();
        pd.ErrorMessage.Should().Contain("stack-1");
        pd.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void MarkAsPartiallyRunning_RaisesEvent()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "t-1");
        pd.FailStack("stack-1", "Error");
        pd.ClearDomainEvents();

        pd.MarkAsPartiallyRunning("Partial failure");

        var evt = pd.DomainEvents.OfType<ProductDeploymentPartiallyCompleted>().Single();
        evt.RunningStacks.Should().Be(1);
        evt.FailedStacks.Should().Be(1);
    }

    [Fact]
    public void MarkAsPartiallyRunning_WithNoCompletedStacks_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");

        var act = () => pd.MarkAsPartiallyRunning("All failed");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsPartiallyRunning_FromRunning_ThrowsInvalidOperationException()
    {
        var pd = CreateRunningDeployment(2);

        var act = () => pd.MarkAsPartiallyRunning("Should not work");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsPartiallyRunning_WithEmptyReason_ThrowsArgumentException()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "t-1");
        pd.FailStack("stack-1", "Error");

        var act = () => pd.MarkAsPartiallyRunning("");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Product Lifecycle - MarkAsFailed

    [Fact]
    public void MarkAsFailed_FromDeploying_SetsFailed()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");

        pd.MarkAsFailed("Critical error");

        pd.Status.Should().Be(ProductDeploymentStatus.Failed);
        pd.ErrorMessage.Should().Be("Critical error");
        pd.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_RaisesProductDeploymentFailedEvent()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");
        pd.ClearDomainEvents();

        pd.MarkAsFailed("Critical");

        var evt = pd.DomainEvents.OfType<ProductDeploymentFailed>().Single();
        evt.ErrorMessage.Should().Be("Critical");
    }

    [Fact]
    public void MarkAsFailed_FromRunning_ThrowsInvalidOperationException()
    {
        var pd = CreateRunningDeployment(1);

        var act = () => pd.MarkAsFailed("Error");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsFailed_WithEmptyMessage_ThrowsArgumentException()
    {
        var pd = CreateTestDeployment(1);

        var act = () => pd.MarkAsFailed("");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Product Lifecycle - StartRemoval

    [Fact]
    public void StartRemoval_FromRunning_TransitionsToRemoving()
    {
        var pd = CreateRunningDeployment(2);

        pd.StartRemoval();

        pd.Status.Should().Be(ProductDeploymentStatus.Removing);
        pd.CompletedAt.Should().BeNull();
        pd.ErrorMessage.Should().BeNull();
        pd.Stacks.Should().AllSatisfy(s =>
            s.Status.Should().Be(StackDeploymentStatus.Pending));
    }

    [Fact]
    public void StartRemoval_FromPartiallyRunning_TransitionsToRemoving()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "t-1");
        pd.FailStack("stack-1", "Error");
        pd.MarkAsPartiallyRunning("Partial");

        pd.StartRemoval();

        pd.Status.Should().Be(ProductDeploymentStatus.Removing);
    }

    [Fact]
    public void StartRemoval_FromFailed_TransitionsToRemoving()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");
        pd.MarkAsFailed("All failed");

        pd.StartRemoval();

        pd.Status.Should().Be(ProductDeploymentStatus.Removing);
    }

    [Fact]
    public void StartRemoval_RaisesProductRemovalInitiatedEvent()
    {
        var pd = CreateRunningDeployment(2);
        pd.ClearDomainEvents();

        pd.StartRemoval();

        var evt = pd.DomainEvents.OfType<ProductRemovalInitiated>().Single();
        evt.TotalStacks.Should().Be(2);
    }

    [Fact]
    public void StartRemoval_FromDeploying_ThrowsInvalidOperationException()
    {
        var pd = CreateTestDeployment(1);

        var act = () => pd.StartRemoval();

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Product Lifecycle - MarkStackRemoved

    [Fact]
    public void MarkStackRemoved_SingleStack_RemovesStackAndCompletesDeployment()
    {
        var pd = CreateRunningDeployment(1);
        pd.StartRemoval();

        pd.MarkStackRemoved("stack-0");

        pd.Status.Should().Be(ProductDeploymentStatus.Removed);
        pd.IsTerminal.Should().BeTrue();
        pd.CompletedAt.Should().NotBeNull();
        pd.DomainEvents.OfType<ProductDeploymentRemoved>().Should().ContainSingle();
    }

    [Fact]
    public void MarkStackRemoved_NotAllRemoved_StaysInRemoving()
    {
        var pd = CreateRunningDeployment(2);
        pd.StartRemoval();

        pd.MarkStackRemoved("stack-0");

        pd.Status.Should().Be(ProductDeploymentStatus.Removing);
        pd.RemovedStacks.Should().Be(1);
    }

    [Fact]
    public void MarkStackRemoved_AllRemoved_TransitionsToRemoved()
    {
        var pd = CreateRunningDeployment(3);
        pd.StartRemoval();

        pd.MarkStackRemoved("stack-0");
        pd.MarkStackRemoved("stack-1");
        pd.MarkStackRemoved("stack-2");

        pd.Status.Should().Be(ProductDeploymentStatus.Removed);
        pd.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void MarkStackRemoved_WhenNotRemoving_ThrowsInvalidOperationException()
    {
        var pd = CreateRunningDeployment(1);

        var act = () => pd.MarkStackRemoved("stack-0");

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region State Machine Transitions

    [Theory]
    [InlineData(ProductDeploymentStatus.Deploying, ProductDeploymentStatus.Running, true)]
    [InlineData(ProductDeploymentStatus.Deploying, ProductDeploymentStatus.PartiallyRunning, true)]
    [InlineData(ProductDeploymentStatus.Deploying, ProductDeploymentStatus.Failed, true)]
    [InlineData(ProductDeploymentStatus.Deploying, ProductDeploymentStatus.Upgrading, false)]
    [InlineData(ProductDeploymentStatus.Deploying, ProductDeploymentStatus.Removing, false)]
    [InlineData(ProductDeploymentStatus.Deploying, ProductDeploymentStatus.Removed, false)]
    [InlineData(ProductDeploymentStatus.Running, ProductDeploymentStatus.Upgrading, true)]
    [InlineData(ProductDeploymentStatus.Running, ProductDeploymentStatus.Removing, true)]
    [InlineData(ProductDeploymentStatus.Running, ProductDeploymentStatus.Deploying, false)]
    [InlineData(ProductDeploymentStatus.Running, ProductDeploymentStatus.Failed, false)]
    [InlineData(ProductDeploymentStatus.PartiallyRunning, ProductDeploymentStatus.Upgrading, true)]
    [InlineData(ProductDeploymentStatus.PartiallyRunning, ProductDeploymentStatus.Removing, true)]
    [InlineData(ProductDeploymentStatus.PartiallyRunning, ProductDeploymentStatus.Running, false)]
    [InlineData(ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.Running, true)]
    [InlineData(ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.PartiallyRunning, true)]
    [InlineData(ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.Failed, true)]
    [InlineData(ProductDeploymentStatus.Upgrading, ProductDeploymentStatus.Removing, false)]
    [InlineData(ProductDeploymentStatus.Failed, ProductDeploymentStatus.Upgrading, true)]
    [InlineData(ProductDeploymentStatus.Failed, ProductDeploymentStatus.Removing, true)]
    [InlineData(ProductDeploymentStatus.Failed, ProductDeploymentStatus.Running, false)]
    [InlineData(ProductDeploymentStatus.Removing, ProductDeploymentStatus.Removed, true)]
    [InlineData(ProductDeploymentStatus.Removing, ProductDeploymentStatus.Running, false)]
    [InlineData(ProductDeploymentStatus.Removed, ProductDeploymentStatus.Deploying, false)]
    [InlineData(ProductDeploymentStatus.Removed, ProductDeploymentStatus.Running, false)]
    [InlineData(ProductDeploymentStatus.Removed, ProductDeploymentStatus.Removing, false)]
    public void CanTransitionTo_ReturnsExpectedResult(
        ProductDeploymentStatus from, ProductDeploymentStatus to, bool expected)
    {
        var pd = CreateDeploymentInStatus(from);
        pd.CanTransitionTo(to).Should().Be(expected);
    }

    #endregion

    #region Query Properties

    [Fact]
    public void CanUpgrade_WhenRunning_ReturnsTrue()
    {
        var pd = CreateRunningDeployment(1);
        pd.CanUpgrade.Should().BeTrue();
    }

    [Fact]
    public void CanUpgrade_WhenPartiallyRunning_ReturnsTrue()
    {
        var pd = CreatePartiallyRunningDeployment();
        pd.CanUpgrade.Should().BeTrue();
    }

    [Fact]
    public void CanUpgrade_WhenDeploying_ReturnsFalse()
    {
        var pd = CreateTestDeployment(1);
        pd.CanUpgrade.Should().BeFalse();
    }

    [Fact]
    public void CanRemove_WhenRunning_ReturnsTrue()
    {
        var pd = CreateRunningDeployment(1);
        pd.CanRemove.Should().BeTrue();
    }

    [Fact]
    public void CanRemove_WhenFailed_ReturnsTrue()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");
        pd.MarkAsFailed("All failed");

        pd.CanRemove.Should().BeTrue();
    }

    [Fact]
    public void CanRemove_WhenDeploying_ReturnsFalse()
    {
        var pd = CreateTestDeployment(1);
        pd.CanRemove.Should().BeFalse();
    }

    [Fact]
    public void CanRollback_WhenFailedWithPreviousVersion_ReturnsTrue()
    {
        var existing = CreateRunningDeployment(1);
        var upgrade = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, CreateStackConfigs(1),
            new Dictionary<string, string>());

        upgrade.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        upgrade.FailStack("stack-0", "Error");
        upgrade.MarkAsFailed("All failed");

        upgrade.CanRollback.Should().BeTrue();
        upgrade.PreviousVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void CanRollback_WhenFailedWithNoPreviousVersion_ReturnsFalse()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");
        pd.MarkAsFailed("All failed");

        pd.CanRollback.Should().BeFalse();
    }

    #endregion

    #region Ordering

    [Fact]
    public void GetStacksInDeployOrder_ReturnsAscendingOrder()
    {
        var pd = CreateTestDeployment(4);

        var ordered = pd.GetStacksInDeployOrder();

        ordered.Should().HaveCount(4);
        ordered[0].Order.Should().Be(0);
        ordered[1].Order.Should().Be(1);
        ordered[2].Order.Should().Be(2);
        ordered[3].Order.Should().Be(3);
    }

    [Fact]
    public void GetStacksInRemoveOrder_ReturnsDescendingOrder()
    {
        var pd = CreateTestDeployment(4);

        var ordered = pd.GetStacksInRemoveOrder();

        ordered.Should().HaveCount(4);
        ordered[0].Order.Should().Be(3);
        ordered[1].Order.Should().Be(2);
        ordered[2].Order.Should().Be(1);
        ordered[3].Order.Should().Be(0);
    }

    #endregion

    #region Duration

    [Fact]
    public void GetDuration_WhenNotCompleted_ReturnsNull()
    {
        var pd = CreateTestDeployment(1);
        pd.GetDuration().Should().BeNull();
    }

    [Fact]
    public void GetDuration_WhenCompleted_ReturnsDuration()
    {
        var pd = CreateRunningDeployment(1);
        pd.GetDuration().Should().NotBeNull();
        pd.GetDuration()!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    #endregion

    #region Single-Stack Product

    [Fact]
    public void SingleStackProduct_DeployAndComplete_Works()
    {
        var pd = CreateTestDeployment(1);

        pd.StartStack("stack-0", DeploymentId.NewId(), "prod-stack-0");
        pd.CompleteStack("stack-0");

        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.TotalStacks.Should().Be(1);
        pd.CompletedStacks.Should().Be(1);
    }

    [Fact]
    public void SingleStackProduct_DeployAndFail_CanTransitionToFailed()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "prod-stack-0");
        pd.FailStack("stack-0", "Error");

        pd.MarkAsFailed("All stacks failed");

        pd.Status.Should().Be(ProductDeploymentStatus.Failed);
    }

    #endregion

    #region Stack Name Case Insensitivity

    [Fact]
    public void FindStack_IsCaseInsensitive()
    {
        var pd = CreateTestDeployment(1);

        // stack-0 was created with lowercase, try uppercase
        pd.StartStack("STACK-0", DeploymentId.NewId(), "prod-stack-0");

        pd.Stacks.First().Status.Should().Be(StackDeploymentStatus.Deploying);
    }

    #endregion

    #region Upgrade After Completion - CompleteStack sets LastUpgradedAt

    [Fact]
    public void CompleteAllStacks_DuringUpgrade_SetsLastUpgradedAt()
    {
        var existing = CreateRunningDeployment(1);
        var upgrade = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, CreateStackConfigs(1),
            new Dictionary<string, string>());

        upgrade.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        upgrade.CompleteStack("stack-0");

        upgrade.Status.Should().Be(ProductDeploymentStatus.Running);
        upgrade.LastUpgradedAt.Should().NotBeNull();
    }

    [Fact]
    public void CompleteAllStacks_DuringDeploy_DoesNotSetLastUpgradedAt()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");

        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.LastUpgradedAt.Should().BeNull();
    }

    #endregion

    #region PhaseHistory

    [Fact]
    public void PhaseHistory_RecordsAllPhases()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "t-1");
        pd.CompleteStack("stack-1");

        // Initiated + start-0 + complete-0 + start-1 + complete-1 + deployment-completed
        pd.PhaseHistory.Count.Should().BeGreaterThanOrEqualTo(5);
    }

    #endregion

    #region Helper Methods

    private static List<StackDeploymentConfig> CreateStackConfigs(int count)
    {
        var configs = new List<StackDeploymentConfig>();
        for (var i = 0; i < count; i++)
        {
            configs.Add(new StackDeploymentConfig(
                $"stack-{i}",
                $"Stack {i}",
                $"sid:{i}",
                2,
                new Dictionary<string, string> { { $"VAR_{i}", $"value_{i}" } }));
        }
        return configs;
    }

    private static ProductDeployment CreateTestDeployment(int stackCount)
    {
        return ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            EnvironmentId.NewId(),
            "stacks:testproduct",
            "stacks:testproduct:1.0.0",
            "testproduct",
            "Test Product",
            "1.0.0",
            UserId.NewId(),
            CreateStackConfigs(stackCount),
            new Dictionary<string, string> { { "SHARED", "value" } });
    }

    private static ProductDeployment CreateRunningDeployment(int stackCount)
    {
        var pd = CreateTestDeployment(stackCount);
        for (var i = 0; i < stackCount; i++)
        {
            pd.StartStack($"stack-{i}", DeploymentId.NewId(), $"test-stack-{i}");
            pd.CompleteStack($"stack-{i}");
        }
        return pd;
    }

    private static ProductDeployment CreatePartiallyRunningDeployment()
    {
        var pd = CreateTestDeployment(2);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.CompleteStack("stack-0");
        pd.StartStack("stack-1", DeploymentId.NewId(), "t-1");
        pd.FailStack("stack-1", "Error");
        pd.MarkAsPartiallyRunning("Partial failure");
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

    private static ProductDeployment CreateUpgradingDeployment()
    {
        var existing = CreateRunningDeployment(2);
        return ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.NewId(), existing.EnvironmentId,
            existing.ProductGroupId, "pid:2.0.0",
            existing.ProductName, existing.ProductDisplayName, "2.0.0",
            UserId.NewId(), existing, CreateStackConfigs(2),
            new Dictionary<string, string>());
    }

    private static ProductDeployment CreateFailedDeployment()
    {
        var pd = CreateTestDeployment(1);
        pd.StartStack("stack-0", DeploymentId.NewId(), "t-0");
        pd.FailStack("stack-0", "Error");
        pd.MarkAsFailed("All failed");
        return pd;
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

    #endregion
}

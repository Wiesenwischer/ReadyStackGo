using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ProductStackDeployment child entity.
/// </summary>
public class ProductStackDeploymentTests
{
    #region Construction

    [Fact]
    public void Constructor_WithValidData_CreatesEntity()
    {
        var vars = new Dictionary<string, string> { { "PORT", "8080" } };

        var stack = new ProductStackDeployment(
            "infrastructure", "Infrastructure", "sid:infra", 0, 3, vars);

        stack.StackName.Should().Be("infrastructure");
        stack.StackDisplayName.Should().Be("Infrastructure");
        stack.StackId.Should().Be("sid:infra");
        stack.Order.Should().Be(0);
        stack.ServiceCount.Should().Be(3);
        stack.Status.Should().Be(StackDeploymentStatus.Pending);
        stack.DeploymentId.Should().BeNull();
        stack.DeploymentStackName.Should().BeNull();
        stack.StartedAt.Should().BeNull();
        stack.CompletedAt.Should().BeNull();
        stack.ErrorMessage.Should().BeNull();
        stack.IsNewInUpgrade.Should().BeFalse();
        stack.Variables.Should().ContainKey("PORT");
    }

    [Fact]
    public void Constructor_WithIsNewInUpgrade_SetsFlag()
    {
        var stack = new ProductStackDeployment(
            "new-stack", "New Stack", "sid:new", 5, 1,
            new Dictionary<string, string>(), isNewInUpgrade: true);

        stack.IsNewInUpgrade.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptyStackName_ThrowsArgumentException()
    {
        var act = () => new ProductStackDeployment(
            "", "Display", "sid", 0, 1, new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyDisplayName_ThrowsArgumentException()
    {
        var act = () => new ProductStackDeployment(
            "name", "", "sid", 0, 1, new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyStackId_ThrowsArgumentException()
    {
        var act = () => new ProductStackDeployment(
            "name", "Display", "", 0, 1, new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeOrder_ThrowsArgumentException()
    {
        var act = () => new ProductStackDeployment(
            "name", "Display", "sid", -1, 1, new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeServiceCount_ThrowsArgumentException()
    {
        var act = () => new ProductStackDeployment(
            "name", "Display", "sid", 0, -1, new Dictionary<string, string>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullVariables_DoesNotThrow()
    {
        var act = () => new ProductStackDeployment(
            "name", "Display", "sid", 0, 1, null!);

        act.Should().NotThrow();
    }

    #endregion

    #region Start

    [Fact]
    public void Start_FromPending_TransitionsToDeploying()
    {
        var stack = CreateStack();
        var deploymentId = DeploymentId.NewId();

        stack.Start(deploymentId, "myproduct-infra");

        stack.Status.Should().Be(StackDeploymentStatus.Deploying);
        stack.DeploymentId.Should().Be(deploymentId);
        stack.DeploymentStackName.Should().Be("myproduct-infra");
        stack.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_AlreadyDeploying_ThrowsInvalidOperationException()
    {
        var stack = CreateStack();
        stack.Start(DeploymentId.NewId(), "name-1");

        var act = () => stack.Start(DeploymentId.NewId(), "name-2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Start_WithNullDeploymentId_ThrowsArgumentNullException()
    {
        var stack = CreateStack();

        var act = () => stack.Start(null!, "name");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Start_WithEmptyDeploymentStackName_ThrowsArgumentException()
    {
        var stack = CreateStack();

        var act = () => stack.Start(DeploymentId.NewId(), "");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Complete

    [Fact]
    public void Complete_FromDeploying_TransitionsToRunning()
    {
        var stack = CreateStartedStack();

        stack.Complete();

        stack.Status.Should().Be(StackDeploymentStatus.Running);
        stack.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_FromPending_ThrowsInvalidOperationException()
    {
        var stack = CreateStack();

        var act = () => stack.Complete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_FromFailed_ThrowsInvalidOperationException()
    {
        var stack = CreateStartedStack();
        stack.Fail("Error");

        var act = () => stack.Complete();

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_FromDeploying_TransitionsToFailed()
    {
        var stack = CreateStartedStack();

        stack.Fail("Connection refused");

        stack.Status.Should().Be(StackDeploymentStatus.Failed);
        stack.ErrorMessage.Should().Be("Connection refused");
        stack.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_FromPending_ThrowsInvalidOperationException()
    {
        var stack = CreateStack();

        var act = () => stack.Fail("Error");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_WithEmptyMessage_ThrowsArgumentException()
    {
        var stack = CreateStartedStack();

        var act = () => stack.Fail("");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region MarkRemoved

    [Fact]
    public void MarkRemoved_SetsStatusToRemoved()
    {
        var stack = CreateStack();

        stack.MarkRemoved();

        stack.Status.Should().Be(StackDeploymentStatus.Removed);
        stack.CompletedAt.Should().NotBeNull();
    }

    #endregion

    #region ResetToPending

    [Fact]
    public void ResetToPending_ClearsAllTransientState()
    {
        var stack = CreateStartedStack();
        stack.Fail("Error");

        stack.ResetToPending();

        stack.Status.Should().Be(StackDeploymentStatus.Pending);
        stack.StartedAt.Should().BeNull();
        stack.CompletedAt.Should().BeNull();
        stack.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region GetDuration

    [Fact]
    public void GetDuration_WhenNotStarted_ReturnsNull()
    {
        var stack = CreateStack();
        stack.GetDuration().Should().BeNull();
    }

    [Fact]
    public void GetDuration_WhenStarted_ReturnsDuration()
    {
        var stack = CreateStartedStack();
        stack.GetDuration().Should().NotBeNull();
    }

    [Fact]
    public void GetDuration_WhenCompleted_ReturnsDuration()
    {
        var stack = CreateStartedStack();
        stack.Complete();

        stack.GetDuration().Should().NotBeNull();
        stack.GetDuration()!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    #endregion

    #region Helper Methods

    private static ProductStackDeployment CreateStack()
    {
        return new ProductStackDeployment(
            "infrastructure", "Infrastructure", "sid:infra",
            0, 3, new Dictionary<string, string> { { "PORT", "8080" } });
    }

    private static ProductStackDeployment CreateStartedStack()
    {
        var stack = CreateStack();
        stack.Start(DeploymentId.NewId(), "myproduct-infra");
        return stack;
    }

    #endregion
}

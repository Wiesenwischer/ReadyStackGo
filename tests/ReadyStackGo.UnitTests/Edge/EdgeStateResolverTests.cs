using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Exhaustive state→edge-mode mapping (locked decision §5). No health-guessing: the only
/// inputs are the authoritative deploy status, the operation mode and the maintenance trigger.
/// </summary>
public class EdgeStateResolverTests
{
    [Fact]
    public void Running_AndNormal_Proxies()
    {
        var state = EdgeStateResolver.Resolve(ProductDeploymentStatus.Running, OperationMode.Normal, null, "1.2.3");

        state.Mode.Should().Be(EdgeMode.Proxy);
        state.StatusState.Should().Be(EdgeStatusState.Running);
        state.StatusStateToken.Should().Be("running");
        state.PlannedMaintenance.Should().BeFalse();
        state.ProductVersion.Should().Be("1.2.3");
    }

    [Fact]
    public void Running_ButMaintenanceFlag_ServesPlannedMaintenance()
    {
        var trigger = MaintenanceTrigger.Manual("Scheduled DB upgrade", "admin");

        var state = EdgeStateResolver.Resolve(ProductDeploymentStatus.Running, OperationMode.Maintenance, trigger);

        state.Mode.Should().Be(EdgeMode.Maintenance);
        state.StatusState.Should().Be(EdgeStatusState.Maintenance);
        state.PlannedMaintenance.Should().BeTrue("the maintenance flag means planned wording");
        state.Reason.Should().Be("Scheduled DB upgrade");
    }

    [Theory]
    [InlineData(ProductDeploymentStatus.Deploying)]
    [InlineData(ProductDeploymentStatus.Redeploying)]
    [InlineData(ProductDeploymentStatus.Upgrading)]
    public void InProgress_ServesDeploying(ProductDeploymentStatus status)
    {
        var state = EdgeStateResolver.Resolve(status, OperationMode.Normal, null);

        state.Mode.Should().Be(EdgeMode.Maintenance);
        state.StatusState.Should().Be(EdgeStatusState.Deploying);
        state.StatusStateToken.Should().Be("deploying");
        state.PlannedMaintenance.Should().BeFalse("a redeploy is not planned maintenance — it's temporary unavailability");
    }

    [Theory]
    [InlineData(ProductDeploymentStatus.Failed)]
    [InlineData(ProductDeploymentStatus.Stopped)]
    [InlineData(ProductDeploymentStatus.PartiallyRunning)]
    [InlineData(ProductDeploymentStatus.Removing)]
    public void DownOrDegraded_AndNormal_ServesTemporarilyUnavailable(ProductDeploymentStatus status)
    {
        var state = EdgeStateResolver.Resolve(status, OperationMode.Normal, null);

        state.Mode.Should().Be(EdgeMode.Maintenance);
        state.StatusState.Should().Be(EdgeStatusState.Maintenance);
        state.PlannedMaintenance.Should().BeFalse("no flag → temporarily unavailable, not planned");
    }

    [Fact]
    public void MaintenanceFlag_WinsOverInProgressStatus_ForWording()
    {
        // Even mid-redeploy, an explicit maintenance flag means planned wording.
        var trigger = MaintenanceTrigger.Observer("external flag set");

        var state = EdgeStateResolver.Resolve(ProductDeploymentStatus.Redeploying, OperationMode.Maintenance, trigger);

        state.Mode.Should().Be(EdgeMode.Maintenance);
        state.PlannedMaintenance.Should().BeTrue();
        state.Reason.Should().Be("external flag set");
    }
}

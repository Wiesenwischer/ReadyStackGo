using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;

namespace ReadyStackGo.UnitTests.Application.Services;

/// <summary>
/// The selection predicate behind maintenance stop/start. The states that matter are the ones the
/// old "state == running" check silently skipped — a cycling container reports <c>restarting</c>,
/// not <c>running</c>, and kept its database connections through the whole maintenance window.
/// </summary>
public class MaintenanceContainerFilterTests
{
    private static ContainerDto Container(
        string state,
        string? maintenanceLabel = null,
        string? stackLabel = null,
        string id = "c1")
    {
        var labels = new Dictionary<string, string>();
        if (maintenanceLabel != null) labels["rsgo.maintenance"] = maintenanceLabel;
        if (stackLabel != null) labels["rsgo.stack"] = stackLabel;

        return new ContainerDto
        {
            Id = id,
            Name = id,
            Image = "test:1.0",
            State = state,
            Status = state,
            Labels = labels
        };
    }

    [Theory]
    [InlineData("running")]
    [InlineData("restarting")]
    [InlineData("paused")]
    [InlineData("created")]
    public void ShouldStop_LiveState_IsTrue(string state)
    {
        MaintenanceContainerFilter.ShouldStop(Container(state)).Should().BeTrue(
            "a container in state '{0}' still holds resources and must be stopped", state);
    }

    [Theory]
    [InlineData("exited")]
    [InlineData("dead")]
    [InlineData("removing")]
    public void ShouldStop_AlreadyGoneOrGoing_IsFalse(string state)
    {
        MaintenanceContainerFilter.ShouldStop(Container(state)).Should().BeFalse();
    }

    [Theory]
    [InlineData("RUNNING")]
    [InlineData("Exited")]
    public void ShouldStop_IsCaseInsensitive(string state)
    {
        // Docker reports lowercase, but nothing in the contract guarantees it.
        var expected = !state.Equals("Exited", StringComparison.OrdinalIgnoreCase);

        MaintenanceContainerFilter.ShouldStop(Container(state)).Should().Be(expected);
    }

    [Theory]
    [InlineData("running")]
    [InlineData("restarting")]
    public void ShouldStop_ExemptContainer_IsFalse(string state)
    {
        // The edge proxy and product-contributed maintenance containers must survive the window.
        MaintenanceContainerFilter.ShouldStop(Container(state, maintenanceLabel: "ignore"))
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldStop_ExemptLabelIsCaseInsensitive()
    {
        MaintenanceContainerFilter.ShouldStop(Container("running", maintenanceLabel: "Ignore"))
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldStop_UnknownMaintenanceLabelValue_IsTrue()
    {
        // Only "ignore" exempts. Anything else is a normal container.
        MaintenanceContainerFilter.ShouldStop(Container("running", maintenanceLabel: "whatever"))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("exited", true)]
    [InlineData("created", true)]
    [InlineData("running", false)]
    [InlineData("dead", false)]
    public void ShouldStart_OnlyStartsStartableStates(string state, bool expected)
    {
        MaintenanceContainerFilter.ShouldStart(Container(state)).Should().Be(expected);
    }

    [Fact]
    public void ShouldStart_ExemptContainer_IsFalse()
    {
        MaintenanceContainerFilter.ShouldStart(Container("exited", maintenanceLabel: "ignore"))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("exited", true)]
    [InlineData("dead", true)]
    [InlineData("running", false)]
    [InlineData("restarting", false)]
    [InlineData("removing", false)]
    public void IsStopped_OnlyTerminalStates(string state, bool expected)
    {
        MaintenanceContainerFilter.IsStopped(Container(state)).Should().Be(expected);
    }

    [Fact]
    public void BelongsToStack_MatchesExactLabel()
    {
        var container = Container("running", stackLabel: "prod-web");

        MaintenanceContainerFilter.BelongsToStack(container, "prod-web").Should().BeTrue();
        MaintenanceContainerFilter.BelongsToStack(container, "prod-web-2").Should().BeFalse();
        MaintenanceContainerFilter.BelongsToStack(container, "prod").Should().BeFalse();
    }

    [Fact]
    public void BelongsToStack_WithoutLabel_IsFalse()
    {
        MaintenanceContainerFilter.BelongsToStack(Container("running"), "prod-web").Should().BeFalse();
    }
}

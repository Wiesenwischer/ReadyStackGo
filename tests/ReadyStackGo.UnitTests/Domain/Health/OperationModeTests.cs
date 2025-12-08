using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Domain.Health;

/// <summary>
/// Unit tests for OperationMode smart enum.
/// Tests behavior encapsulation and state transitions.
/// </summary>
public class OperationModeTests
{
    #region Static Instances

    [Fact]
    public void OperationMode_HasFiveDefinedModes()
    {
        var allModes = OperationMode.GetAll().ToList();

        allModes.Should().HaveCount(5);
        allModes.Should().Contain(OperationMode.Normal);
        allModes.Should().Contain(OperationMode.Migrating);
        allModes.Should().Contain(OperationMode.Maintenance);
        allModes.Should().Contain(OperationMode.Stopped);
        allModes.Should().Contain(OperationMode.Failed);
    }

    [Fact]
    public void OperationMode_Values_AreUnique()
    {
        var allModes = OperationMode.GetAll();
        var values = allModes.Select(m => m.Value).ToList();

        values.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region IsAvailable

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Migrating", false)]
    [InlineData("Maintenance", false)]
    [InlineData("Stopped", false)]
    [InlineData("Failed", false)]
    public void IsAvailable_ReturnsCorrectValue(string modeName, bool expectedAvailable)
    {
        var mode = OperationMode.FromName(modeName);

        mode.IsAvailable.Should().Be(expectedAvailable);
    }

    #endregion

    #region AllowsDeployment

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Migrating", false)]
    [InlineData("Maintenance", false)]
    [InlineData("Stopped", false)]
    [InlineData("Failed", true)]  // Can retry after failure
    public void AllowsDeployment_ReturnsCorrectValue(string modeName, bool expectedAllows)
    {
        var mode = OperationMode.FromName(modeName);

        mode.AllowsDeployment.Should().Be(expectedAllows);
    }

    #endregion

    #region CanStart / CanStop

    [Theory]
    [InlineData("Normal", false)]
    [InlineData("Migrating", false)]
    [InlineData("Maintenance", false)]
    [InlineData("Stopped", true)]
    [InlineData("Failed", true)]
    public void CanStart_ReturnsCorrectValue(string modeName, bool expectedCanStart)
    {
        var mode = OperationMode.FromName(modeName);

        mode.CanStart.Should().Be(expectedCanStart);
    }

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Migrating", true)]
    [InlineData("Maintenance", true)]
    [InlineData("Stopped", false)]
    [InlineData("Failed", false)]
    public void CanStop_ReturnsCorrectValue(string modeName, bool expectedCanStop)
    {
        var mode = OperationMode.FromName(modeName);

        mode.CanStop.Should().Be(expectedCanStop);
    }

    #endregion

    #region RequiresAttention

    [Theory]
    [InlineData("Normal", false)]
    [InlineData("Migrating", false)]
    [InlineData("Maintenance", false)]
    [InlineData("Stopped", false)]
    [InlineData("Failed", true)]
    public void RequiresAttention_ReturnsCorrectValue(string modeName, bool expectedAttention)
    {
        var mode = OperationMode.FromName(modeName);

        mode.RequiresAttention.Should().Be(expectedAttention);
    }

    #endregion

    #region ExpectsDegradedHealth

    [Theory]
    [InlineData("Normal", false)]
    [InlineData("Migrating", true)]
    [InlineData("Maintenance", true)]
    [InlineData("Stopped", true)]
    [InlineData("Failed", false)]
    public void ExpectsDegradedHealth_ReturnsCorrectValue(string modeName, bool expectedDegraded)
    {
        var mode = OperationMode.FromName(modeName);

        mode.ExpectsDegradedHealth.Should().Be(expectedDegraded);
    }

    #endregion

    #region IsPlannedRestriction

    [Theory]
    [InlineData("Normal", false)]
    [InlineData("Migrating", true)]
    [InlineData("Maintenance", true)]
    [InlineData("Stopped", true)]
    [InlineData("Failed", false)]
    public void IsPlannedRestriction_ReturnsCorrectValue(string modeName, bool expectedPlanned)
    {
        var mode = OperationMode.FromName(modeName);

        mode.IsPlannedRestriction.Should().Be(expectedPlanned);
    }

    #endregion

    #region MinimumHealthStatus

    [Fact]
    public void MinimumHealthStatus_Normal_IsHealthy()
    {
        OperationMode.Normal.MinimumHealthStatus.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void MinimumHealthStatus_Migrating_IsDegraded()
    {
        OperationMode.Migrating.MinimumHealthStatus.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void MinimumHealthStatus_Maintenance_IsDegraded()
    {
        OperationMode.Maintenance.MinimumHealthStatus.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void MinimumHealthStatus_Stopped_IsDegraded()
    {
        OperationMode.Stopped.MinimumHealthStatus.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void MinimumHealthStatus_Failed_IsUnhealthy()
    {
        OperationMode.Failed.MinimumHealthStatus.Should().Be(HealthStatus.Unhealthy);
    }

    #endregion

    #region CssColorClass

    [Theory]
    [InlineData("Normal", "green")]
    [InlineData("Migrating", "blue")]
    [InlineData("Maintenance", "yellow")]
    [InlineData("Stopped", "gray")]
    [InlineData("Failed", "red")]
    public void CssColorClass_ReturnsCorrectColor(string modeName, string expectedColor)
    {
        var mode = OperationMode.FromName(modeName);

        mode.CssColorClass.Should().Be(expectedColor);
    }

    #endregion

    #region Icon

    [Fact]
    public void Icon_AllModesHaveIcons()
    {
        foreach (var mode in OperationMode.GetAll())
        {
            mode.Icon.Should().NotBeNullOrEmpty($"Mode {mode.Name} should have an icon");
        }
    }

    #endregion

    #region State Transitions

    [Fact]
    public void Normal_CanTransitionTo_MigratingMaintenanceStopped()
    {
        var validTransitions = OperationMode.Normal.GetValidTransitions().ToList();

        validTransitions.Should().Contain(OperationMode.Migrating);
        validTransitions.Should().Contain(OperationMode.Maintenance);
        validTransitions.Should().Contain(OperationMode.Stopped);
        validTransitions.Should().NotContain(OperationMode.Failed);
        validTransitions.Should().NotContain(OperationMode.Normal);
    }

    [Fact]
    public void Migrating_CanTransitionTo_NormalOrFailed()
    {
        var validTransitions = OperationMode.Migrating.GetValidTransitions().ToList();

        validTransitions.Should().Contain(OperationMode.Normal);
        validTransitions.Should().Contain(OperationMode.Failed);
        validTransitions.Should().HaveCount(2);
    }

    [Fact]
    public void Maintenance_CanTransitionTo_NormalOnly()
    {
        var validTransitions = OperationMode.Maintenance.GetValidTransitions().ToList();

        validTransitions.Should().ContainSingle();
        validTransitions.Should().Contain(OperationMode.Normal);
    }

    [Fact]
    public void Stopped_CanTransitionTo_NormalOnly()
    {
        var validTransitions = OperationMode.Stopped.GetValidTransitions().ToList();

        validTransitions.Should().ContainSingle();
        validTransitions.Should().Contain(OperationMode.Normal);
    }

    [Fact]
    public void Failed_CanTransitionTo_NormalOrMigrating()
    {
        var validTransitions = OperationMode.Failed.GetValidTransitions().ToList();

        validTransitions.Should().Contain(OperationMode.Normal);
        validTransitions.Should().Contain(OperationMode.Migrating);
        validTransitions.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("Normal", "Migrating", true)]
    [InlineData("Normal", "Maintenance", true)]
    [InlineData("Normal", "Stopped", true)]
    [InlineData("Normal", "Failed", false)]
    [InlineData("Migrating", "Normal", true)]
    [InlineData("Migrating", "Failed", true)]
    [InlineData("Migrating", "Stopped", false)]
    [InlineData("Stopped", "Normal", true)]
    [InlineData("Stopped", "Migrating", false)]
    [InlineData("Failed", "Normal", true)]
    [InlineData("Failed", "Migrating", true)]
    public void CanTransitionTo_ReturnsCorrectValue(string from, string to, bool expected)
    {
        var fromMode = OperationMode.FromName(from);
        var toMode = OperationMode.FromName(to);

        fromMode.CanTransitionTo(toMode).Should().Be(expected);
    }

    #endregion

    #region FromValue / FromName

    [Theory]
    [InlineData(0, "Normal")]
    [InlineData(1, "Migrating")]
    [InlineData(2, "Maintenance")]
    [InlineData(3, "Stopped")]
    [InlineData(4, "Failed")]
    public void FromValue_ReturnsCorrectMode(int value, string expectedName)
    {
        var mode = OperationMode.FromValue(value);

        mode.Name.Should().Be(expectedName);
    }

    [Fact]
    public void FromValue_InvalidValue_ThrowsArgumentException()
    {
        var act = () => OperationMode.FromValue(999);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("normal", "Normal")]
    [InlineData("MIGRATING", "Migrating")]
    [InlineData("Maintenance", "Maintenance")]
    public void FromName_IsCaseInsensitive(string input, string expectedName)
    {
        var mode = OperationMode.FromName(input);

        mode.Name.Should().Be(expectedName);
    }

    [Fact]
    public void FromName_InvalidName_ThrowsArgumentException()
    {
        var act = () => OperationMode.FromName("InvalidMode");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameMode_AreEqual()
    {
        var mode1 = OperationMode.Normal;
        var mode2 = OperationMode.Normal;

        (mode1 == mode2).Should().BeTrue();
        mode1.Equals(mode2).Should().BeTrue();
        mode1.GetHashCode().Should().Be(mode2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentModes_AreNotEqual()
    {
        var mode1 = OperationMode.Normal;
        var mode2 = OperationMode.Failed;

        (mode1 == mode2).Should().BeFalse();
        (mode1 != mode2).Should().BeTrue();
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ReturnsName()
    {
        OperationMode.Normal.ToString().Should().Be("Normal");
        OperationMode.Failed.ToString().Should().Be("Failed");
    }

    #endregion

    #region Implicit Conversion

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        int value = OperationMode.Migrating;

        value.Should().Be(1);
    }

    #endregion
}

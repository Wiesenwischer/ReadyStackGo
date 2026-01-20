using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Domain.Health;

/// <summary>
/// Unit tests for OperationMode smart enum.
/// Tests behavior encapsulation and state transitions.
/// OperationMode is simplified to only Normal and Maintenance.
/// </summary>
public class OperationModeTests
{
    #region Static Instances

    [Fact]
    public void OperationMode_HasTwoDefinedModes()
    {
        var allModes = OperationMode.GetAll().ToList();

        allModes.Should().HaveCount(2);
        allModes.Should().Contain(OperationMode.Normal);
        allModes.Should().Contain(OperationMode.Maintenance);
    }

    [Fact]
    public void OperationMode_Values_AreUnique()
    {
        var allModes = OperationMode.GetAll();
        var values = allModes.Select(m => m.Value).ToList();

        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Normal_HasValueZero()
    {
        OperationMode.Normal.Value.Should().Be(0);
    }

    [Fact]
    public void Maintenance_HasValueOne()
    {
        OperationMode.Maintenance.Value.Should().Be(1);
    }

    #endregion

    #region IsAvailable

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Maintenance", false)]
    public void IsAvailable_ReturnsCorrectValue(string modeName, bool expectedAvailable)
    {
        var mode = OperationMode.FromName(modeName);

        mode.IsAvailable.Should().Be(expectedAvailable);
    }

    #endregion

    #region ExpectsDegradedHealth

    [Theory]
    [InlineData("Normal", false)]
    [InlineData("Maintenance", true)]
    public void ExpectsDegradedHealth_ReturnsCorrectValue(string modeName, bool expectedDegraded)
    {
        var mode = OperationMode.FromName(modeName);

        mode.ExpectsDegradedHealth.Should().Be(expectedDegraded);
    }

    #endregion

    #region MinimumHealthStatus

    [Fact]
    public void MinimumHealthStatus_Normal_IsHealthy()
    {
        OperationMode.Normal.MinimumHealthStatus.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void MinimumHealthStatus_Maintenance_IsDegraded()
    {
        OperationMode.Maintenance.MinimumHealthStatus.Should().Be(HealthStatus.Degraded);
    }

    #endregion

    #region State Transitions

    [Fact]
    public void Normal_CanTransitionTo_Maintenance()
    {
        var validTransitions = OperationMode.Normal.GetValidTransitions().ToList();

        validTransitions.Should().ContainSingle();
        validTransitions.Should().Contain(OperationMode.Maintenance);
    }

    [Fact]
    public void Maintenance_CanTransitionTo_Normal()
    {
        var validTransitions = OperationMode.Maintenance.GetValidTransitions().ToList();

        validTransitions.Should().ContainSingle();
        validTransitions.Should().Contain(OperationMode.Normal);
    }

    [Theory]
    [InlineData("Normal", "Maintenance", true)]
    [InlineData("Normal", "Normal", false)]
    [InlineData("Maintenance", "Normal", true)]
    [InlineData("Maintenance", "Maintenance", false)]
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
    [InlineData(1, "Maintenance")]
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
    [InlineData("NORMAL", "Normal")]
    [InlineData("Maintenance", "Maintenance")]
    [InlineData("maintenance", "Maintenance")]
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

    [Fact]
    public void FromName_OldModeNames_ThrowArgumentException()
    {
        // Old mode names from previous implementation should not work
        var act1 = () => OperationMode.FromName("Migrating");
        var act2 = () => OperationMode.FromName("Stopped");
        var act3 = () => OperationMode.FromName("Failed");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
        act3.Should().Throw<ArgumentException>();
    }

    #endregion

    #region TryFromValue / TryFromName

    [Theory]
    [InlineData(0, true, "Normal")]
    [InlineData(1, true, "Maintenance")]
    [InlineData(2, false, null)]
    [InlineData(999, false, null)]
    public void TryFromValue_ReturnsExpectedResult(int value, bool expectedSuccess, string? expectedName)
    {
        var success = OperationMode.TryFromValue(value, out var mode);

        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            mode.Should().NotBeNull();
            mode!.Name.Should().Be(expectedName);
        }
    }

    [Theory]
    [InlineData("Normal", true, "Normal")]
    [InlineData("Maintenance", true, "Maintenance")]
    [InlineData("Invalid", false, null)]
    public void TryFromName_ReturnsExpectedResult(string name, bool expectedSuccess, string? expectedName)
    {
        var success = OperationMode.TryFromName(name, out var mode);

        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            mode.Should().NotBeNull();
            mode!.Name.Should().Be(expectedName);
        }
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
        var mode2 = OperationMode.Maintenance;

        (mode1 == mode2).Should().BeFalse();
        (mode1 != mode2).Should().BeTrue();
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ReturnsName()
    {
        OperationMode.Normal.ToString().Should().Be("Normal");
        OperationMode.Maintenance.ToString().Should().Be("Maintenance");
    }

    #endregion

    #region Implicit Conversion

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        int normalValue = OperationMode.Normal;
        int maintenanceValue = OperationMode.Maintenance;

        normalValue.Should().Be(0);
        maintenanceValue.Should().Be(1);
    }

    #endregion

    #region Description

    [Fact]
    public void Description_Normal_HasDescription()
    {
        OperationMode.Normal.Description.Should().NotBeNullOrEmpty();
        OperationMode.Normal.Description.Should().Contain("Normal");
    }

    [Fact]
    public void Description_Maintenance_HasDescription()
    {
        OperationMode.Maintenance.Description.Should().NotBeNullOrEmpty();
        OperationMode.Maintenance.Description.Should().Contain("maintenance");
    }

    #endregion
}

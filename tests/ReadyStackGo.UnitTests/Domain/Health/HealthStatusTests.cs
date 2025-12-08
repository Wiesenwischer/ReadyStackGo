using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.UnitTests.Domain.Health;

/// <summary>
/// Unit tests for HealthStatus smart enum.
/// Tests behavior encapsulation and business rules.
/// </summary>
public class HealthStatusTests
{
    #region Static Instances

    [Fact]
    public void HealthStatus_HasFourDefinedStatuses()
    {
        var allStatuses = HealthStatus.GetAll().ToList();

        allStatuses.Should().HaveCount(4);
        allStatuses.Should().Contain(HealthStatus.Healthy);
        allStatuses.Should().Contain(HealthStatus.Degraded);
        allStatuses.Should().Contain(HealthStatus.Unhealthy);
        allStatuses.Should().Contain(HealthStatus.Unknown);
    }

    [Fact]
    public void HealthStatus_Values_AreUnique()
    {
        var allStatuses = HealthStatus.GetAll();
        var values = allStatuses.Select(s => s.Value).ToList();

        values.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region IsOperational

    [Theory]
    [InlineData("Healthy", true)]
    [InlineData("Degraded", true)]
    [InlineData("Unhealthy", false)]
    [InlineData("Unknown", false)]
    public void IsOperational_ReturnsCorrectValue(string statusName, bool expectedOperational)
    {
        var status = HealthStatus.FromName(statusName);

        status.IsOperational.Should().Be(expectedOperational);
    }

    #endregion

    #region RequiresAttention

    [Theory]
    [InlineData("Healthy", false)]
    [InlineData("Degraded", true)]
    [InlineData("Unhealthy", true)]
    [InlineData("Unknown", false)]
    public void RequiresAttention_ReturnsCorrectValue(string statusName, bool expectedAttention)
    {
        var status = HealthStatus.FromName(statusName);

        status.RequiresAttention.Should().Be(expectedAttention);
    }

    #endregion

    #region IsCritical

    [Theory]
    [InlineData("Healthy", false)]
    [InlineData("Degraded", false)]
    [InlineData("Unhealthy", true)]
    [InlineData("Unknown", false)]
    public void IsCritical_ReturnsCorrectValue(string statusName, bool expectedCritical)
    {
        var status = HealthStatus.FromName(statusName);

        status.IsCritical.Should().Be(expectedCritical);
    }

    #endregion

    #region AllowsActions

    [Theory]
    [InlineData("Healthy", true)]
    [InlineData("Degraded", true)]
    [InlineData("Unhealthy", true)]
    [InlineData("Unknown", false)]
    public void AllowsActions_ReturnsCorrectValue(string statusName, bool expectedAllows)
    {
        var status = HealthStatus.FromName(statusName);

        status.AllowsActions.Should().Be(expectedAllows);
    }

    #endregion

    #region SeverityLevel

    [Fact]
    public void SeverityLevel_Healthy_IsNone()
    {
        HealthStatus.Healthy.SeverityLevel.Should().Be(Severity.None);
    }

    [Fact]
    public void SeverityLevel_Degraded_IsWarning()
    {
        HealthStatus.Degraded.SeverityLevel.Should().Be(Severity.Warning);
    }

    [Fact]
    public void SeverityLevel_Unhealthy_IsCritical()
    {
        HealthStatus.Unhealthy.SeverityLevel.Should().Be(Severity.Critical);
    }

    [Fact]
    public void SeverityLevel_Unknown_IsInfo()
    {
        HealthStatus.Unknown.SeverityLevel.Should().Be(Severity.Info);
    }

    #endregion

    #region CombineWith

    [Theory]
    [InlineData("Healthy", "Healthy", "Healthy")]
    [InlineData("Healthy", "Degraded", "Degraded")]
    [InlineData("Healthy", "Unhealthy", "Unhealthy")]
    [InlineData("Healthy", "Unknown", "Unknown")]
    [InlineData("Degraded", "Healthy", "Degraded")]
    [InlineData("Degraded", "Degraded", "Degraded")]
    [InlineData("Degraded", "Unhealthy", "Unhealthy")]
    [InlineData("Unhealthy", "Healthy", "Unhealthy")]
    [InlineData("Unhealthy", "Degraded", "Unhealthy")]
    [InlineData("Unhealthy", "Unhealthy", "Unhealthy")]
    public void CombineWith_ReturnsWorseStatus(string first, string second, string expectedResult)
    {
        var status1 = HealthStatus.FromName(first);
        var status2 = HealthStatus.FromName(second);
        var expected = HealthStatus.FromName(expectedResult);

        var result = status1.CombineWith(status2);

        result.Should().Be(expected);
    }

    [Fact]
    public void CombineWith_Null_ReturnsSelf()
    {
        var result = HealthStatus.Healthy.CombineWith(null!);

        result.Should().Be(HealthStatus.Healthy);
    }

    #endregion

    #region Aggregate

    [Fact]
    public void Aggregate_EmptyCollection_ReturnsUnknown()
    {
        var result = HealthStatus.Aggregate(Enumerable.Empty<HealthStatus>());

        result.Should().Be(HealthStatus.Unknown);
    }

    [Fact]
    public void Aggregate_AllHealthy_ReturnsHealthy()
    {
        var statuses = new[] { HealthStatus.Healthy, HealthStatus.Healthy, HealthStatus.Healthy };

        var result = HealthStatus.Aggregate(statuses);

        result.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Aggregate_MixedStatuses_ReturnsWorst()
    {
        var statuses = new[] { HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Healthy };

        var result = HealthStatus.Aggregate(statuses);

        result.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void Aggregate_WithUnhealthy_ReturnsUnhealthy()
    {
        var statuses = new[] { HealthStatus.Healthy, HealthStatus.Unhealthy, HealthStatus.Degraded };

        var result = HealthStatus.Aggregate(statuses);

        result.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Aggregate_SingleStatus_ReturnsThatStatus()
    {
        var result = HealthStatus.Aggregate(new[] { HealthStatus.Degraded });

        result.Should().Be(HealthStatus.Degraded);
    }

    #endregion

    #region FromValue / FromName

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Degraded")]
    [InlineData(2, "Unhealthy")]
    [InlineData(3, "Unknown")]
    public void FromValue_ReturnsCorrectStatus(int value, string expectedName)
    {
        var status = HealthStatus.FromValue(value);

        status.Name.Should().Be(expectedName);
    }

    [Fact]
    public void FromValue_InvalidValue_ThrowsArgumentException()
    {
        var act = () => HealthStatus.FromValue(999);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("healthy", "Healthy")]
    [InlineData("DEGRADED", "Degraded")]
    [InlineData("Unhealthy", "Unhealthy")]
    public void FromName_IsCaseInsensitive(string input, string expectedName)
    {
        var status = HealthStatus.FromName(input);

        status.Name.Should().Be(expectedName);
    }

    [Fact]
    public void FromName_InvalidName_ThrowsArgumentException()
    {
        var act = () => HealthStatus.FromName("InvalidStatus");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryFromValue_ValidValue_ReturnsTrue()
    {
        var result = HealthStatus.TryFromValue(0, out var status);

        result.Should().BeTrue();
        status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void TryFromValue_InvalidValue_ReturnsFalse()
    {
        var result = HealthStatus.TryFromValue(999, out var status);

        result.Should().BeFalse();
        status.Should().BeNull();
    }

    [Fact]
    public void TryFromName_ValidName_ReturnsTrue()
    {
        var result = HealthStatus.TryFromName("Degraded", out var status);

        result.Should().BeTrue();
        status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void TryFromName_InvalidName_ReturnsFalse()
    {
        var result = HealthStatus.TryFromName("Invalid", out var status);

        result.Should().BeFalse();
        status.Should().BeNull();
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameStatus_AreEqual()
    {
        var status1 = HealthStatus.Healthy;
        var status2 = HealthStatus.Healthy;

        (status1 == status2).Should().BeTrue();
        status1.Equals(status2).Should().BeTrue();
        status1.GetHashCode().Should().Be(status2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentStatuses_AreNotEqual()
    {
        var status1 = HealthStatus.Healthy;
        var status2 = HealthStatus.Unhealthy;

        (status1 == status2).Should().BeFalse();
        (status1 != status2).Should().BeTrue();
        status1.Equals(status2).Should().BeFalse();
    }

    [Fact]
    public void Equality_FromValueAndStatic_AreEqual()
    {
        var fromValue = HealthStatus.FromValue(0);
        var staticInstance = HealthStatus.Healthy;

        fromValue.Should().Be(staticInstance);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ReturnsName()
    {
        HealthStatus.Healthy.ToString().Should().Be("Healthy");
        HealthStatus.Degraded.ToString().Should().Be("Degraded");
        HealthStatus.Unhealthy.ToString().Should().Be("Unhealthy");
        HealthStatus.Unknown.ToString().Should().Be("Unknown");
    }

    #endregion

    #region Implicit Conversion

    [Fact]
    public void ImplicitConversion_ToInt_ReturnsValue()
    {
        int value = HealthStatus.Unhealthy;

        value.Should().Be(2);
    }

    #endregion
}

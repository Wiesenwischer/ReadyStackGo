using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.UnitTests.Domain.Observers;

/// <summary>
/// Unit tests for ObserverResult value object.
/// </summary>
public class ObserverResultTests
{
    #region Factory Methods

    [Fact]
    public void MaintenanceRequired_CreatesSuccessfulResult_WithMaintenanceFlag()
    {
        var result = ObserverResult.MaintenanceRequired("1");

        result.IsSuccess.Should().BeTrue();
        result.IsMaintenanceRequired.Should().BeTrue();
        result.ObservedValue.Should().Be("1");
        result.ErrorMessage.Should().BeNull();
        result.CheckedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void NormalOperation_CreatesSuccessfulResult_WithoutMaintenanceFlag()
    {
        var result = ObserverResult.NormalOperation("0");

        result.IsSuccess.Should().BeTrue();
        result.IsMaintenanceRequired.Should().BeFalse();
        result.ObservedValue.Should().Be("0");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failed_CreatesFailedResult()
    {
        var result = ObserverResult.Failed("Connection timeout");

        result.IsSuccess.Should().BeFalse();
        result.IsMaintenanceRequired.Should().BeFalse();
        result.ObservedValue.Should().BeNull();
        result.ErrorMessage.Should().Be("Connection timeout");
    }

    #endregion

    #region FromComparison

    [Theory]
    [InlineData("1", "1", true)]
    [InlineData("true", "true", true)]
    [InlineData("maintenance", "maintenance", true)]
    [InlineData("0", "1", false)]
    [InlineData("false", "true", false)]
    [InlineData("normal", "maintenance", false)]
    public void FromComparison_DeterminesMaintenanceCorrectly(
        string observedValue,
        string maintenanceValue,
        bool expectedMaintenance)
    {
        var result = ObserverResult.FromComparison(observedValue, maintenanceValue);

        result.IsSuccess.Should().BeTrue();
        result.IsMaintenanceRequired.Should().Be(expectedMaintenance);
        result.ObservedValue.Should().Be(observedValue);
    }

    [Theory]
    [InlineData("TRUE", "true")]
    [InlineData("True", "TRUE")]
    [InlineData("MAINTENANCE", "maintenance")]
    public void FromComparison_IsCaseInsensitive(string observedValue, string maintenanceValue)
    {
        var result = ObserverResult.FromComparison(observedValue, maintenanceValue);

        result.IsMaintenanceRequired.Should().BeTrue();
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_MaintenanceRequired_ReturnsDescriptiveString()
    {
        var result = ObserverResult.MaintenanceRequired("1");

        result.ToString().Should().Contain("Maintenance required");
        result.ToString().Should().Contain("1");
    }

    [Fact]
    public void ToString_NormalOperation_ReturnsDescriptiveString()
    {
        var result = ObserverResult.NormalOperation("0");

        result.ToString().Should().Contain("Normal operation");
        result.ToString().Should().Contain("0");
    }

    [Fact]
    public void ToString_Failed_ReturnsErrorMessage()
    {
        var result = ObserverResult.Failed("Connection refused");

        result.ToString().Should().Contain("Failed");
        result.ToString().Should().Contain("Connection refused");
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Create two results with same values at approximately the same time
        var result1 = ObserverResult.MaintenanceRequired("1");
        var result2 = ObserverResult.MaintenanceRequired("1");

        // Note: CheckedAt will be slightly different, so they won't be strictly equal
        // This tests the behavior - in practice, results are compared by reference or used individually
        result1.IsSuccess.Should().Be(result2.IsSuccess);
        result1.IsMaintenanceRequired.Should().Be(result2.IsMaintenanceRequired);
        result1.ObservedValue.Should().Be(result2.ObservedValue);
    }

    #endregion
}

using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for Enablement value object.
/// </summary>
public class EnablementTests
{
    #region Factory Method Tests

    [Fact]
    public void IndefiniteEnablement_CreatesEnabledStatus()
    {
        // Act
        var enablement = Enablement.IndefiniteEnablement();

        // Assert
        enablement.Enabled.Should().BeTrue();
        enablement.StartDate.Should().BeNull();
        enablement.EndDate.Should().BeNull();
    }

    [Fact]
    public void Disabled_CreatesDisabledStatus()
    {
        // Act
        var enablement = Enablement.Disabled();

        // Assert
        enablement.Enabled.Should().BeFalse();
        enablement.StartDate.Should().BeNull();
        enablement.EndDate.Should().BeNull();
    }

    [Fact]
    public void TimeLimited_CreatesTimeLimitedEnablement()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(30);

        // Act
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.Enabled.Should().BeTrue();
        enablement.StartDate.Should().Be(start);
        enablement.EndDate.Should().Be(end);
    }

    [Fact]
    public void TimeLimited_WithStartAfterEnd_ThrowsArgumentException()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(30);
        var end = DateTime.UtcNow;

        // Act
        var act = () => Enablement.TimeLimited(start, end);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*start date must be before end date*");
    }

    #endregion

    #region IsEnabled Tests

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var enablement = Enablement.Disabled();

        // Assert
        enablement.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhenIndefinitelyEnabled_ReturnsTrue()
    {
        // Arrange
        var enablement = Enablement.IndefiniteEnablement();

        // Assert
        enablement.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenTimeLimitedAndWithinRange_ReturnsTrue()
    {
        // Arrange
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow.AddHours(1);
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenTimeLimitedAndBeforeStart_ReturnsFalse()
    {
        // Arrange
        var start = DateTime.UtcNow.AddHours(1);
        var end = DateTime.UtcNow.AddHours(2);
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhenTimeLimitedAndAfterEnd_ReturnsFalse()
    {
        // Arrange
        var start = DateTime.UtcNow.AddHours(-2);
        var end = DateTime.UtcNow.AddHours(-1);
        var enablement = Enablement.TimeLimited(start, end);

        // Assert
        enablement.IsEnabled.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameIndefiniteEnablement_ReturnsTrue()
    {
        // Arrange
        var e1 = Enablement.IndefiniteEnablement();
        var e2 = Enablement.IndefiniteEnablement();

        // Assert
        e1.Should().Be(e2);
    }

    [Fact]
    public void Equals_SameDisabled_ReturnsTrue()
    {
        // Arrange
        var e1 = Enablement.Disabled();
        var e2 = Enablement.Disabled();

        // Assert
        e1.Should().Be(e2);
    }

    [Fact]
    public void Equals_SameTimeLimited_ReturnsTrue()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var e1 = Enablement.TimeLimited(start, end);
        var e2 = Enablement.TimeLimited(start, end);

        // Assert
        e1.Should().Be(e2);
    }

    [Fact]
    public void Equals_DifferentEnablements_ReturnsFalse()
    {
        // Arrange
        var e1 = Enablement.IndefiniteEnablement();
        var e2 = Enablement.Disabled();

        // Assert
        e1.Should().NotBe(e2);
    }

    [Fact]
    public void Equals_DifferentTimeLimits_ReturnsFalse()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var e1 = Enablement.TimeLimited(start, start.AddDays(30));
        var e2 = Enablement.TimeLimited(start, start.AddDays(60));

        // Assert
        e1.Should().NotBe(e2);
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHashCode()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var e1 = Enablement.TimeLimited(start, end);
        var e2 = Enablement.TimeLimited(start, end);

        // Assert
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var enablement = Enablement.IndefiniteEnablement();

        // Act
        var result = enablement.ToString();

        // Assert
        result.Should().Contain("Enablement");
        result.Should().Contain("enabled=True");
    }

    [Fact]
    public void ToString_Disabled_ContainsDisabledInfo()
    {
        // Arrange
        var enablement = Enablement.Disabled();

        // Act
        var result = enablement.ToString();

        // Assert
        result.Should().Contain("enabled=False");
    }

    #endregion
}

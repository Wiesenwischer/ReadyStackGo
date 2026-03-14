namespace ReadyStackGo.UnitTests.Domain.Observers;

using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Unit tests for MaintenanceTrigger value object.
/// Covers creation, properties, equality, and factory methods.
/// </summary>
public class MaintenanceTriggerTests
{
    #region Factory Method Tests

    [Fact]
    public void Manual_CreatesManualTrigger()
    {
        // Act
        var trigger = MaintenanceTrigger.Manual("Scheduled maintenance", "admin-user");

        // Assert
        Assert.Equal(MaintenanceTriggerSource.Manual, trigger.Source);
        Assert.True(trigger.IsManual);
        Assert.False(trigger.IsObserver);
        Assert.Equal("Scheduled maintenance", trigger.Reason);
        Assert.Equal("admin-user", trigger.TriggeredBy);
        Assert.True(trigger.TriggeredAtUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Manual_WithoutArguments_CreatesMinimalTrigger()
    {
        // Act
        var trigger = MaintenanceTrigger.Manual();

        // Assert
        Assert.True(trigger.IsManual);
        Assert.Null(trigger.Reason);
        Assert.Null(trigger.TriggeredBy);
    }

    [Fact]
    public void Observer_CreatesObserverTrigger()
    {
        // Act
        var trigger = MaintenanceTrigger.Observer("External DB maintenance detected", "SqlObserver");

        // Assert
        Assert.Equal(MaintenanceTriggerSource.Observer, trigger.Source);
        Assert.True(trigger.IsObserver);
        Assert.False(trigger.IsManual);
        Assert.Equal("External DB maintenance detected", trigger.Reason);
        Assert.Equal("SqlObserver", trigger.TriggeredBy);
    }

    [Fact]
    public void Observer_WithoutArguments_CreatesMinimalTrigger()
    {
        // Act
        var trigger = MaintenanceTrigger.Observer();

        // Assert
        Assert.True(trigger.IsObserver);
        Assert.Null(trigger.Reason);
        Assert.Null(trigger.TriggeredBy);
    }

    [Fact]
    public void Create_WithExplicitTimestamp_PreservesTimestamp()
    {
        // Arrange
        var timestamp = new DateTime(2026, 3, 14, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var trigger = MaintenanceTrigger.Create(
            MaintenanceTriggerSource.Observer, "Reason", timestamp, "HttpObserver");

        // Assert
        Assert.Equal(timestamp, trigger.TriggeredAtUtc);
        Assert.Equal(MaintenanceTriggerSource.Observer, trigger.Source);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void SameTriggers_AreEqual()
    {
        // Arrange
        var timestamp = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);
        var trigger1 = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, "reason", timestamp, "user1");
        var trigger2 = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, "reason", timestamp, "user1");

        // Assert
        Assert.Equal(trigger1, trigger2);
        Assert.True(trigger1 == trigger2);
    }

    [Fact]
    public void DifferentSource_NotEqual()
    {
        // Arrange
        var timestamp = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);
        var manual = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, "reason", timestamp, "user1");
        var observer = MaintenanceTrigger.Create(MaintenanceTriggerSource.Observer, "reason", timestamp, "user1");

        // Assert
        Assert.NotEqual(manual, observer);
        Assert.True(manual != observer);
    }

    [Fact]
    public void DifferentReason_NotEqual()
    {
        // Arrange
        var timestamp = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);
        var trigger1 = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, "reason1", timestamp, "user1");
        var trigger2 = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, "reason2", timestamp, "user1");

        // Assert
        Assert.NotEqual(trigger1, trigger2);
    }

    [Fact]
    public void NullReason_NotEqualToNonNull()
    {
        // Arrange
        var timestamp = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);
        var trigger1 = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, null, timestamp, null);
        var trigger2 = MaintenanceTrigger.Create(MaintenanceTriggerSource.Manual, "reason", timestamp, null);

        // Assert
        Assert.NotEqual(trigger1, trigger2);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ContainsSource()
    {
        // Arrange
        var trigger = MaintenanceTrigger.Manual("Test reason");

        // Act
        var result = trigger.ToString();

        // Assert
        Assert.Contains("Manual", result);
        Assert.Contains("Test reason", result);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void MaintenanceTriggerSource_HasExpectedValues()
    {
        Assert.Equal(0, (int)MaintenanceTriggerSource.Manual);
        Assert.Equal(1, (int)MaintenanceTriggerSource.Observer);
    }

    #endregion
}

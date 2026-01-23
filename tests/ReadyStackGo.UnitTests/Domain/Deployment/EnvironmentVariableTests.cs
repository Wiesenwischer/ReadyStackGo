using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Domain.EnvironmentVariableTests;

/// <summary>
/// Unit tests for EnvironmentVariable entity.
/// </summary>
public class EnvironmentVariableTests
{
    #region Creation Tests

    [Fact]
    public void Create_WithValidData_CreatesEnvironmentVariable()
    {
        // Arrange
        var varId = EnvironmentVariableId.NewId();
        var envId = EnvironmentId.NewId();
        var key = "DATABASE_URL";
        var value = "postgresql://localhost:5432/db";

        // Act
        var envVar = EnvironmentVariable.Create(varId, envId, key, value);

        // Assert
        envVar.Id.Should().Be(varId);
        envVar.EnvironmentId.Should().Be(envId);
        envVar.Key.Should().Be(key);
        envVar.Value.Should().Be(value);
        envVar.IsEncrypted.Should().BeFalse();
        envVar.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        envVar.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithEncryptedFlag_CreatesEncryptedVariable()
    {
        // Arrange
        var varId = EnvironmentVariableId.NewId();
        var envId = EnvironmentId.NewId();

        // Act
        var envVar = EnvironmentVariable.Create(varId, envId, "API_KEY", "secret123", isEncrypted: true);

        // Assert
        envVar.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullId_ThrowsArgumentException()
    {
        // Act
        var act = () => EnvironmentVariable.Create(
            null!,
            EnvironmentId.NewId(),
            "KEY",
            "value");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EnvironmentVariableId is required*");
    }

    [Fact]
    public void Create_WithNullEnvironmentId_ThrowsArgumentException()
    {
        // Act
        var act = () => EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            null!,
            "KEY",
            "value");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EnvironmentId is required*");
    }

    [Fact]
    public void Create_WithEmptyKey_ThrowsArgumentException()
    {
        // Act
        var act = () => EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            "",
            "value");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Variable key is required*");
    }

    [Fact]
    public void Create_WithNullValue_ThrowsArgumentException()
    {
        // Act
        var act = () => EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            "KEY",
            null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Variable value cannot be null*");
    }

    [Fact]
    public void Create_WithKeyTooLong_ThrowsArgumentException()
    {
        // Act
        var act = () => EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            new string('x', 501),
            "value");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Variable key must be 500 characters or less*");
    }

    #endregion

    #region UpdateValue Tests

    [Fact]
    public void UpdateValue_WithValidValue_UpdatesValue()
    {
        // Arrange
        var envVar = CreateTestEnvironmentVariable();
        var originalUpdatedAt = envVar.UpdatedAt;
        Thread.Sleep(10); // Ensure time difference

        // Act
        envVar.UpdateValue("new-value");

        // Assert
        envVar.Value.Should().Be("new-value");
        envVar.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        envVar.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateValue_WithEncryptedFlag_UpdatesEncryptionStatus()
    {
        // Arrange
        var envVar = CreateTestEnvironmentVariable();
        envVar.IsEncrypted.Should().BeFalse();

        // Act
        envVar.UpdateValue("encrypted-value", isEncrypted: true);

        // Assert
        envVar.Value.Should().Be("encrypted-value");
        envVar.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void UpdateValue_WithNullValue_ThrowsArgumentException()
    {
        // Arrange
        var envVar = CreateTestEnvironmentVariable();

        // Act
        var act = () => envVar.UpdateValue(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Variable value cannot be null*");
    }

    [Fact]
    public void UpdateValue_CanSetEmptyString()
    {
        // Arrange
        var envVar = CreateTestEnvironmentVariable();

        // Act
        envVar.UpdateValue("");

        // Assert
        envVar.Value.Should().BeEmpty();
    }

    #endregion

    #region EnvironmentVariableId Tests

    [Fact]
    public void EnvironmentVariableId_NewId_CreatesUniqueId()
    {
        // Act
        var id1 = EnvironmentVariableId.NewId();
        var id2 = EnvironmentVariableId.NewId();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void EnvironmentVariableId_Create_CreatesUniqueId()
    {
        // Act
        var id1 = EnvironmentVariableId.Create();
        var id2 = EnvironmentVariableId.Create();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void EnvironmentVariableId_FromGuid_CreatesCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = EnvironmentVariableId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void EnvironmentVariableId_EmptyGuid_ThrowsException()
    {
        // Act
        var act = () => new EnvironmentVariableId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*EnvironmentVariableId cannot be empty*");
    }

    [Fact]
    public void EnvironmentVariableId_Equality_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new EnvironmentVariableId(guid);
        var id2 = new EnvironmentVariableId(guid);

        // Assert
        id1.Should().Be(id2);
        id1.Equals(id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void EnvironmentVariableId_ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = new EnvironmentVariableId(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var envVar = CreateTestEnvironmentVariable();

        // Act
        var result = envVar.ToString();

        // Assert
        result.Should().Contain("EnvironmentVariable");
        result.Should().Contain("TEST_KEY");
        result.Should().Contain("isEncrypted=False");
    }

    [Fact]
    public void ToString_WithEncryptedVariable_ShowsEncryptedStatus()
    {
        // Arrange
        var envVar = EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            "SECRET_KEY",
            "secret-value",
            isEncrypted: true);

        // Act
        var result = envVar.ToString();

        // Assert
        result.Should().Contain("isEncrypted=True");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithSpecialCharactersInKey_CreatesVariable()
    {
        // Arrange
        var key = "APP_CONFIG__DATABASE__CONNECTION_STRING";

        // Act
        var envVar = EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            key,
            "value");

        // Assert
        envVar.Key.Should().Be(key);
    }

    [Fact]
    public void Create_WithVeryLongValue_CreatesVariable()
    {
        // Arrange
        var longValue = new string('x', 10000);

        // Act
        var envVar = EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            "LONG_KEY",
            longValue);

        // Assert
        envVar.Value.Should().Be(longValue);
    }

    [Fact]
    public void Create_WithMaxLengthKey_CreatesVariable()
    {
        // Arrange
        var maxKey = new string('x', 500);

        // Act
        var envVar = EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            maxKey,
            "value");

        // Assert
        envVar.Key.Should().Be(maxKey);
    }

    [Fact]
    public void UpdateValue_MultipleUpdates_UpdatesTimestampEachTime()
    {
        // Arrange
        var envVar = CreateTestEnvironmentVariable();
        var firstUpdate = envVar.UpdatedAt;
        Thread.Sleep(10);

        // Act
        envVar.UpdateValue("value1");
        var secondUpdate = envVar.UpdatedAt;
        Thread.Sleep(10);
        envVar.UpdateValue("value2");
        var thirdUpdate = envVar.UpdatedAt;

        // Assert
        secondUpdate.Should().BeAfter(firstUpdate);
        thirdUpdate.Should().BeAfter(secondUpdate);
    }

    #endregion

    #region Helper Methods

    private static EnvironmentVariable CreateTestEnvironmentVariable()
    {
        return EnvironmentVariable.Create(
            EnvironmentVariableId.NewId(),
            EnvironmentId.NewId(),
            "TEST_KEY",
            "test-value");
    }

    #endregion
}

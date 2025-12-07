using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for Environment domain events.
/// </summary>
public class EnvironmentEventTests
{
    #region EnvironmentCreated Tests

    [Fact]
    public void EnvironmentCreated_Constructor_SetsAllProperties()
    {
        // Arrange
        var environmentId = EnvironmentId.NewId();
        var name = "Production";

        // Act
        var evt = new EnvironmentCreated(environmentId, name);

        // Assert
        evt.EnvironmentId.Should().Be(environmentId);
        evt.Name.Should().Be(name);
    }

    [Fact]
    public void EnvironmentCreated_InheritsFromDomainEvent()
    {
        // Arrange & Act
        var evt = new EnvironmentCreated(EnvironmentId.NewId(), "Test");

        // Assert
        evt.Should().BeAssignableTo<ReadyStackGo.Domain.SharedKernel.DomainEvent>();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    [InlineData("Local Docker")]
    public void EnvironmentCreated_VariousNames_TracksCorrectly(string name)
    {
        // Act
        var evt = new EnvironmentCreated(EnvironmentId.NewId(), name);

        // Assert
        evt.Name.Should().Be(name);
    }

    #endregion
}

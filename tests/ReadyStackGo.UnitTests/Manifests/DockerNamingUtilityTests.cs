using FluentAssertions;
using ReadyStackGo.Infrastructure.Docker;
using Xunit;

namespace ReadyStackGo.UnitTests.Manifests;

public class DockerNamingUtilityTests
{
    #region SanitizeForDocker Tests

    [Fact]
    public void SanitizeForDocker_WithValidName_ReturnsUnchanged()
    {
        // Arrange
        var input = "my-valid-name";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("my-valid-name");
    }

    [Fact]
    public void SanitizeForDocker_WithSpaces_ReplacesWithUnderscores()
    {
        // Arrange
        var input = "Business Services";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("Business_Services");
    }

    [Fact]
    public void SanitizeForDocker_WithMultipleSpaces_ReplacesAndCollapses()
    {
        // Arrange
        var input = "My   Stack   Name";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("My_Stack_Name");
    }

    [Fact]
    public void SanitizeForDocker_WithSpecialCharacters_RemovesThem()
    {
        // Arrange
        var input = "Stack@Name!#$%";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("Stack_Name");
    }

    [Fact]
    public void SanitizeForDocker_WithUnicode_RemovesThem()
    {
        // Arrange
        var input = "Stäck_Nämé";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("St_ck_N_m");
    }

    [Fact]
    public void SanitizeForDocker_StartingWithInvalidChar_RemovesLeadingChars()
    {
        // Arrange
        var input = "_-._my-stack";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("my-stack");
    }

    [Fact]
    public void SanitizeForDocker_WithNull_ReturnsDefault()
    {
        // Act
        var result = DockerNamingUtility.SanitizeForDocker(null!);

        // Assert
        result.Should().Be("unnamed");
    }

    [Fact]
    public void SanitizeForDocker_WithEmpty_ReturnsDefault()
    {
        // Act
        var result = DockerNamingUtility.SanitizeForDocker("");

        // Assert
        result.Should().Be("unnamed");
    }

    [Fact]
    public void SanitizeForDocker_WithOnlyInvalidChars_ReturnsDefault()
    {
        // Arrange
        var input = "!!!@@@###";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("unnamed");
    }

    [Fact]
    public void SanitizeForDocker_WithPeriodAndHyphen_PreservesThem()
    {
        // Arrange
        var input = "my.stack-name";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("my.stack-name");
    }

    [Fact]
    public void SanitizeForDocker_WithTrailingUnderscores_TrimsThemOff()
    {
        // Arrange
        var input = "mystack___";

        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert
        result.Should().Be("mystack");
    }

    #endregion

    #region CreateContainerName Tests

    [Fact]
    public void CreateContainerName_WithValidNames_CombinesCorrectly()
    {
        // Arrange
        var stackName = "my-stack";
        var serviceName = "web";

        // Act
        var result = DockerNamingUtility.CreateContainerName(stackName, serviceName);

        // Assert
        result.Should().Be("my-stack_web");
    }

    [Fact]
    public void CreateContainerName_WithSpacesInStackName_SanitizesStackName()
    {
        // Arrange - This was the actual bug: "Business Services" + "_" + "healthmonitor"
        var stackName = "Business Services";
        var serviceName = "healthmonitor";

        // Act
        var result = DockerNamingUtility.CreateContainerName(stackName, serviceName);

        // Assert
        result.Should().Be("Business_Services_healthmonitor");
        // The result should be a valid Docker container name
        result.Should().MatchRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
    }

    [Fact]
    public void CreateContainerName_WithSpacesInBothNames_SanitizesBoth()
    {
        // Arrange
        var stackName = "My Stack";
        var serviceName = "Web Service";

        // Act
        var result = DockerNamingUtility.CreateContainerName(stackName, serviceName);

        // Assert
        result.Should().Be("My_Stack_Web_Service");
    }

    [Fact]
    public void CreateContainerName_WithSpecialCharsInServiceName_SanitizesServiceName()
    {
        // Arrange
        var stackName = "mystack";
        var serviceName = "web@service!";

        // Act
        var result = DockerNamingUtility.CreateContainerName(stackName, serviceName);

        // Assert
        result.Should().Be("mystack_web_service");
    }

    #endregion

    #region CreateNetworkName Tests

    [Fact]
    public void CreateNetworkName_WithValidNames_CombinesCorrectly()
    {
        // Arrange
        var stackName = "my-stack";
        var networkName = "default";

        // Act
        var result = DockerNamingUtility.CreateNetworkName(stackName, networkName);

        // Assert
        result.Should().Be("my-stack_default");
    }

    [Fact]
    public void CreateNetworkName_WithSpacesInStackName_SanitizesStackName()
    {
        // Arrange
        var stackName = "Business Services";
        var networkName = "internal";

        // Act
        var result = DockerNamingUtility.CreateNetworkName(stackName, networkName);

        // Assert
        result.Should().Be("Business_Services_internal");
    }

    #endregion

    #region CreateVolumeName Tests

    [Fact]
    public void CreateVolumeName_WithValidNames_CombinesCorrectly()
    {
        // Arrange
        var stackName = "my-stack";
        var volumeName = "data";

        // Act
        var result = DockerNamingUtility.CreateVolumeName(stackName, volumeName);

        // Assert
        result.Should().Be("my-stack_data");
    }

    [Fact]
    public void CreateVolumeName_WithSpacesInStackName_SanitizesStackName()
    {
        // Arrange
        var stackName = "Business Services";
        var volumeName = "db-data";

        // Act
        var result = DockerNamingUtility.CreateVolumeName(stackName, volumeName);

        // Assert
        result.Should().Be("Business_Services_db-data");
    }

    #endregion

    #region Docker Name Validation Tests (ensure output is always valid)

    [Theory]
    [InlineData("Business Services")]
    [InlineData("My App (Production)")]
    [InlineData("App v2.0")]
    [InlineData("123-app")]
    [InlineData("_private")]
    [InlineData("---dashes---")]
    [InlineData("unicode: äöü")]
    [InlineData("spaces   multiple")]
    [InlineData("!@#$%^&*()")]
    public void SanitizeForDocker_AlwaysProducesValidDockerName(string input)
    {
        // Act
        var result = DockerNamingUtility.SanitizeForDocker(input);

        // Assert - Docker container names must:
        // 1. Start with alphanumeric character
        // 2. Contain only alphanumeric, underscore, period, hyphen
        result.Should().MatchRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$",
            $"Input '{input}' produced invalid result '{result}'");
    }

    [Theory]
    [InlineData("Business Services", "healthmonitor")]
    [InlineData("My Stack (v2)", "web-service")]
    [InlineData("Production", "db")]
    [InlineData("__special__", "_service_")]
    public void CreateContainerName_AlwaysProducesValidDockerName(string stackName, string serviceName)
    {
        // Act
        var result = DockerNamingUtility.CreateContainerName(stackName, serviceName);

        // Assert
        result.Should().MatchRegex("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$",
            $"Stack '{stackName}' + Service '{serviceName}' produced invalid result '{result}'");
    }

    #endregion
}

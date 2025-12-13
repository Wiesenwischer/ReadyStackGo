using FluentAssertions;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.UnitTests.Domain.StackManagement;

/// <summary>
/// Unit tests for StackSource domain class.
/// </summary>
public class StackSourceTests
{
    #region LocalDirectory Creation Tests

    [Fact]
    public void CreateLocalDirectory_WithValidData_CreatesSource()
    {
        // Act
        var source = StackSource.CreateLocalDirectory(
            new StackSourceId("local-stacks"),
            "Local Stacks",
            "/app/stacks",
            "*.yaml;*.yml");

        // Assert
        source.Id.Value.Should().Be("local-stacks");
        source.Name.Should().Be("Local Stacks");
        source.Type.Should().Be(StackSourceType.LocalDirectory);
        source.Path.Should().Be("/app/stacks");
        source.FilePattern.Should().Be("*.yaml;*.yml");
        source.Enabled.Should().BeTrue();
        source.GitUrl.Should().BeNull();
        source.GitBranch.Should().BeNull();
    }

    [Fact]
    public void CreateLocalDirectory_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => StackSource.CreateLocalDirectory(
            new StackSourceId("local"),
            "",
            "/app/stacks",
            "*.yaml");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void CreateLocalDirectory_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        var act = () => StackSource.CreateLocalDirectory(
            new StackSourceId("local"),
            "Local",
            "",
            "*.yaml");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*path*");
    }

    #endregion

    #region GitRepository Creation Tests

    [Fact]
    public void CreateGitRepository_WithValidData_CreatesSource()
    {
        // Act
        var source = StackSource.CreateGitRepository(
            new StackSourceId("github-stacks"),
            "GitHub Stacks",
            "https://github.com/org/stacks.git",
            "main",
            "stacks",
            "*.yaml;*.yml");

        // Assert
        source.Id.Value.Should().Be("github-stacks");
        source.Name.Should().Be("GitHub Stacks");
        source.Type.Should().Be(StackSourceType.GitRepository);
        source.GitUrl.Should().Be("https://github.com/org/stacks.git");
        source.GitBranch.Should().Be("main");
        source.Path.Should().Be("stacks");
        source.FilePattern.Should().Be("*.yaml;*.yml");
        source.Enabled.Should().BeTrue();
    }

    [Fact]
    public void CreateGitRepository_WithEmptyGitUrl_ThrowsArgumentException()
    {
        // Act
        var act = () => StackSource.CreateGitRepository(
            new StackSourceId("git"),
            "Git",
            "",
            "main",
            null,
            "*.yaml");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*gitUrl*");
    }

    [Fact]
    public void CreateGitRepository_WithNullBranch_UsesMainAsDefault()
    {
        // Act
        var source = StackSource.CreateGitRepository(
            new StackSourceId("git"),
            "Git",
            "https://github.com/org/stacks.git",
            null,
            null,
            "*.yaml");

        // Assert
        source.GitBranch.Should().Be("main");
    }

    #endregion

    #region Enable/Disable Tests

    [Fact]
    public void Disable_EnabledSource_DisablesIt()
    {
        // Arrange
        var source = CreateTestLocalSource();
        source.Enabled.Should().BeTrue();

        // Act
        source.Disable();

        // Assert
        source.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_DisabledSource_EnablesIt()
    {
        // Arrange
        var source = CreateTestLocalSource();
        source.Disable();

        // Act
        source.Enable();

        // Assert
        source.Enabled.Should().BeTrue();
    }

    #endregion

    #region UpdateName Tests

    [Fact]
    public void UpdateName_WithValidName_ChangesName()
    {
        // Arrange
        var source = CreateTestLocalSource();

        // Act
        source.UpdateName("New Name");

        // Assert
        source.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateName_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var source = CreateTestLocalSource();

        // Act
        var act = () => source.UpdateName("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*name*");
    }

    #endregion

    #region LastSyncedAt Tests

    [Fact]
    public void MarkSynced_UpdatesLastSyncedAt()
    {
        // Arrange
        var source = CreateTestLocalSource();
        var beforeSync = DateTime.UtcNow;

        // Act
        source.MarkSynced();

        // Assert
        source.LastSyncedAt.Should().NotBeNull();
        source.LastSyncedAt.Should().BeOnOrAfter(beforeSync);
    }

    #endregion

    #region StackSourceId Tests

    [Fact]
    public void StackSourceId_Create_CreatesCorrectly()
    {
        // Act
        var id = new StackSourceId("my-source");

        // Assert
        id.Value.Should().Be("my-source");
    }

    [Fact]
    public void StackSourceId_EmptyValue_ThrowsArgumentException()
    {
        // Act
        var act = () => new StackSourceId("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*value*");
    }

    [Fact]
    public void StackSourceId_Equality_WorksCorrectly()
    {
        // Arrange
        var id1 = new StackSourceId("source");
        var id2 = new StackSourceId("source");

        // Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void StackSourceId_ToString_ReturnsValue()
    {
        // Arrange
        var id = new StackSourceId("my-source");

        // Act & Assert
        id.ToString().Should().Be("my-source");
    }

    [Fact]
    public void StackSourceId_ImplicitConversionToString_Works()
    {
        // Arrange
        var id = new StackSourceId("my-source");

        // Act
        string value = id;

        // Assert
        value.Should().Be("my-source");
    }

    #endregion

    #region Helper Methods

    private static StackSource CreateTestLocalSource()
    {
        return StackSource.CreateLocalDirectory(
            new StackSourceId("local"),
            "Local",
            "/app/stacks",
            "*.yaml;*.yml");
    }

    #endregion
}

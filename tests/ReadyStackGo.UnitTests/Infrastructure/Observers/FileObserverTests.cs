using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Observers;

/// <summary>
/// Unit tests for FileObserver.
/// </summary>
public class FileObserverTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly Mock<ILogger<FileObserver>> _loggerMock;

    public FileObserverTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"observer_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _loggerMock = new Mock<ILogger<FileObserver>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region Existence Mode

    [Fact]
    public async Task CheckAsync_FileExists_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "maintenance.flag");
        await File.WriteAllTextAsync(filePath, "");

        var settings = FileObserverSettings.ForExistence(filePath);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "true",  // File exists = maintenance
            "false",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("true");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_FileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent.flag");

        var settings = FileObserverSettings.ForExistence(filePath);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "true",
            "false",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("false");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    #endregion

    #region Content Mode

    [Fact]
    public async Task CheckAsync_ContentMode_ReadsEntireContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "config.txt");
        await File.WriteAllTextAsync(filePath, "maintenance");

        var settings = FileObserverSettings.ForContent(filePath);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("maintenance");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_ContentMode_WithPattern_ExtractsMatch()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "config.txt");
        await File.WriteAllTextAsync(filePath, "status=maintenance\nversion=1.0");

        var settings = FileObserverSettings.ForContent(filePath, @"status=(\w+)");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("maintenance");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_ContentMode_PatternNoMatch_ReturnsNormalValue()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "config.txt");
        await File.WriteAllTextAsync(filePath, "other_key=value");

        var settings = FileObserverSettings.ForContent(filePath, @"status=(\w+)");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("normal");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ContentMode_FileDoesNotExist_ReturnsNormalValue()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent.txt");

        var settings = FileObserverSettings.ForContent(filePath);
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("normal");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    #endregion

    #region Type Property

    [Fact]
    public void Type_ReturnsFileObserverType()
    {
        var settings = FileObserverSettings.ForExistence("/tmp/test");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "true",
            "false",
            settings);
        var observer = new FileObserver(config, _loggerMock.Object);

        observer.Type.Should().Be(ObserverType.File);
    }

    #endregion
}

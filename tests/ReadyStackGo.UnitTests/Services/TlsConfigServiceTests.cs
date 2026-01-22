using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for TlsConfigService.
/// Tests certificate validation and configuration management.
/// </summary>
public class TlsConfigServiceTests : IDisposable
{
    private readonly Mock<IConfigStore> _configStoreMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<TlsConfigService>> _loggerMock;
    private readonly string _testConfigPath;

    public TlsConfigServiceTests()
    {
        _configStoreMock = new Mock<IConfigStore>();
        _loggerMock = new Mock<ILogger<TlsConfigService>>();

        _testConfigPath = Path.Combine(Path.GetTempPath(), "rsgo-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testConfigPath);

        // Use in-memory configuration instead of mocking
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigPath"] = _testConfigPath
            });
        _configuration = configBuilder.Build();
    }

    private TlsConfigService CreateService()
    {
        return new TlsConfigService(
            _configStoreMock.Object,
            _configuration,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetTlsConfigAsync_ReturnsTlsMode()
    {
        // Arrange
        _configStoreMock.Setup(c => c.GetTlsConfigAsync())
            .ReturnsAsync(new TlsConfig
            {
                TlsMode = TlsMode.SelfSigned,
                HttpEnabled = true,
                CertificatePath = "/nonexistent/path.pfx"
            });

        var service = CreateService();

        // Act
        var result = await service.GetTlsConfigAsync();

        // Assert
        result.Mode.Should().Be("SelfSigned");
        result.HttpEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetTlsConfigAsync_WithNonExistentCertificate_ReturnsNullCertificateInfo()
    {
        // Arrange
        _configStoreMock.Setup(c => c.GetTlsConfigAsync())
            .ReturnsAsync(new TlsConfig
            {
                TlsMode = TlsMode.SelfSigned,
                CertificatePath = "/nonexistent/path.pfx"
            });

        var service = CreateService();

        // Act
        var result = await service.GetTlsConfigAsync();

        // Assert
        result.CertificateInfo.Should().BeNull();
    }

    [Fact]
    public async Task UploadPfxCertificateAsync_WithInvalidData_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var invalidPfxData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = await service.UploadPfxCertificateAsync(invalidPfxData, "password");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid certificate");
    }

    [Fact]
    public async Task UploadPemCertificateAsync_WithInvalidPem_ReturnsError()
    {
        // Arrange
        var service = CreateService();
        var invalidCertPem = "not a valid certificate";
        var invalidKeyPem = "not a valid key";

        // Act
        var result = await service.UploadPemCertificateAsync(invalidCertPem, invalidKeyPem);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid certificate");
    }

    [Fact]
    public async Task SetHttpEnabledAsync_UpdatesConfigStore()
    {
        // Arrange
        var config = new TlsConfig { HttpEnabled = false };
        _configStoreMock.Setup(c => c.GetTlsConfigAsync()).ReturnsAsync(config);
        _configStoreMock.Setup(c => c.SaveTlsConfigAsync(It.IsAny<TlsConfig>())).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.SetHttpEnabledAsync(true);

        // Assert
        result.Success.Should().BeTrue();
        _configStoreMock.Verify(c => c.SaveTlsConfigAsync(It.Is<TlsConfig>(cfg => cfg.HttpEnabled == true)), Times.Once);
    }

    [Fact]
    public async Task SetHttpEnabledAsync_EnabledTrue_ReturnsSuccessMessage()
    {
        // Arrange
        _configStoreMock.Setup(c => c.GetTlsConfigAsync()).ReturnsAsync(new TlsConfig());
        _configStoreMock.Setup(c => c.SaveTlsConfigAsync(It.IsAny<TlsConfig>())).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.SetHttpEnabledAsync(true);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("enabled");
    }

    [Fact]
    public async Task SetHttpEnabledAsync_EnabledFalse_ReturnsSuccessMessage()
    {
        // Arrange
        _configStoreMock.Setup(c => c.GetTlsConfigAsync()).ReturnsAsync(new TlsConfig());
        _configStoreMock.Setup(c => c.SaveTlsConfigAsync(It.IsAny<TlsConfig>())).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.SetHttpEnabledAsync(false);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task ResetToSelfSignedAsync_WithMissingSelfSignedCert_ReturnsError()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ResetToSelfSignedAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ResetToSelfSignedAsync_WithExistingCert_UpdatesConfig()
    {
        // Arrange
        var tlsPath = Path.Combine(_testConfigPath, "tls");
        Directory.CreateDirectory(tlsPath);
        var selfSignedPath = Path.Combine(tlsPath, "selfsigned.pfx");
        File.WriteAllText(selfSignedPath, "dummy"); // Just for existence check

        _configStoreMock.Setup(c => c.GetTlsConfigAsync()).ReturnsAsync(new TlsConfig { TlsMode = TlsMode.Custom });
        _configStoreMock.Setup(c => c.SaveTlsConfigAsync(It.IsAny<TlsConfig>())).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.ResetToSelfSignedAsync();

        // Assert
        result.Success.Should().BeTrue();
        _configStoreMock.Verify(c => c.SaveTlsConfigAsync(It.Is<TlsConfig>(cfg =>
            cfg.TlsMode == TlsMode.SelfSigned)), Times.Once);
    }

    [Fact]
    public async Task SetHttpEnabledAsync_RequiresRestart()
    {
        // Arrange
        _configStoreMock.Setup(c => c.GetTlsConfigAsync()).ReturnsAsync(new TlsConfig());
        _configStoreMock.Setup(c => c.SaveTlsConfigAsync(It.IsAny<TlsConfig>())).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.SetHttpEnabledAsync(true);

        // Assert
        result.RequiresRestart.Should().BeTrue();
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testConfigPath))
        {
            try { Directory.Delete(_testConfigPath, true); } catch { }
        }
    }
}

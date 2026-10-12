using FluentAssertions;
using Moq;
using ReadyStackGo.Infrastructure.Configuration;
using BaseUrlExtensions = ReadyStackGo.Application.Services.SystemConfigBaseUrlExtensions;

namespace ReadyStackGo.UnitTests.Infrastructure.Configuration;

public class SystemConfigServiceBaseUrlTests
{
    private readonly Mock<IConfigStore> _store = new();
    private SystemConfig _config = new();

    public SystemConfigServiceBaseUrlTests()
    {
        _store.Setup(s => s.GetSystemConfigAsync()).ReturnsAsync(() => _config);
        _store.Setup(s => s.SaveSystemConfigAsync(It.IsAny<SystemConfig>()))
            .Callback<SystemConfig>(c => _config = c)
            .Returns(Task.CompletedTask);
    }

    private SystemConfigService CreateService() => new(_store.Object);

    [Fact]
    public async Task GetConfiguredBaseUrl_FreshConfigWithBuiltInDefault_ReturnsNull()
    {
        _config.BaseUrl.Should().Be("http://localhost:5000");

        (await CreateService().GetConfiguredBaseUrlAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("http://localhost:5000/")]
    public async Task GetConfiguredBaseUrl_UnsetValues_ReturnNull(string stored)
    {
        _config.BaseUrl = stored;

        (await CreateService().GetConfiguredBaseUrlAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetConfiguredBaseUrl_StoredValue_IsReturnedNormalized()
    {
        _config.BaseUrl = "https://rsgo.example.com/";

        (await CreateService().GetConfiguredBaseUrlAsync()).Should().Be("https://rsgo.example.com");
    }

    [Fact]
    public async Task GetConfiguredBaseUrl_StoredValueInvalid_ReturnsNull()
    {
        _config.BaseUrl = "not a url";

        (await CreateService().GetConfiguredBaseUrlAsync()).Should().BeNull();
    }

    [Fact]
    public async Task GetBaseUrl_StillReturnsRawDefault()
    {
        (await CreateService().GetBaseUrlAsync()).Should().Be("http://localhost:5000");
    }

    [Fact]
    public async Task SetBaseUrl_ValidValue_StoresNormalized()
    {
        await CreateService().SetBaseUrlAsync("  https://rsgo.example.com/  ");

        _config.BaseUrl.Should().Be("https://rsgo.example.com");
        (await CreateService().GetConfiguredBaseUrlAsync()).Should().Be("https://rsgo.example.com");
    }

    [Fact]
    public async Task SetBaseUrl_KeepsOtherSettings()
    {
        _config.WizardState = WizardState.Installed;
        _config.DefaultTheme = "classic";

        await CreateService().SetBaseUrlAsync("https://rsgo.example.com");

        _config.WizardState.Should().Be(WizardState.Installed);
        _config.DefaultTheme.Should().Be("classic");
    }

    [Theory]
    [InlineData("")]
    [InlineData("rsgo.example.com")]
    [InlineData("ftp://rsgo.example.com")]
    [InlineData("https://rsgo.example.com/?x=1")]
    public async Task SetBaseUrl_InvalidValue_ThrowsAndDoesNotSave(string value)
    {
        var act = () => CreateService().SetBaseUrlAsync(value);

        await act.Should().ThrowAsync<ArgumentException>();
        _store.Verify(s => s.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Never);
        _config.BaseUrl.Should().Be("http://localhost:5000");
    }

    [Fact]
    public async Task SetBaseUrl_BuiltInDefault_IsStoredButStillCountsAsUnset()
    {
        await CreateService().SetBaseUrlAsync("http://localhost:5000");

        (await CreateService().GetConfiguredBaseUrlAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData("https://rsgo.example.com/", "https://rsgo.example.com")]
    [InlineData("HTTPS://RSGO.Example.com/path/", "https://rsgo.example.com/path")]
    [InlineData("http://localhost:5000", "http://localhost:5000")]
    public async Task GetEffectiveBaseUrl_IsNormalizedLikeTheSetup(string stored, string expected)
    {
        _config.BaseUrl = stored;

        (await BaseUrlExtensions.GetEffectiveBaseUrlAsync(CreateService())).Should().Be(expected);
    }

    [Fact]
    public async Task GetEffectiveBaseUrl_ValueThatCannotBeNormalized_IsReturnedWithoutTrailingSlash()
    {
        _config.BaseUrl = "not a url/";

        (await BaseUrlExtensions.GetEffectiveBaseUrlAsync(CreateService())).Should().Be("not a url");
    }
}

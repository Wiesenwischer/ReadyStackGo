using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.UnitTests.Domain.Observers;

/// <summary>
/// Unit tests for MaintenanceObserverConfig and related settings classes.
/// </summary>
public class MaintenanceObserverConfigTests
{
    #region MaintenanceObserverConfig.Create

    [Fact]
    public void Create_WithValidParameters_ReturnsConfig()
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "Server=localhost;Database=test");

        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            settings);

        config.Should().NotBeNull();
        config.Type.Should().Be(ObserverType.SqlExtendedProperty);
        config.PollingInterval.Should().Be(TimeSpan.FromSeconds(30));
        config.MaintenanceValue.Should().Be("1");
        config.NormalValue.Should().Be("0");
        config.Settings.Should().Be(settings);
    }

    [Fact]
    public void Create_WithNullType_ThrowsArgumentNullException()
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "connstr");

        var act = () => MaintenanceObserverConfig.Create(
            null!,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            settings);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullSettings_ThrowsArgumentNullException()
    {
        var act = () => MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithZeroPollingInterval_ThrowsArgumentException()
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "connstr");

        var act = () => MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.Zero,
            "1",
            "0",
            settings);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Polling interval*");
    }

    [Fact]
    public void Create_WithNegativePollingInterval_ThrowsArgumentException()
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "connstr");

        var act = () => MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(-10),
            "1",
            "0",
            settings);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyMaintenanceValue_ThrowsArgumentException(string? maintenanceValue)
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "connstr");

        var act = () => MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            maintenanceValue!,
            "0",
            settings);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Maintenance value*");
    }

    [Fact]
    public void Create_WithNullNormalValue_IsAllowed()
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode", "connstr");

        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            "1",
            null,
            settings);

        config.NormalValue.Should().BeNull();
    }

    #endregion

    #region SqlObserverSettings

    [Fact]
    public void SqlObserverSettings_ForExtendedProperty_CreatesCorrectSettings()
    {
        var settings = SqlObserverSettings.ForExtendedProperty(
            "app.MaintenanceMode",
            "Server=localhost;Database=test");

        settings.PropertyName.Should().Be("app.MaintenanceMode");
        settings.ConnectionString.Should().Be("Server=localhost;Database=test");
        settings.Query.Should().BeNull();
        settings.HasConnection.Should().BeTrue();
    }

    [Fact]
    public void SqlObserverSettings_ForQuery_CreatesCorrectSettings()
    {
        var settings = SqlObserverSettings.ForQuery(
            "SELECT MaintenanceMode FROM Config",
            "Server=localhost;Database=test");

        settings.Query.Should().Be("SELECT MaintenanceMode FROM Config");
        settings.ConnectionString.Should().Be("Server=localhost;Database=test");
        settings.PropertyName.Should().BeNull();
        settings.HasConnection.Should().BeTrue();
    }

    [Fact]
    public void SqlObserverSettings_WithConnectionName_CreatesCorrectSettings()
    {
        var settings = SqlObserverSettings.ForExtendedProperty(
            "app.MaintenanceMode",
            connectionName: "BACKEND_DB");

        settings.PropertyName.Should().Be("app.MaintenanceMode");
        settings.ConnectionName.Should().Be("BACKEND_DB");
        settings.ConnectionString.Should().BeNull();
        settings.HasConnection.Should().BeTrue();
    }

    [Fact]
    public void SqlObserverSettings_Validate_NoConnection_ReturnsError()
    {
        var settings = SqlObserverSettings.ForExtendedProperty("app.MaintenanceMode");

        var errors = settings.Validate().ToList();

        errors.Should().Contain(e => e.Contains("connectionString") || e.Contains("connectionName"));
    }

    // Note: Testing both connectionString and connectionName set, or neither property nor query set,
    // would require reflection or making the class non-sealed. These edge cases are covered by
    // validation in the actual usage flow.

    #endregion

    #region HttpObserverSettings

    [Fact]
    public void HttpObserverSettings_Create_WithDefaults_UsesCorrectDefaults()
    {
        var settings = HttpObserverSettings.Create("https://api.example.com/status");

        settings.Url.Should().Be("https://api.example.com/status");
        settings.Method.Should().Be("GET");
        settings.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        settings.JsonPath.Should().BeNull();
        settings.Headers.Should().BeNull();
    }

    [Fact]
    public void HttpObserverSettings_Create_WithCustomValues_UsesProvidedValues()
    {
        var headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } };
        var settings = HttpObserverSettings.Create(
            "https://api.example.com/status",
            "POST",
            headers,
            TimeSpan.FromSeconds(30),
            "$.maintenance");

        settings.Url.Should().Be("https://api.example.com/status");
        settings.Method.Should().Be("POST");
        settings.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        settings.JsonPath.Should().Be("$.maintenance");
        settings.Headers.Should().ContainKey("Authorization");
    }

    [Fact]
    public void HttpObserverSettings_Validate_ValidUrl_NoErrors()
    {
        var settings = HttpObserverSettings.Create("https://api.example.com/status");

        var errors = settings.Validate().ToList();

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid.com")]
    public void HttpObserverSettings_Validate_InvalidUrl_ReturnsError(string url)
    {
        var settings = HttpObserverSettings.Create(url);

        var errors = settings.Validate().ToList();

        errors.Should().NotBeEmpty();
    }

    #endregion

    #region FileObserverSettings

    [Fact]
    public void FileObserverSettings_ForExistence_CreatesCorrectSettings()
    {
        var settings = FileObserverSettings.ForExistence("/var/maintenance.flag");

        settings.Path.Should().Be("/var/maintenance.flag");
        settings.Mode.Should().Be(FileCheckMode.Exists);
        settings.ContentPattern.Should().BeNull();
    }

    [Fact]
    public void FileObserverSettings_ForContent_CreatesCorrectSettings()
    {
        var settings = FileObserverSettings.ForContent("/var/config.txt", @"maintenance=(\w+)");

        settings.Path.Should().Be("/var/config.txt");
        settings.Mode.Should().Be(FileCheckMode.Content);
        settings.ContentPattern.Should().Be(@"maintenance=(\w+)");
    }

    [Fact]
    public void FileObserverSettings_Validate_ValidPath_NoErrors()
    {
        var settings = FileObserverSettings.ForExistence("/var/maintenance.flag");

        var errors = settings.Validate().ToList();

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FileObserverSettings_Validate_EmptyPath_ReturnsError(string path)
    {
        var settings = FileObserverSettings.ForExistence(path);

        var errors = settings.Validate().ToList();

        errors.Should().Contain(e => e.Contains("path"));
    }

    [Fact]
    public void FileObserverSettings_Validate_InvalidRegex_ReturnsError()
    {
        var settings = FileObserverSettings.ForContent("/var/config.txt", "[invalid(regex");

        var errors = settings.Validate().ToList();

        errors.Should().Contain(e => e.Contains("pattern") || e.Contains("regular expression"));
    }

    #endregion
}

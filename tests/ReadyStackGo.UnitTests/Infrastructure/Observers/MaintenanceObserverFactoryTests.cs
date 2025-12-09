using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Infrastructure.Observers;

namespace ReadyStackGo.UnitTests.Infrastructure.Observers;

/// <summary>
/// Unit tests for MaintenanceObserverFactory.
/// </summary>
public class MaintenanceObserverFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MaintenanceObserverFactory _factory;

    public MaintenanceObserverFactoryTests()
    {
        var services = new ServiceCollection();

        // Add required services
        services.AddLogging();
        services.AddHttpClient("MaintenanceObserver");

        _serviceProvider = services.BuildServiceProvider();
        _factory = new MaintenanceObserverFactory(_serviceProvider);
    }

    #region IsSupported

    [Theory]
    [InlineData("sqlExtendedProperty", true)]
    [InlineData("sqlQuery", true)]
    [InlineData("http", true)]
    [InlineData("file", true)]
    public void IsSupported_KnownTypes_ReturnsTrue(string typeValue, bool expected)
    {
        var type = ObserverType.FromValue(typeValue);

        var result = _factory.IsSupported(type);

        result.Should().Be(expected);
    }

    #endregion

    #region SupportedTypes

    [Fact]
    public void SupportedTypes_ReturnsAllFourTypes()
    {
        var supportedTypes = _factory.SupportedTypes.ToList();

        supportedTypes.Should().HaveCount(4);
        supportedTypes.Should().Contain(t => t.Value == "sqlExtendedProperty");
        supportedTypes.Should().Contain(t => t.Value == "sqlQuery");
        supportedTypes.Should().Contain(t => t.Value == "http");
        supportedTypes.Should().Contain(t => t.Value == "file");
    }

    #endregion

    #region Create

    [Fact]
    public void Create_SqlExtendedPropertyObserver_ReturnsCorrectType()
    {
        var settings = SqlObserverSettings.ForExtendedProperty(
            "app.MaintenanceMode",
            "Server=localhost;Database=test");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            settings);

        var observer = _factory.Create(config);

        observer.Should().BeOfType<SqlExtendedPropertyObserver>();
        observer.Type.Should().Be(ObserverType.SqlExtendedProperty);
    }

    [Fact]
    public void Create_SqlQueryObserver_ReturnsCorrectType()
    {
        var settings = SqlObserverSettings.ForQuery(
            "SELECT Value FROM Config WHERE Key='Maintenance'",
            "Server=localhost;Database=test");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.SqlQuery,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            settings);

        var observer = _factory.Create(config);

        observer.Should().BeOfType<SqlQueryObserver>();
        observer.Type.Should().Be(ObserverType.SqlQuery);
    }

    [Fact]
    public void Create_HttpObserver_ReturnsCorrectType()
    {
        var settings = HttpObserverSettings.Create("https://api.example.com/status");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);

        var observer = _factory.Create(config);

        observer.Should().BeOfType<HttpObserver>();
        observer.Type.Should().Be(ObserverType.Http);
    }

    [Fact]
    public void Create_FileObserver_ReturnsCorrectType()
    {
        var settings = FileObserverSettings.ForExistence("/var/maintenance.flag");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.File,
            TimeSpan.FromSeconds(30),
            "true",
            "false",
            settings);

        var observer = _factory.Create(config);

        observer.Should().BeOfType<FileObserver>();
        observer.Type.Should().Be(ObserverType.File);
    }

    [Fact]
    public void Create_NullConfig_ThrowsArgumentNullException()
    {
        var act = () => _factory.Create(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}

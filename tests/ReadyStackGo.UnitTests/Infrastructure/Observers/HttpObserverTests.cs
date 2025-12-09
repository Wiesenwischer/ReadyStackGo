using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Infrastructure.Observers;

namespace ReadyStackGo.UnitTests.Infrastructure.Observers;

/// <summary>
/// Unit tests for HttpObserver.
/// </summary>
public class HttpObserverTests
{
    private readonly Mock<ILogger<HttpObserver>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;

    public HttpObserverTests()
    {
        _loggerMock = new Mock<ILogger<HttpObserver>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
    }

    private void SetupHttpResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient("MaintenanceObserver"))
            .Returns(httpClient);
    }

    #region Simple Response

    [Fact]
    public async Task CheckAsync_SimpleResponse_ReturnsContent()
    {
        // Arrange
        SetupHttpResponse("maintenance");
        var settings = HttpObserverSettings.Create("https://api.example.com/status");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("maintenance");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_SimpleResponse_Normal_ReturnsNormal()
    {
        // Arrange
        SetupHttpResponse("normal");
        var settings = HttpObserverSettings.Create("https://api.example.com/status");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("normal");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    #endregion

    #region JSON Response with JSONPath

    [Fact]
    public async Task CheckAsync_JsonResponse_ExtractsValue()
    {
        // Arrange
        SetupHttpResponse("{\"status\": \"maintenance\", \"version\": \"1.0\"}");
        var settings = HttpObserverSettings.Create(
            "https://api.example.com/status",
            jsonPath: "status");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("maintenance");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_NestedJsonResponse_ExtractsValue()
    {
        // Arrange
        SetupHttpResponse("{\"system\": {\"maintenance\": {\"enabled\": true}}}");
        var settings = HttpObserverSettings.Create(
            "https://api.example.com/status",
            jsonPath: "system.maintenance.enabled");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "true",
            "false",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("true");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_JsonWithNumber_ExtractsAsString()
    {
        // Arrange
        SetupHttpResponse("{\"maintenanceMode\": 1}");
        var settings = HttpObserverSettings.Create(
            "https://api.example.com/status",
            jsonPath: "maintenanceMode");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "1",
            "0",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("1");
        result.IsMaintenanceRequired.Should().BeTrue();
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task CheckAsync_HttpError_ReturnsFailedResult()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(_httpHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(x => x.CreateClient("MaintenanceObserver"))
            .Returns(httpClient);

        var settings = HttpObserverSettings.Create("https://api.example.com/status");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection refused");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_InvalidJsonPath_ReturnsFailedResult()
    {
        // Arrange
        SetupHttpResponse("{\"status\": \"ok\"}");
        var settings = HttpObserverSettings.Create(
            "https://api.example.com/status",
            jsonPath: "nonexistent.path");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        // Act
        var result = await observer.CheckAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion

    #region Type Property

    [Fact]
    public void Type_ReturnsHttpObserverType()
    {
        SetupHttpResponse("ok");
        var settings = HttpObserverSettings.Create("https://api.example.com/status");
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            settings);
        var observer = new HttpObserver(config, _httpClientFactoryMock.Object, _loggerMock.Object);

        observer.Type.Should().Be(ObserverType.Http);
    }

    #endregion
}

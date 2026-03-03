using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Authentication;

public class ApiKeyFirstUseNotificationTests
{
    private readonly Mock<IApiKeyRepository> _repositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ApiKeyAuthenticationHandler _handler;
    private readonly DefaultHttpContext _httpContext;

    public ApiKeyFirstUseNotificationTests()
    {
        _repositoryMock = new Mock<IApiKeyRepository>();
        _notificationServiceMock = new Mock<INotificationService>();

        var services = new ServiceCollection();
        services.AddScoped(_ => _repositoryMock.Object);
        services.AddSingleton(_ => _notificationServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        _handler = new ApiKeyAuthenticationHandler(
            optionsMonitor.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            serviceProvider);

        _httpContext = new DefaultHttpContext();
    }

    private static ApiKey CreateTestApiKey(string rawKey, bool simulatePriorUsage = false)
    {
        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        var apiKey = ApiKey.Create(
            ApiKeyId.Create(),
            OrganizationId.Create(),
            "CI Pipeline",
            keyHash,
            rawKey[..12],
            new List<string> { "Hooks.Redeploy" },
            null,
            null);

        if (simulatePriorUsage)
        {
            apiKey.RecordUsage(); // Sets LastUsedAt
        }

        return apiKey;
    }

    private async Task<AuthenticateResult> AuthenticateAsync()
    {
        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationHandler.SchemeName,
            null,
            typeof(ApiKeyAuthenticationHandler));

        await _handler.InitializeAsync(scheme, _httpContext);
        return await _handler.AuthenticateAsync();
    }

    [Fact]
    public async Task FirstUse_CreatesNotification()
    {
        var rawKey = "rsgo_firstusekey1234";
        var apiKey = CreateTestApiKey(rawKey, simulatePriorUsage: false);
        apiKey.LastUsedAt.Should().BeNull(); // Confirm never used

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        await AuthenticateAsync();

        _notificationServiceMock.Verify(n => n.AddAsync(
            It.Is<Notification>(not =>
                not.Type == NotificationType.ApiKeyFirstUse &&
                not.Severity == NotificationSeverity.Info &&
                not.Metadata["keyName"] == "CI Pipeline"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubsequentUse_NoNotification()
    {
        var rawKey = "rsgo_seconduse12345";
        var apiKey = CreateTestApiKey(rawKey, simulatePriorUsage: true);
        apiKey.LastUsedAt.Should().NotBeNull(); // Already used

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        await AuthenticateAsync();

        _notificationServiceMock.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FirstUse_NotificationContainsKeyPrefix()
    {
        var rawKey = "rsgo_prefixtest1234";
        var apiKey = CreateTestApiKey(rawKey, simulatePriorUsage: false);

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        await AuthenticateAsync();

        _notificationServiceMock.Verify(n => n.AddAsync(
            It.Is<Notification>(not =>
                not.Metadata["keyPrefix"] == "rsgo_prefixt"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FirstUse_NotificationServiceThrows_AuthStillSucceeds()
    {
        _notificationServiceMock
            .Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification service down"));

        var rawKey = "rsgo_failnotify1234";
        var apiKey = CreateTestApiKey(rawKey, simulatePriorUsage: false);

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeTrue("authentication should succeed even if notification fails");
    }
}

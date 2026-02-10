using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.UnitTests.Authentication;

public class ApiKeyAuthenticationHandlerTests
{
    private readonly Mock<IApiKeyRepository> _repositoryMock;
    private readonly ApiKeyAuthenticationHandler _handler;
    private readonly DefaultHttpContext _httpContext;

    public ApiKeyAuthenticationHandlerTests()
    {
        _repositoryMock = new Mock<IApiKeyRepository>();

        // Build a service provider that can resolve IApiKeyRepository via scope
        var services = new ServiceCollection();
        services.AddScoped(_ => _repositoryMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());

        var loggerFactory = NullLoggerFactory.Instance;

        _handler = new ApiKeyAuthenticationHandler(
            optionsMonitor.Object,
            loggerFactory,
            UrlEncoder.Default,
            serviceProvider);

        _httpContext = new DefaultHttpContext();
    }

    private static ApiKey CreateTestApiKey(
        string rawKey,
        OrganizationId orgId,
        List<string> permissions,
        Guid? environmentId = null,
        DateTime? expiresAt = null)
    {
        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        return ApiKey.Create(
            ApiKeyId.Create(),
            orgId,
            "Test Key",
            keyHash,
            rawKey[..12],
            permissions,
            environmentId,
            expiresAt);
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
    public async Task NoHeader_ReturnsNoResult()
    {
        var result = await AuthenticateAsync();

        result.None.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task EmptyHeader_ReturnsNoResult()
    {
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = "";

        var result = await AuthenticateAsync();

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidPrefix_ReturnsFail()
    {
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = "invalid_key_format";

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid API key format");
    }

    [Fact]
    public async Task UnknownKey_ReturnsFail()
    {
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = "rsgo_unknown_key_value";
        _repositoryMock.Setup(r => r.GetByKeyHash(It.IsAny<string>())).Returns((ApiKey?)null);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid API key");
    }

    [Fact]
    public async Task RevokedKey_ReturnsFail()
    {
        var rawKey = "rsgo_testrevokedkey1234";
        var orgId = OrganizationId.Create();
        var apiKey = CreateTestApiKey(rawKey, orgId, new List<string> { "Hooks.Redeploy" });
        apiKey.Revoke("Compromised");

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("revoked");
    }

    [Fact]
    public async Task ExpiredKey_ReturnsFail()
    {
        var rawKey = "rsgo_testexpiredkey123";
        var orgId = OrganizationId.Create();
        var expiresAt = DateTime.UtcNow.AddDays(-1);
        var apiKey = CreateTestApiKey(rawKey, orgId, new List<string> { "Hooks.Redeploy" }, expiresAt: expiresAt);

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task ValidKey_ReturnsSuccessWithCorrectClaims()
    {
        var rawKey = "rsgo_testvalidkey1234";
        var orgId = OrganizationId.Create();
        var permissions = new List<string> { "Hooks.Redeploy", "Hooks.Upgrade" };
        var apiKey = CreateTestApiKey(rawKey, orgId, permissions);

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeTrue();

        var principal = result.Principal!;
        principal.FindFirst(RbacClaimTypes.ApiKeyId)!.Value.Should().Be(apiKey.Id.Value.ToString());
        principal.FindFirst(RbacClaimTypes.ApiKeyName)!.Value.Should().Be("Test Key");
        principal.FindFirst(RbacClaimTypes.UserId)!.Value.Should().Be($"apikey:{apiKey.Id.Value}");

        // Check role assignments
        var rolesClaim = principal.FindFirst(RbacClaimTypes.RoleAssignments)!.Value;
        var roleAssignments = JsonSerializer.Deserialize<List<RoleAssignmentClaim>>(rolesClaim)!;
        roleAssignments.Should().HaveCount(1);
        roleAssignments[0].Role.Should().Be(RoleId.Operator.Value);
        roleAssignments[0].Scope.Should().Be(ScopeType.Organization.ToString());
        roleAssignments[0].ScopeId.Should().Be(orgId.Value.ToString());

        // Check API permissions
        var apiPermissionClaims = principal.FindAll(RbacClaimTypes.ApiPermission).Select(c => c.Value).ToList();
        apiPermissionClaims.Should().Contain("Hooks.Redeploy");
        apiPermissionClaims.Should().Contain("Hooks.Upgrade");
    }

    [Fact]
    public async Task ValidKeyWithEnvironmentId_IncludesEnvIdClaim()
    {
        var rawKey = "rsgo_testenvkeyvalue1";
        var orgId = OrganizationId.Create();
        var envId = Guid.NewGuid();
        var apiKey = CreateTestApiKey(rawKey, orgId, new List<string> { "Hooks.Redeploy" }, environmentId: envId);

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(RbacClaimTypes.EnvironmentId)!.Value.Should().Be(envId.ToString());
    }

    [Fact]
    public async Task ValidKey_RecordsUsage()
    {
        var rawKey = "rsgo_testusagetrack12";
        var orgId = OrganizationId.Create();
        var apiKey = CreateTestApiKey(rawKey, orgId, new List<string> { "Hooks.Redeploy" });

        apiKey.LastUsedAt.Should().BeNull();

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        await AuthenticateAsync();

        apiKey.LastUsedAt.Should().NotBeNull();
        _repositoryMock.Verify(r => r.Update(apiKey), Times.Once);
        _repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task ValidKeyWithNoEnvironmentId_DoesNotIncludeEnvIdClaim()
    {
        var rawKey = "rsgo_testnoenvkey123";
        var orgId = OrganizationId.Create();
        var apiKey = CreateTestApiKey(rawKey, orgId, new List<string> { "Hooks.Redeploy" });

        var keyHash = ApiKeyHasher.ComputeSha256Hash(rawKey);
        _httpContext.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = rawKey;
        _repositoryMock.Setup(r => r.GetByKeyHash(keyHash)).Returns(apiKey);

        var result = await AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst(RbacClaimTypes.EnvironmentId).Should().BeNull();
    }
}

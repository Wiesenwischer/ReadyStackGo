using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.ApiKeys;
using ReadyStackGo.Application.UseCases.ApiKeys.CreateApiKey;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Application.ApiKeys;

public class CreateApiKeyHandlerTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepositoryMock;
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock;
    private readonly Mock<ILogger<CreateApiKeyHandler>> _loggerMock;
    private readonly CreateApiKeyHandler _handler;
    private readonly Organization _testOrganization;

    public CreateApiKeyHandlerTests()
    {
        _apiKeyRepositoryMock = new Mock<IApiKeyRepository>();
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _loggerMock = new Mock<ILogger<CreateApiKeyHandler>>();
        _handler = new CreateApiKeyHandler(
            _apiKeyRepositoryMock.Object,
            _organizationRepositoryMock.Object,
            _loggerMock.Object);

        _testOrganization = Organization.Provision(OrganizationId.Create(), "Test Org", "Test Organization");
        _organizationRepositoryMock.Setup(r => r.GetAll())
            .Returns(new List<Organization> { _testOrganization });
        _apiKeyRepositoryMock.Setup(r => r.GetByOrganization(It.IsAny<OrganizationId>()))
            .Returns(new List<ApiKey>());
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessWithFullKey()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ApiKey.Should().NotBeNull();
        result.ApiKey!.Name.Should().Be("Test Key");
        result.ApiKey.FullKey.Should().StartWith("rsgo_");
        result.ApiKey.FullKey.Should().HaveLength(37); // rsgo_ (5) + 32 random chars
        result.ApiKey.KeyPrefix.Should().HaveLength(12);
        _apiKeyRepositoryMock.Verify(r => r.Add(It.IsAny<ApiKey>()), Times.Once);
        _apiKeyRepositoryMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyName_ReturnsFailure()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("name is required");
        _apiKeyRepositoryMock.Verify(r => r.Add(It.IsAny<ApiKey>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceName_ReturnsFailure()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "   ",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("name is required");
    }

    [Fact]
    public async Task Handle_NoPermissions_ReturnsFailure()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            Permissions = new List<string>()
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("permission is required");
    }

    [Fact]
    public async Task Handle_DuplicateActiveName_ReturnsFailure()
    {
        var existingKey = ApiKey.Create(
            ApiKeyId.Create(),
            _testOrganization.Id,
            "Existing Key",
            "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            "rsgo_existin",
            new List<string> { "Hooks.Redeploy" });

        _apiKeyRepositoryMock.Setup(r => r.GetByOrganization(_testOrganization.Id))
            .Returns(new List<ApiKey> { existingKey });

        var request = new CreateApiKeyRequest
        {
            Name = "Existing Key",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_DuplicateRevokedName_AllowsCreation()
    {
        var revokedKey = ApiKey.Create(
            ApiKeyId.Create(),
            _testOrganization.Id,
            "Revoked Key",
            "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            "rsgo_revoked",
            new List<string> { "Hooks.Redeploy" });
        revokedKey.Revoke("No longer needed");

        _apiKeyRepositoryMock.Setup(r => r.GetByOrganization(_testOrganization.Id))
            .Returns(new List<ApiKey> { revokedKey });

        var request = new CreateApiKeyRequest
        {
            Name = "Revoked Key",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoOrganization_ReturnsFailure()
    {
        _organizationRepositoryMock.Setup(r => r.GetAll())
            .Returns(new List<Organization>());

        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Organization not set");
    }

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsFailure()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            Permissions = new List<string> { "Hooks.Redeploy" },
            EnvironmentId = "not-a-guid"
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_ValidEnvironmentId_CreatesKeyWithScope()
    {
        var envId = Guid.NewGuid();
        var request = new CreateApiKeyRequest
        {
            Name = "Scoped Key",
            Permissions = new List<string> { "Hooks.Redeploy" },
            EnvironmentId = envId.ToString()
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
        _apiKeyRepositoryMock.Verify(r => r.Add(It.Is<ApiKey>(k => k.EnvironmentId == envId)), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpiry_CreatesKeyWithExpiration()
    {
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var request = new CreateApiKeyRequest
        {
            Name = "Expiring Key",
            Permissions = new List<string> { "Hooks.Redeploy" },
            ExpiresAt = expiresAt
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
        _apiKeyRepositoryMock.Verify(r => r.Add(It.Is<ApiKey>(k => k.ExpiresAt == expiresAt)), Times.Once);
    }

    [Fact]
    public async Task Handle_GeneratedKey_HasCorrectFormat()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "Format Test Key",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ApiKey!.FullKey.Should().StartWith("rsgo_");
        result.ApiKey.FullKey.Should().HaveLength(37); // rsgo_ (5) + 32 random
        result.ApiKey.FullKey[5..].Should().MatchRegex("^[a-z0-9]{32}$");
    }

    [Fact]
    public async Task Handle_GeneratedKeys_AreUnique()
    {
        var request1 = new CreateApiKeyRequest
        {
            Name = "Unique Key 1",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };
        var request2 = new CreateApiKeyRequest
        {
            Name = "Unique Key 2",
            Permissions = new List<string> { "Hooks.Redeploy" }
        };

        var result1 = await _handler.Handle(new CreateApiKeyCommand(request1), CancellationToken.None);
        var result2 = await _handler.Handle(new CreateApiKeyCommand(request2), CancellationToken.None);

        result1.ApiKey!.FullKey.Should().NotBe(result2.ApiKey!.FullKey);
    }

    [Fact]
    public async Task Handle_MultiplePermissions_CreatesKeyWithAllPermissions()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "Multi Perm Key",
            Permissions = new List<string> { "Hooks.Redeploy", "Hooks.Upgrade", "Hooks.SyncSources" }
        };

        var result = await _handler.Handle(new CreateApiKeyCommand(request), CancellationToken.None);

        result.Success.Should().BeTrue();
        _apiKeyRepositoryMock.Verify(r => r.Add(It.Is<ApiKey>(k =>
            k.Permissions.Count == 3 &&
            k.Permissions.Contains("Hooks.Redeploy") &&
            k.Permissions.Contains("Hooks.Upgrade") &&
            k.Permissions.Contains("Hooks.SyncSources"))), Times.Once);
    }
}

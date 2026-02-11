using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for ApiKey aggregate root.
/// </summary>
public class ApiKeyTests
{
    private readonly OrganizationId _organizationId = new OrganizationId(Guid.NewGuid());
    private const string ValidKeyHash = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";
    private const string ValidKeyPrefix = "rsgo_a1b2c3d";
    private static readonly string[] DefaultPermissions = new[] { "Hooks.Redeploy" };

    private ApiKey CreateApiKey(
        string name = "CI/CD Pipeline",
        string keyHash = ValidKeyHash,
        string keyPrefix = ValidKeyPrefix,
        IEnumerable<string>? permissions = null,
        Guid? environmentId = null,
        DateTime? expiresAt = null)
    {
        return ApiKey.Create(
            ApiKeyId.Create(),
            _organizationId,
            name,
            keyHash,
            keyPrefix,
            permissions ?? DefaultPermissions,
            environmentId,
            expiresAt);
    }

    #region Creation Tests

    [Fact]
    public void Create_WithValidData_CreatesApiKey()
    {
        // Arrange
        var id = ApiKeyId.Create();
        var permissions = new[] { "Hooks.Redeploy", "Hooks.Upgrade" };

        // Act
        var apiKey = ApiKey.Create(id, _organizationId, "CI Pipeline", ValidKeyHash, ValidKeyPrefix, permissions);

        // Assert
        apiKey.Id.Should().Be(id);
        apiKey.OrganizationId.Should().Be(_organizationId);
        apiKey.Name.Should().Be("CI Pipeline");
        apiKey.KeyHash.Should().Be(ValidKeyHash);
        apiKey.KeyPrefix.Should().Be(ValidKeyPrefix);
        apiKey.Permissions.Should().HaveCount(2);
        apiKey.Permissions.Should().Contain("Hooks.Redeploy");
        apiKey.Permissions.Should().Contain("Hooks.Upgrade");
        apiKey.EnvironmentId.Should().BeNull();
        apiKey.ExpiresAt.Should().BeNull();
        apiKey.IsRevoked.Should().BeFalse();
        apiKey.RevokedAt.Should().BeNull();
        apiKey.RevokedReason.Should().BeNull();
        apiKey.LastUsedAt.Should().BeNull();
        apiKey.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithAllOptionalFields_CreatesApiKey()
    {
        // Arrange
        var envId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var apiKey = CreateApiKey(environmentId: envId, expiresAt: expiresAt);

        // Assert
        apiKey.EnvironmentId.Should().Be(envId);
        apiKey.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateApiKey(name: "");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*name*required*");
    }

    [Fact]
    public void Create_WithTooLongName_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateApiKey(name: new string('a', 101));

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*100 characters*");
    }

    [Fact]
    public void Create_WithEmptyKeyHash_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateApiKey(keyHash: "");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*hash*required*");
    }

    [Fact]
    public void Create_WithInvalidKeyHashLength_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateApiKey(keyHash: "tooshort");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*64 characters*");
    }

    [Fact]
    public void Create_WithEmptyKeyPrefix_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateApiKey(keyPrefix: "");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*prefix*required*");
    }

    [Fact]
    public void Create_WithEmptyPermissions_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateApiKey(permissions: Array.Empty<string>());

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*permission*required*");
    }

    [Fact]
    public void Create_WithNullPermissions_ThrowsArgumentException()
    {
        // Act
        var act = () => ApiKey.Create(
            ApiKeyId.Create(), _organizationId, "Test", ValidKeyHash, ValidKeyPrefix, null!);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*permission*required*");
    }

    #endregion

    #region Domain Events

    [Fact]
    public void Create_RaisesApiKeyCreatedEvent()
    {
        // Act
        var apiKey = CreateApiKey(name: "Pipeline Key");

        // Assert
        apiKey.DomainEvents.Should().ContainSingle();
        var domainEvent = apiKey.DomainEvents.First();
        domainEvent.Should().BeOfType<ApiKeyCreated>();
        var created = (ApiKeyCreated)domainEvent;
        created.ApiKeyId.Should().Be(apiKey.Id);
        created.Name.Should().Be("Pipeline Key");
    }

    [Fact]
    public void Revoke_RaisesApiKeyRevokedEvent()
    {
        // Arrange
        var apiKey = CreateApiKey(name: "Test Key");
        apiKey.ClearDomainEvents();

        // Act
        apiKey.Revoke("Compromised");

        // Assert
        apiKey.DomainEvents.Should().ContainSingle();
        var domainEvent = apiKey.DomainEvents.First();
        domainEvent.Should().BeOfType<ApiKeyRevoked>();
        var revoked = (ApiKeyRevoked)domainEvent;
        revoked.ApiKeyId.Should().Be(apiKey.Id);
        revoked.Name.Should().Be("Test Key");
        revoked.Reason.Should().Be("Compromised");
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public void Revoke_WithReason_RevokesApiKey()
    {
        // Arrange
        var apiKey = CreateApiKey();

        // Act
        apiKey.Revoke("Key compromised");

        // Assert
        apiKey.IsRevoked.Should().BeTrue();
        apiKey.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        apiKey.RevokedReason.Should().Be("Key compromised");
    }

    [Fact]
    public void Revoke_WithoutReason_RevokesApiKey()
    {
        // Arrange
        var apiKey = CreateApiKey();

        // Act
        apiKey.Revoke();

        // Assert
        apiKey.IsRevoked.Should().BeTrue();
        apiKey.RevokedAt.Should().NotBeNull();
        apiKey.RevokedReason.Should().BeNull();
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsArgumentException()
    {
        // Arrange
        var apiKey = CreateApiKey();
        apiKey.Revoke("First revocation");

        // Act
        var act = () => apiKey.Revoke("Second revocation");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*already revoked*");
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_NoExpiryDate_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(expiresAt: null);

        // Act & Assert
        apiKey.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_FutureExpiryDate_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(expiresAt: DateTime.UtcNow.AddDays(30));

        // Act & Assert
        apiKey.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_PastExpiryDate_ReturnsTrue()
    {
        // Arrange
        var apiKey = CreateApiKey(expiresAt: DateTime.UtcNow.AddDays(-1));

        // Act & Assert
        apiKey.IsExpired().Should().BeTrue();
    }

    #endregion

    #region IsValid Tests

    [Fact]
    public void IsValid_ActiveKey_ReturnsTrue()
    {
        // Arrange
        var apiKey = CreateApiKey();

        // Act & Assert
        apiKey.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_RevokedKey_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey();
        apiKey.Revoke();

        // Act & Assert
        apiKey.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_ExpiredKey_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(expiresAt: DateTime.UtcNow.AddDays(-1));

        // Act & Assert
        apiKey.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_RevokedAndExpiredKey_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(expiresAt: DateTime.UtcNow.AddDays(-1));
        apiKey.Revoke();

        // Act & Assert
        apiKey.IsValid().Should().BeFalse();
    }

    #endregion

    #region HasPermission Tests

    [Fact]
    public void HasPermission_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "Hooks.Redeploy" });

        // Act & Assert
        apiKey.HasPermission("Hooks.Redeploy").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_NoMatch_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "Hooks.Redeploy" });

        // Act & Assert
        apiKey.HasPermission("Hooks.Upgrade").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WildcardAll_MatchesEverything()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "*.*" });

        // Act & Assert
        apiKey.HasPermission("Hooks.Redeploy").Should().BeTrue();
        apiKey.HasPermission("ApiKeys.Create").Should().BeTrue();
        apiKey.HasPermission("Anything.Whatever").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ResourceWildcard_MatchesAllActionsOnResource()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "Hooks.*" });

        // Act & Assert
        apiKey.HasPermission("Hooks.Redeploy").Should().BeTrue();
        apiKey.HasPermission("Hooks.Upgrade").Should().BeTrue();
        apiKey.HasPermission("Hooks.SyncSources").Should().BeTrue();
        apiKey.HasPermission("ApiKeys.Create").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_MultiplePermissions_MatchesAny()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "Hooks.Redeploy", "Hooks.Upgrade" });

        // Act & Assert
        apiKey.HasPermission("Hooks.Redeploy").Should().BeTrue();
        apiKey.HasPermission("Hooks.Upgrade").Should().BeTrue();
        apiKey.HasPermission("Hooks.SyncSources").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_EmptyInput_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "*.*" });

        // Act & Assert
        apiKey.HasPermission("").Should().BeFalse();
        apiKey.HasPermission(null!).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_InvalidFormat_ReturnsFalse()
    {
        // Arrange
        var apiKey = CreateApiKey(permissions: new[] { "Hooks.Redeploy" });

        // Act & Assert
        apiKey.HasPermission("NoDotsHere").Should().BeFalse();
        apiKey.HasPermission("Too.Many.Dots").Should().BeFalse();
    }

    #endregion

    #region RecordUsage Tests

    [Fact]
    public void RecordUsage_UpdatesLastUsedAt()
    {
        // Arrange
        var apiKey = CreateApiKey();
        apiKey.LastUsedAt.Should().BeNull();

        // Act
        apiKey.RecordUsage();

        // Assert
        apiKey.LastUsedAt.Should().NotBeNull();
        apiKey.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordUsage_CalledMultipleTimes_UpdatesTimestamp()
    {
        // Arrange
        var apiKey = CreateApiKey();
        apiKey.RecordUsage();
        var firstUsage = apiKey.LastUsedAt;

        // Act
        apiKey.RecordUsage();

        // Assert
        apiKey.LastUsedAt.Should().BeOnOrAfter(firstUsage!.Value);
    }

    #endregion

    #region ApiKeyId Tests

    [Fact]
    public void ApiKeyId_Create_GeneratesNewGuid()
    {
        // Act
        var id = ApiKeyId.Create();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ApiKeyId_FromGuid_PreservesValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = ApiKeyId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ApiKeyId_EmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => new ApiKeyId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void ApiKeyId_EqualityByValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id1 = ApiKeyId.FromGuid(guid);
        var id2 = ApiKeyId.FromGuid(guid);

        // Assert
        id1.Should().Be(id2);
    }

    #endregion
}

using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Registries;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for Registry aggregate root and image pattern matching.
/// </summary>
public class RegistryTests
{
    private readonly OrganizationId _organizationId = new OrganizationId(Guid.NewGuid());

    #region Creation Tests

    [Fact]
    public void Create_WithValidData_CreatesRegistry()
    {
        // Arrange
        var registryId = RegistryId.Create();

        // Act
        var registry = Registry.Create(
            registryId,
            _organizationId,
            "Docker Hub",
            "https://index.docker.io/v1/",
            "myuser",
            "mypassword");

        // Assert
        registry.Id.Should().Be(registryId);
        registry.OrganizationId.Should().Be(_organizationId);
        registry.Name.Should().Be("Docker Hub");
        registry.Url.Should().Be("https://index.docker.io/v1");
        registry.Username.Should().Be("myuser");
        registry.Password.Should().Be("mypassword");
        registry.IsDefault.Should().BeFalse();
        registry.HasCredentials.Should().BeTrue();
        registry.ImagePatterns.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithoutCredentials_CreatesRegistry()
    {
        // Arrange
        var registryId = RegistryId.Create();

        // Act
        var registry = Registry.Create(
            registryId,
            _organizationId,
            "GitHub Container Registry",
            "https://ghcr.io");

        // Assert
        registry.Username.Should().BeNull();
        registry.Password.Should().BeNull();
        registry.HasCredentials.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsException()
    {
        // Arrange
        var registryId = RegistryId.Create();

        // Act
        var act = () => Registry.Create(registryId, _organizationId, "", "https://docker.io");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*name*required*");
    }

    [Fact]
    public void Create_WithEmptyUrl_ThrowsException()
    {
        // Arrange
        var registryId = RegistryId.Create();

        // Act
        var act = () => Registry.Create(registryId, _organizationId, "Docker Hub", "");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*URL*required*");
    }

    [Fact]
    public void Create_NormalizesUrl_AddsHttps()
    {
        // Arrange
        var registryId = RegistryId.Create();

        // Act
        var registry = Registry.Create(registryId, _organizationId, "Private", "registry.example.com");

        // Assert
        registry.Url.Should().Be("https://registry.example.com");
    }

    [Fact]
    public void Create_NormalizesUrl_RemovesTrailingSlash()
    {
        // Arrange
        var registryId = RegistryId.Create();

        // Act
        var registry = Registry.Create(registryId, _organizationId, "Private", "https://registry.example.com/");

        // Assert
        registry.Url.Should().Be("https://registry.example.com");
    }

    #endregion

    #region Image Pattern Management Tests

    [Fact]
    public void SetImagePatterns_WithPatterns_SetsPatterns()
    {
        // Arrange
        var registry = CreateRegistry();
        var patterns = new[] { "library/*", "myorg/*", "ghcr.io/*" };

        // Act
        registry.SetImagePatterns(patterns);

        // Assert
        registry.ImagePatterns.Should().HaveCount(3);
        registry.ImagePatterns.Should().Contain("library/*");
        registry.ImagePatterns.Should().Contain("myorg/*");
        registry.ImagePatterns.Should().Contain("ghcr.io/*");
    }

    [Fact]
    public void SetImagePatterns_WithEmptyList_ClearsPatterns()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "library/*" });

        // Act
        registry.SetImagePatterns(Array.Empty<string>());

        // Assert
        registry.ImagePatterns.Should().BeEmpty();
    }

    [Fact]
    public void SetImagePatterns_TrimsWhitespace()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.SetImagePatterns(new[] { "  library/*  ", " myorg/* " });

        // Assert
        registry.ImagePatterns.Should().Contain("library/*");
        registry.ImagePatterns.Should().Contain("myorg/*");
    }

    [Fact]
    public void SetImagePatterns_IgnoresEmptyStrings()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.SetImagePatterns(new[] { "library/*", "", "  ", "myorg/*" });

        // Assert
        registry.ImagePatterns.Should().HaveCount(2);
    }

    [Fact]
    public void AddImagePattern_AddsPattern()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.AddImagePattern("library/*");
        registry.AddImagePattern("myorg/*");

        // Assert
        registry.ImagePatterns.Should().HaveCount(2);
        registry.ImagePatterns.Should().Contain("library/*");
    }

    [Fact]
    public void AddImagePattern_DoesNotDuplicate()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.AddImagePattern("library/*");

        // Act
        registry.AddImagePattern("library/*");

        // Assert
        registry.ImagePatterns.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveImagePattern_RemovesPattern()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "library/*", "myorg/*" });

        // Act
        registry.RemoveImagePattern("library/*");

        // Assert
        registry.ImagePatterns.Should().HaveCount(1);
        registry.ImagePatterns.Should().NotContain("library/*");
    }

    #endregion

    #region Image Pattern Matching Tests

    [Theory]
    [InlineData("library/nginx", "library/*", true)]
    [InlineData("library/nginx:latest", "library/*", true)]
    [InlineData("library/redis", "library/*", true)]
    [InlineData("myuser/myimage", "myuser/*", true)]
    [InlineData("myuser/myimage:v1.0", "myuser/*", true)]
    [InlineData("otheruser/myimage", "myuser/*", false)]
    [InlineData("nginx", "nginx", true)] // Exact match for simple image
    [InlineData("nginx:latest", "nginx", true)] // Tag stripped
    [InlineData("redis", "nginx", false)] // No match
    public void MatchesImage_WithSimpleWildcard_MatchesCorrectly(string image, string pattern, bool expected)
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { pattern });

        // Act
        var result = registry.MatchesImage(image);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ghcr.io/myorg/myimage", "ghcr.io/**", true)]  // ** matches across segments
    [InlineData("ghcr.io/myorg/myimage:latest", "ghcr.io/**", true)]
    [InlineData("ghcr.io/myimage", "ghcr.io/*", true)]  // * matches single segment
    [InlineData("ghcr.io/other/image", "ghcr.io/myorg/*", false)]
    [InlineData("ghcr.io/myorg/subdir/image", "ghcr.io/myorg/**", true)]
    [InlineData("docker.io/library/nginx", "ghcr.io/*", false)]
    public void MatchesImage_WithRegistryPrefix_MatchesCorrectly(string image, string pattern, bool expected)
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { pattern });

        // Act
        var result = registry.MatchesImage(image);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("myregistry.com:5000/myimage", "myregistry.com:5000/*", true)]
    [InlineData("localhost:5000/test", "localhost:5000/*", true)]
    [InlineData("localhost:5000/org/test", "localhost:5000/**", true)]
    public void MatchesImage_WithPort_MatchesCorrectly(string image, string pattern, bool expected)
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { pattern });

        // Act
        var result = registry.MatchesImage(image);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesImage_WithMultiplePatterns_MatchesAny()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "library/*", "myorg/*", "ghcr.io/**" });

        // Act & Assert
        registry.MatchesImage("library/nginx").Should().BeTrue();
        registry.MatchesImage("myorg/app").Should().BeTrue();
        registry.MatchesImage("ghcr.io/someone/app").Should().BeTrue();
        registry.MatchesImage("quay.io/other/app").Should().BeFalse();
    }

    [Fact]
    public void MatchesImage_CaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "myorg/*" });

        // Act & Assert
        registry.MatchesImage("myorg/app").Should().BeTrue();
        registry.MatchesImage("MYORG/APP").Should().BeTrue();
        registry.MatchesImage("MyOrg/App").Should().BeTrue();
    }

    [Fact]
    public void MatchesImage_WithDigest_StripsDigest()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "myorg/*" });

        // Act
        var result = registry.MatchesImage("myorg/app@sha256:abc123");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region URL-based Matching (Fallback) Tests

    [Fact]
    public void MatchesImage_WithoutPatterns_FallsBackToUrlMatching()
    {
        // Arrange - Docker Hub registry
        var registry = Registry.Create(
            RegistryId.Create(),
            _organizationId,
            "Docker Hub",
            "https://index.docker.io/v1/");

        // Act & Assert - Docker Hub images (no explicit registry) should match
        registry.MatchesImage("nginx").Should().BeTrue();
        registry.MatchesImage("library/nginx").Should().BeTrue();
        registry.MatchesImage("myuser/myimage").Should().BeTrue();
        registry.MatchesImage("ghcr.io/other/image").Should().BeFalse();
    }

    [Fact]
    public void MatchesImage_WithGhcrUrl_MatchesGhcrImages()
    {
        // Arrange
        var registry = Registry.Create(
            RegistryId.Create(),
            _organizationId,
            "GitHub Container Registry",
            "https://ghcr.io");

        // Act & Assert
        registry.MatchesImage("ghcr.io/myorg/myimage").Should().BeTrue();
        registry.MatchesImage("ghcr.io/other/app:latest").Should().BeTrue();
        registry.MatchesImage("nginx").Should().BeFalse();
        registry.MatchesImage("docker.io/library/nginx").Should().BeFalse();
    }

    [Fact]
    public void MatchesImage_PatternsOverrideUrlMatching()
    {
        // Arrange - Docker Hub URL but specific patterns
        var registry = Registry.Create(
            RegistryId.Create(),
            _organizationId,
            "Docker Hub Subset",
            "https://index.docker.io/v1/");
        registry.SetImagePatterns(new[] { "myspecificorg/*" });

        // Act & Assert - Only pattern should match, not all Docker Hub images
        registry.MatchesImage("myspecificorg/app").Should().BeTrue();
        registry.MatchesImage("nginx").Should().BeFalse(); // Would match URL-based, but patterns take precedence
        registry.MatchesImage("otherorg/app").Should().BeFalse();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MatchesImage_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "library/*" });

        // Act
        var result = registry.MatchesImage("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesImage_WithNull_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetImagePatterns(new[] { "library/*" });

        // Act
        var result = registry.MatchesImage(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetRegistryHost_ExtractsHostCorrectly()
    {
        // Arrange & Act & Assert
        CreateRegistryWithUrl("https://ghcr.io").GetRegistryHost().Should().Be("ghcr.io");
        CreateRegistryWithUrl("https://index.docker.io/v1/").GetRegistryHost().Should().Be("index.docker.io");
        CreateRegistryWithUrl("http://localhost:5000").GetRegistryHost().Should().Be("localhost:5000");
        CreateRegistryWithUrl("registry.example.com").GetRegistryHost().Should().Be("registry.example.com");
    }

    #endregion

    #region Default Registry Tests

    [Fact]
    public void SetAsDefault_SetsIsDefault()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.SetAsDefault();

        // Assert
        registry.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void UnsetAsDefault_UnsetsIsDefault()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.SetAsDefault();

        // Act
        registry.UnsetAsDefault();

        // Assert
        registry.IsDefault.Should().BeFalse();
    }

    #endregion

    #region Credential Update Tests

    [Fact]
    public void UpdateCredentials_UpdatesCredentials()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        registry.UpdateCredentials("newuser", "newpass");

        // Assert
        registry.Username.Should().Be("newuser");
        registry.Password.Should().Be("newpass");
        registry.HasCredentials.Should().BeTrue();
    }

    [Fact]
    public void UpdateCredentials_ClearsCredentials()
    {
        // Arrange
        var registry = Registry.Create(
            RegistryId.Create(),
            _organizationId,
            "Test",
            "https://test.io",
            "user",
            "pass");

        // Act
        registry.UpdateCredentials(null, null);

        // Assert
        registry.HasCredentials.Should().BeFalse();
    }

    #endregion

    #region Helpers

    private Registry CreateRegistry()
    {
        return Registry.Create(
            RegistryId.Create(),
            _organizationId,
            "Test Registry",
            "https://test.io");
    }

    private Registry CreateRegistryWithUrl(string url)
    {
        return Registry.Create(
            RegistryId.Create(),
            _organizationId,
            "Test Registry",
            url);
    }

    #endregion
}

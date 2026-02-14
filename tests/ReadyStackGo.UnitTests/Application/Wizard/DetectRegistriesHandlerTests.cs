using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Wizard.DetectRegistries;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.StackManagement.Stacks;
using DeploymentOrgId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.UnitTests.Application.Wizard;

public class DetectRegistriesHandlerTests
{
    private readonly Mock<IProductCache> _productCacheMock = new();
    private readonly Mock<IImageReferenceExtractor> _extractorMock = new();
    private readonly Mock<IRegistryRepository> _registryRepoMock = new();
    private readonly Mock<IOrganizationRepository> _orgRepoMock = new();
    private readonly Mock<IRegistryAccessChecker> _accessCheckerMock = new();
    private readonly Mock<ILogger<DetectRegistriesHandler>> _loggerMock = new();

    private DetectRegistriesHandler CreateHandler()
        => new(_productCacheMock.Object, _extractorMock.Object,
            _registryRepoMock.Object, _orgRepoMock.Object,
            _accessCheckerMock.Object, _loggerMock.Object);

    private static StackDefinition CreateStack(params string[] images)
    {
        var services = images.Select(img => new ServiceTemplate
        {
            Name = $"svc-{Guid.NewGuid():N}",
            Image = img
        }).ToList();

        return new StackDefinition(
            sourceId: "test-source",
            name: "test-stack",
            productId: new ProductId("test-source:test-product"),
            services: services);
    }

    private void SetupStacks(params StackDefinition[] stacks)
    {
        _productCacheMock.Setup(c => c.GetAllStacks()).Returns(stacks);
    }

    private void SetupExtractorResult(IReadOnlyList<RegistryArea> areas)
    {
        _extractorMock
            .Setup(e => e.GroupByRegistryArea(It.IsAny<IEnumerable<string>>()))
            .Returns(areas);
    }

    private void SetupOrganization()
    {
        var org = Organization.Provision(
            OrganizationId.NewId(), "Test Org", "Test Organization");
        _orgRepoMock.Setup(r => r.GetAll()).Returns([org]);
        _registryRepoMock
            .Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns([]);
    }

    private void SetupNoOrganization()
    {
        _orgRepoMock.Setup(r => r.GetAll()).Returns([]);
    }

    private void SetupAccessCheckerReturns(RegistryAccessLevel level)
    {
        _accessCheckerMock
            .Setup(c => c.CheckAccessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);
    }

    private void SetupAccessCheckerForHost(string host, RegistryAccessLevel level)
    {
        _accessCheckerMock
            .Setup(c => c.CheckAccessAsync(
                host, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(level);
    }

    private void SetupParseReturns(string image, string host, string ns, string repo)
    {
        _extractorMock
            .Setup(e => e.Parse(image))
            .Returns(new ParsedImageReference(image, host, ns, repo, null));
    }

    #region No stacks — default registries

    [Fact]
    public async Task Handle_NoStacks_ReturnsDefaultRegistryAreas()
    {
        SetupStacks();
        SetupNoOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.Should().NotBeEmpty();
        result.Areas.Should().Contain(a => a.Host == "docker.io");
        result.Areas.Should().Contain(a => a.Host == "ghcr.io");
        result.Areas.Should().Contain(a => a.Host == "registry.gitlab.com");
        result.Areas.Should().Contain(a => a.Host == "quay.io");
    }

    [Fact]
    public async Task Handle_NoStacks_DefaultDockerHubIsLikelyPublic()
    {
        SetupStacks();
        SetupNoOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        var dockerHub = result.Areas.First(a => a.Host == "docker.io");
        dockerHub.IsLikelyPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoStacks_DoesNotCallExtractor()
    {
        SetupStacks();
        SetupNoOrganization();
        var handler = CreateHandler();

        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        _extractorMock.Verify(
            e => e.GroupByRegistryArea(It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    #endregion

    #region With stacks — extraction

    [Fact]
    public async Task Handle_WithStacks_CallsExtractorWithImages()
    {
        var stack = CreateStack("nginx:latest", "amssolution/ams-api:1.0");
        SetupStacks(stack);
        SetupExtractorResult([]);
        SetupNoOrganization();
        var handler = CreateHandler();

        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        _extractorMock.Verify(
            e => e.GroupByRegistryArea(It.Is<IEnumerable<string>>(imgs =>
                imgs.Contains("nginx:latest") && imgs.Contains("amssolution/ams-api:1.0"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithStacks_ReturnsExtractorResults()
    {
        var stack = CreateStack("nginx:latest");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub (Official Images)", true, ["nginx:latest"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);
        SetupNoOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.Should().HaveCount(1);
        result.Areas[0].Host.Should().Be("docker.io");
        result.Areas[0].SuggestedPattern.Should().Be("library/*");
    }

    [Fact]
    public async Task Handle_DuplicateImages_DeduplicatedBeforeExtraction()
    {
        var stack1 = CreateStack("nginx:latest");
        var stack2 = CreateStack("nginx:latest");
        SetupStacks(stack1, stack2);
        SetupExtractorResult([]);
        SetupNoOrganization();
        var handler = CreateHandler();

        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        _extractorMock.Verify(
            e => e.GroupByRegistryArea(It.Is<IEnumerable<string>>(imgs =>
                imgs.Count() == 1)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyImageStrings_FilteredOut()
    {
        var stack = CreateStack("nginx:latest", "", "  ");
        SetupStacks(stack);
        SetupExtractorResult([]);
        SetupNoOrganization();
        var handler = CreateHandler();

        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        _extractorMock.Verify(
            e => e.GroupByRegistryArea(It.Is<IEnumerable<string>>(imgs =>
                imgs.All(i => !string.IsNullOrWhiteSpace(i)))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleStacks_CollectsAllImages()
    {
        var stack1 = CreateStack("nginx:latest", "redis:7");
        var stack2 = CreateStack("postgres:16", "amssolution/ams-api:1.0");
        SetupStacks(stack1, stack2);
        SetupExtractorResult([]);
        SetupNoOrganization();
        var handler = CreateHandler();

        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        _extractorMock.Verify(
            e => e.GroupByRegistryArea(It.Is<IEnumerable<string>>(imgs =>
                imgs.Count() == 4)),
            Times.Once);
    }

    #endregion

    #region IsConfigured detection

    [Fact]
    public async Task Handle_NoExistingRegistries_AllAreasNotConfigured()
    {
        var stack = CreateStack("nginx:latest");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub", true, ["nginx:latest"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.Should().AllSatisfy(a => a.IsConfigured.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_MatchingRegistryExists_IsConfiguredTrue()
    {
        var stack = CreateStack("ghcr.io/myorg/app:v1");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("ghcr.io", "myorg", "ghcr.io/myorg/*",
                "ghcr.io – myorg", false, ["ghcr.io/myorg/app:v1"])
        ]);
        SetupParseReturns("ghcr.io/myorg/app:v1", "ghcr.io", "myorg", "app");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);

        var org = Organization.Provision(OrganizationId.NewId(), "Test Org", "Test Organization");
        _orgRepoMock.Setup(r => r.GetAll()).Returns([org]);

        var registry = Registry.Create(
            RegistryId.NewId(),
            DeploymentOrgId.FromIdentityAccess(org.Id),
            "GHCR",
            "https://ghcr.io",
            "user", "token");
        registry.SetImagePatterns(["ghcr.io/myorg/*"]);

        _registryRepoMock
            .Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns([registry]);

        var handler = CreateHandler();
        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.Should().HaveCount(1);
        result.Areas[0].IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoOrganization_AllNotConfigured()
    {
        var stack = CreateStack("nginx:latest");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub", true, ["nginx:latest"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);
        SetupNoOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.Should().AllSatisfy(a => a.IsConfigured.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_MixedConfiguredAndNotConfigured()
    {
        var stack = CreateStack("nginx:latest", "ghcr.io/myorg/app:v1");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub", true, ["nginx:latest"]),
            new RegistryArea("ghcr.io", "myorg", "ghcr.io/myorg/*",
                "ghcr.io – myorg", false, ["ghcr.io/myorg/app:v1"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupParseReturns("ghcr.io/myorg/app:v1", "ghcr.io", "myorg", "app");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);

        var org = Organization.Provision(OrganizationId.NewId(), "Test Org", "Test Organization");
        _orgRepoMock.Setup(r => r.GetAll()).Returns([org]);

        var registry = Registry.Create(
            RegistryId.NewId(),
            DeploymentOrgId.FromIdentityAccess(org.Id),
            "GHCR",
            "https://ghcr.io",
            "user", "token");
        registry.SetImagePatterns(["ghcr.io/myorg/*"]);

        _registryRepoMock
            .Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns([registry]);

        var handler = CreateHandler();
        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.First(a => a.Host == "docker.io").IsConfigured.Should().BeFalse();
        result.Areas.First(a => a.Host == "ghcr.io").IsConfigured.Should().BeTrue();
    }

    #endregion

    #region Field mapping

    [Fact]
    public async Task Handle_MapsAllFieldsCorrectly()
    {
        var stack = CreateStack("amssolution/ams-api:1.0");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "amssolution", "amssolution/*",
                "Docker Hub – amssolution", false,
                ["amssolution/ams-api:1.0"])
        ]);
        SetupParseReturns("amssolution/ams-api:1.0", "docker.io", "amssolution", "ams-api");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);
        SetupNoOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        var area = result.Areas.Single();
        area.Host.Should().Be("docker.io");
        area.Namespace.Should().Be("amssolution");
        area.SuggestedPattern.Should().Be("amssolution/*");
        area.SuggestedName.Should().Be("Docker Hub – amssolution");
        area.IsLikelyPublic.Should().BeFalse();
        area.IsConfigured.Should().BeFalse();
        area.Images.Should().Contain("amssolution/ams-api:1.0");
    }

    #endregion

    #region Runtime access check

    [Fact]
    public async Task Handle_AccessCheckerReturnsPublic_OverridesStaticHint()
    {
        var stack = CreateStack("ghcr.io/myorg/app:v1");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("ghcr.io", "myorg", "ghcr.io/myorg/*",
                "ghcr.io – myorg", false, ["ghcr.io/myorg/app:v1"])
        ]);
        SetupParseReturns("ghcr.io/myorg/app:v1", "ghcr.io", "myorg", "app");
        SetupAccessCheckerForHost("ghcr.io", RegistryAccessLevel.Public);
        SetupNoOrganization();

        var handler = CreateHandler();
        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.Single().IsLikelyPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AccessCheckerReturnsAuthRequired_OverridesStaticHint()
    {
        var stack = CreateStack("nginx:latest");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub", true, ["nginx:latest"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupAccessCheckerForHost("docker.io", RegistryAccessLevel.AuthRequired);
        SetupNoOrganization();

        var handler = CreateHandler();
        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        // Runtime check overrides static hint: was true (static) → false (runtime)
        result.Areas.Single().IsLikelyPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AccessCheckerReturnsUnknown_FallsBackToStaticHint()
    {
        var stack = CreateStack("nginx:latest");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub", true, ["nginx:latest"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupAccessCheckerReturns(RegistryAccessLevel.Unknown);
        SetupNoOrganization();

        var handler = CreateHandler();
        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        // Unknown → fallback to static hint (true)
        result.Areas.Single().IsLikelyPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MixedAccessCheckResults_EachAreaUsesOwnResult()
    {
        var stack = CreateStack("nginx:latest", "ghcr.io/private/app:v1");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("docker.io", "library", "library/*",
                "Docker Hub", false, ["nginx:latest"]),
            new RegistryArea("ghcr.io", "private", "ghcr.io/private/*",
                "ghcr.io – private", false, ["ghcr.io/private/app:v1"])
        ]);
        SetupParseReturns("nginx:latest", "docker.io", "library", "nginx");
        SetupParseReturns("ghcr.io/private/app:v1", "ghcr.io", "private", "app");
        SetupAccessCheckerForHost("docker.io", RegistryAccessLevel.Public);
        SetupAccessCheckerForHost("ghcr.io", RegistryAccessLevel.AuthRequired);
        SetupNoOrganization();

        var handler = CreateHandler();
        var result = await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        result.Areas.First(a => a.Host == "docker.io").IsLikelyPublic.Should().BeTrue();
        result.Areas.First(a => a.Host == "ghcr.io").IsLikelyPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoStacks_DefaultAreas_NoAccessCheckCalled()
    {
        SetupStacks();
        SetupNoOrganization();

        var handler = CreateHandler();
        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        // Default areas have no images → access checker not called for them
        _accessCheckerMock.Verify(
            c => c.CheckAccessAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_CallsAccessCheckerWithParsedImageComponents()
    {
        var stack = CreateStack("ghcr.io/myorg/myapp:v2");
        SetupStacks(stack);
        SetupExtractorResult([
            new RegistryArea("ghcr.io", "myorg", "ghcr.io/myorg/*",
                "ghcr.io – myorg", false, ["ghcr.io/myorg/myapp:v2"])
        ]);
        SetupParseReturns("ghcr.io/myorg/myapp:v2", "ghcr.io", "myorg", "myapp");
        SetupAccessCheckerReturns(RegistryAccessLevel.Public);
        SetupNoOrganization();

        var handler = CreateHandler();
        await handler.Handle(new DetectRegistriesQuery(), CancellationToken.None);

        _accessCheckerMock.Verify(
            c => c.CheckAccessAsync("ghcr.io", "myorg", "myapp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

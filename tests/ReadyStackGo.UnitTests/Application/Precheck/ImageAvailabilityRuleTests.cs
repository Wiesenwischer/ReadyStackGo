using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Application.Precheck;

public class ImageAvailabilityRuleTests
{
    private readonly Mock<IDockerService> _dockerServiceMock = new();
    private readonly Mock<IRegistryAccessChecker> _registryCheckerMock = new();
    private readonly ImageAvailabilityRule _sut;

    public ImageAvailabilityRuleTests()
    {
        _sut = new ImageAvailabilityRule(
            _dockerServiceMock.Object,
            _registryCheckerMock.Object,
            Mock.Of<ILogger<ImageAvailabilityRule>>());
    }

    #region Image Exists Locally

    [Fact]
    public async Task Execute_ImageExistsLocally_ReturnsOK()
    {
        var context = CreateContext([new ServiceTemplate { Name = "web", Image = "nginx:latest" }]);
        _dockerServiceMock.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.OK);
        result[0].Detail.Should().Contain("locally");
    }

    #endregion

    #region Image Not Local — Registry Public

    [Fact]
    public async Task Execute_ImageNotLocal_RegistryPublic_ReturnsOK()
    {
        var context = CreateContext([new ServiceTemplate { Name = "web", Image = "nginx:latest" }]);
        _dockerServiceMock.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _registryCheckerMock.Setup(r => r.CheckAccessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegistryAccessLevel.Public);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.OK);
        result[0].Detail.Should().Contain("registry");
    }

    #endregion

    #region Image Not Local — Registry Auth Required

    [Fact]
    public async Task Execute_ImageNotLocal_RegistryAuthRequired_ReturnsError()
    {
        var context = CreateContext([new ServiceTemplate { Name = "web", Image = "ghcr.io/org/app:1.0" }]);
        _dockerServiceMock.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), "ghcr.io/org/app", "1.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _registryCheckerMock.Setup(r => r.CheckAccessAsync(
                "ghcr.io", "org", "app", It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegistryAccessLevel.AuthRequired);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.Error);
        result[0].Title.Should().Contain("authentication");
    }

    #endregion

    #region Image Not Local — Registry Unknown

    [Fact]
    public async Task Execute_ImageNotLocal_RegistryUnknown_ReturnsWarning()
    {
        var context = CreateContext([new ServiceTemplate { Name = "web", Image = "nginx:latest" }]);
        _dockerServiceMock.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), "nginx", "latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _registryCheckerMock.Setup(r => r.CheckAccessAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegistryAccessLevel.Unknown);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.Warning);
    }

    #endregion

    #region Docker Service Failure

    [Fact]
    public async Task Execute_DockerServiceThrows_ReturnsWarning()
    {
        var context = CreateContext([new ServiceTemplate { Name = "web", Image = "nginx:latest" }]);
        _dockerServiceMock.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Docker not reachable"));

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.Warning);
    }

    #endregion

    #region Multiple Services

    [Fact]
    public async Task Execute_MultipleServices_ChecksAll()
    {
        var context = CreateContext([
            new ServiceTemplate { Name = "web", Image = "nginx:latest" },
            new ServiceTemplate { Name = "api", Image = "node:18" }
        ]);
        _dockerServiceMock.Setup(d => d.ImageExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.Severity == PrecheckSeverity.OK);
    }

    #endregion

    #region ParseImageReference

    [Theory]
    [InlineData("nginx", "nginx", "latest")]
    [InlineData("nginx:1.21", "nginx", "1.21")]
    [InlineData("ghcr.io/org/app:v1.0", "ghcr.io/org/app", "v1.0")]
    [InlineData("registry:5000/repo", "registry:5000/repo", "latest")]
    [InlineData("registry:5000/repo:tag", "registry:5000/repo", "tag")]
    [InlineData("image@sha256:abc123", "image", "sha256:abc123")]
    public void ParseImageReference_ParsesCorrectly(string input, string expectedImage, string expectedTag)
    {
        var (image, tag) = ImageAvailabilityRule.ParseImageReference(input);
        image.Should().Be(expectedImage);
        tag.Should().Be(expectedTag);
    }

    #endregion

    #region ParseRegistryComponents

    [Theory]
    [InlineData("nginx", "registry-1.docker.io", "library", "nginx")]
    [InlineData("user/repo", "registry-1.docker.io", "user", "repo")]
    [InlineData("ghcr.io/org/app", "ghcr.io", "org", "app")]
    [InlineData("registry.example.com/ns/app", "registry.example.com", "ns", "app")]
    public void ParseRegistryComponents_ParsesCorrectly(string input, string expectedHost, string expectedNs, string expectedRepo)
    {
        var (host, ns, repo) = ImageAvailabilityRule.ParseRegistryComponents(input);
        host.Should().Be(expectedHost);
        ns.Should().Be(expectedNs);
        repo.Should().Be(expectedRepo);
    }

    #endregion

    #region Helpers

    private static PrecheckContext CreateContext(IReadOnlyList<ServiceTemplate> services)
    {
        return new PrecheckContext
        {
            EnvironmentId = Guid.NewGuid().ToString(),
            StackName = "test-stack",
            StackDefinition = TestHelpers.CreateStackDefinition(services: services),
            Variables = new Dictionary<string, string>(),
            RunningContainers = [],
            ExistingVolumes = [],
        };
    }

    #endregion
}

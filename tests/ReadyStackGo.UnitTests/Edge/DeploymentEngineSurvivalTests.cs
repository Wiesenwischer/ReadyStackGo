using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services.Deployment;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Verifies the survival primitive: <c>RemoveStackAsync</c> never tears down edge-scoped
/// (<c>rsgo.scope=edge</c>) or <c>rsgo.redeploy=ignore</c> containers, while still removing
/// ordinary stack containers. Covers both overloads.
/// </summary>
public class DeploymentEngineSurvivalTests
{
    private readonly Mock<IConfigStore> _configStore = new();
    private readonly Mock<IDockerService> _docker = new();
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IEnvironmentRepository> _envRepo = new();
    private readonly DeploymentEngine _sut;
    private readonly List<string> _removedIds = new();

    private const string EnvId = "11111111-1111-1111-1111-111111111111";
    private const string StackVersion = "test-stack";

    public DeploymentEngineSurvivalTests()
    {
        _sut = new DeploymentEngine(
            _configStore.Object, _docker.Object, _orgRepo.Object, _envRepo.Object,
            new Mock<ILogger<DeploymentEngine>>().Object);

        _configStore.Setup(x => x.GetReleaseConfigAsync()).ReturnsAsync(new ReleaseConfig());
        _configStore.Setup(x => x.SaveReleaseConfigAsync(It.IsAny<ReleaseConfig>())).Returns(Task.CompletedTask);

        _docker.Setup(x => x.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, CancellationToken>((_, id, _, _) => _removedIds.Add(id))
            .Returns(Task.CompletedTask);

        _docker.Setup(x => x.ListContainersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Containers());
    }

    private static List<ContainerDto> Containers() => new()
    {
        new()
        {
            Id = "app", Name = "app", Image = "app:1", State = "running", Status = "running",
            Labels = new Dictionary<string, string> { ["rsgo.stack"] = StackVersion, ["rsgo.context"] = "app" }
        },
        new()
        {
            Id = "edge", Name = "app-edge", Image = "caddy:2.8.4", State = "running", Status = "running",
            Labels = new Dictionary<string, string>
            {
                ["rsgo.stack"] = StackVersion, // even if it carried a stack label, scope must win
                [EdgeConstants.ScopeLabel] = EdgeConstants.ScopeEdge
            }
        },
        new()
        {
            Id = "survivor", Name = "maint", Image = "nginx:1", State = "running", Status = "running",
            Labels = new Dictionary<string, string>
            {
                ["rsgo.stack"] = StackVersion,
                [EdgeConstants.RedeployLabel] = EdgeConstants.RedeployIgnore
            }
        }
    };

    [Fact]
    public async Task RemoveStackAsync_SimpleOverload_ExcludesSurvivors()
    {
        var result = await _sut.RemoveStackAsync(EnvId, StackVersion);

        result.Success.Should().BeTrue();
        _removedIds.Should().ContainSingle().Which.Should().Be("app");
        _removedIds.Should().NotContain("edge");
        _removedIds.Should().NotContain("survivor");
    }

    [Fact]
    public async Task RemoveStackAsync_ProgressOverload_ExcludesSurvivors()
    {
        DeploymentProgressCallback noop = (_, _, _, _, _, _, _, _) => Task.CompletedTask;

        var result = await _sut.RemoveStackAsync(EnvId, StackVersion, noop);

        result.Success.Should().BeTrue();
        _removedIds.Should().ContainSingle().Which.Should().Be("app");
        _removedIds.Should().NotContain("edge", "the edge survives redeploys/removals");
        _removedIds.Should().NotContain("survivor", "rsgo.redeploy=ignore opts out of teardown");
    }
}

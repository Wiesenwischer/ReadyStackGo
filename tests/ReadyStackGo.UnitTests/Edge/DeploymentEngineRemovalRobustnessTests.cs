using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services.Deployment;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Verifies that <see cref="DeploymentEngine.RemoveStackAsync(string,string)"/> removes ALL
/// stack containers reliably even when Docker misbehaves during teardown:
/// transient removal errors are retried, and containers that were missed or recreated by a
/// restart policy are caught by a verification re-list pass. Without this, a straggler survives
/// as an orphaned container while the deployment is still reported as removed.
/// </summary>
public class DeploymentEngineRemovalRobustnessTests
{
    private readonly Mock<IConfigStore> _configStore = new();
    private readonly Mock<IDockerService> _docker = new();
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IEnvironmentRepository> _envRepo = new();
    private readonly TestableDeploymentEngine _sut;

    private const string EnvId = "11111111-1111-1111-1111-111111111111";
    private const string StackVersion = "test-stack";

    public DeploymentEngineRemovalRobustnessTests()
    {
        _sut = new TestableDeploymentEngine(
            _configStore.Object, _docker.Object, _orgRepo.Object, _envRepo.Object,
            new Mock<ILogger<DeploymentEngine>>().Object);

        _configStore.Setup(x => x.GetReleaseConfigAsync()).ReturnsAsync(new ReleaseConfig());
        _configStore.Setup(x => x.SaveReleaseConfigAsync(It.IsAny<ReleaseConfig>())).Returns(Task.CompletedTask);
    }

    private static ContainerDto AppContainer() => new()
    {
        Id = "app", Name = "app", Image = "app:1", State = "running", Status = "running",
        Labels = new Dictionary<string, string> { ["rsgo.stack"] = StackVersion, ["rsgo.context"] = "app" }
    };

    [Fact]
    public async Task RemoveStackAsync_RetriesTransientRemovalFailure_AndSucceeds()
    {
        // ListContainers: snapshot shows the container, re-list shows it gone.
        _docker.SetupSequence(x => x.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerDto> { AppContainer() })
            .ReturnsAsync(new List<ContainerDto>());

        // First remove attempt throws a transient Docker error, second succeeds.
        var attempts = 0;
        _docker.Setup(x => x.RemoveContainerAsync(EnvId, "app", true, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                return attempts == 1
                    ? throw new InvalidOperationException("removal of container app is already in progress")
                    : Task.CompletedTask;
            });

        var result = await _sut.RemoveStackAsync(EnvId, StackVersion);

        result.Success.Should().BeTrue("the transient failure was retried and the container was removed");
        attempts.Should().Be(2, "the removal should be retried once after the transient failure");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveStackAsync_RemovesContainerRecreatedByRestartPolicy_ViaVerificationPass()
    {
        // The container reappears after the first removal (restart-policy race), then is gone.
        _docker.SetupSequence(x => x.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerDto> { AppContainer() }) // initial snapshot
            .ReturnsAsync(new List<ContainerDto> { AppContainer() }) // verification pass 1: still there
            .ReturnsAsync(new List<ContainerDto>());                 // verification pass 2: gone

        _docker.Setup(x => x.RemoveContainerAsync(EnvId, "app", true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RemoveStackAsync(EnvId, StackVersion);

        result.Success.Should().BeTrue("the reappearing container is caught by the verification pass");
        _docker.Verify(x => x.RemoveContainerAsync(EnvId, "app", true, It.IsAny<CancellationToken>()),
            Times.Exactly(2), "the container must be removed again after it reappeared");
    }

    [Fact]
    public async Task RemoveStackAsync_FailsWhenContainerCannotBeRemoved()
    {
        // Container never disappears and every removal attempt fails.
        _docker.Setup(x => x.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<ContainerDto> { AppContainer() });

        _docker.Setup(x => x.RemoveContainerAsync(EnvId, "app", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("device or resource busy"));

        var result = await _sut.RemoveStackAsync(EnvId, StackVersion);

        result.Success.Should().BeFalse("a container that cannot be removed must be reported as a failure");
        result.Errors.Should().NotBeEmpty();
        _docker.Verify(x => x.RemoveContainerAsync(EnvId, "app", true, It.IsAny<CancellationToken>()),
            Times.AtLeast(2), "removal must be retried before giving up");
    }

    /// <summary>
    /// Subclass that removes the retry backoff delay so tests run instantly.
    /// </summary>
    private sealed class TestableDeploymentEngine : DeploymentEngine
    {
        public TestableDeploymentEngine(
            IConfigStore configStore, IDockerService dockerService,
            IOrganizationRepository organizationRepository, IEnvironmentRepository environmentRepository,
            ILogger<DeploymentEngine> logger)
            : base(configStore, dockerService, organizationRepository, environmentRepository, logger)
        {
        }

        protected override Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

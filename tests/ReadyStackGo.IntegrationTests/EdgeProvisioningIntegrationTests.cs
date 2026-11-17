using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Services.Deployment;
using ReadyStackGo.Infrastructure.Services.Edge;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Docker integration tests for the managed edge lifecycle: idempotent provisioning and the
/// redeploy/removal survival primitive. Uses the real DockerService against the local socket.
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class EdgeProvisioningIntegrationTests : IClassFixture<DockerTestFixture>
{
    private readonly DockerTestFixture _fixture;
    private static readonly EnvironmentId TestEnvId = EnvironmentId.Create();

    public EdgeProvisioningIntegrationTests(DockerTestFixture fixture) => _fixture = fixture;

    private static DockerService CreateDockerService()
    {
        var environmentRepository = Substitute.For<IEnvironmentRepository>();
        var socketPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        var env = DomainEnvironment.CreateDockerSocket(
            TestEnvId, DeploymentOrganizationId.Create(), "Test", "edge integration test", socketPath);
        env.SetAsDefault();
        environmentRepository.Get(TestEnvId).Returns(env);

        return new DockerService(
            environmentRepository,
            new ConfigurationBuilder().Build(),
            Substitute.For<ILogger<DockerService>>(),
            Substitute.For<ISshTunnelManager>(),
            Substitute.For<ICredentialEncryptionService>());
    }

    private static EdgeConfig MakeEdgeConfig(string network, int publicPort) => EdgeConfig.Create(
        publicHostname: "edge.test.local",
        publicPort: publicPort,
        upstreamService: "upstream",
        upstreamPort: 80,
        network: network,
        image: EdgeConstants.DefaultCaddyImage);

    [SkippableFact]
    public async Task EnsureEdgeAsync_IsIdempotent_ReusesExistingContainer()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        var docker = CreateDockerService();
        var envId = TestEnvId.ToString();
        var deploymentName = $"edge-idem-{Guid.NewGuid():N}".Substring(0, 18);
        var network = $"{deploymentName}-net";
        var config = MakeEdgeConfig(network, publicPort: 19180);
        var provisioner = new EdgeProvisioner(docker, Substitute.For<ILogger<EdgeProvisioner>>());
        var containerName = EdgeConstants.EdgeContainerName(deploymentName);

        try
        {
            var url1 = await provisioner.EnsureEdgeAsync(envId, deploymentName, "test.product", config);
            var first = await docker.GetContainerByNameAsync(envId, containerName);
            first.Should().NotBeNull("first EnsureEdge creates the container");
            first!.Labels.Should().ContainKey(EdgeConstants.ScopeLabel)
                .WhoseValue.Should().Be(EdgeConstants.ScopeEdge);

            // The edge must actually be running (catches a broken entrypoint/command).
            (await WaitForRunningAsync(docker, envId, containerName))
                .Should().BeTrue("the Caddy edge container must stay running, not crash on start");

            // Second call must reuse, not create a duplicate.
            var url2 = await provisioner.EnsureEdgeAsync(envId, deploymentName, "test.product", config);
            var second = await docker.GetContainerByNameAsync(envId, containerName);

            url2.Should().Be(url1);
            second!.Id.Should().Be(first.Id, "the existing edge container is reused, not recreated");
        }
        finally
        {
            await CleanupAsync(docker, envId, containerName, network);
        }
    }

    [SkippableFact]
    public async Task RemoveStackAsync_LeavesEdgeRunning_WhileRemovingStackContainers()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        var docker = CreateDockerService();
        var envId = TestEnvId.ToString();
        var deploymentName = $"edge-surv-{Guid.NewGuid():N}".Substring(0, 18);
        var network = $"{deploymentName}-net";
        var stackVersion = $"stack-{Guid.NewGuid():N}".Substring(0, 16);
        var config = MakeEdgeConfig(network, publicPort: 19181);
        var provisioner = new EdgeProvisioner(docker, Substitute.For<ILogger<EdgeProvisioner>>());
        var edgeName = EdgeConstants.EdgeContainerName(deploymentName);
        var stackContainerName = $"{deploymentName}-app";

        var configStore = Substitute.For<IConfigStore>();
        configStore.GetReleaseConfigAsync().Returns(new ReleaseConfig());
        var engine = new DeploymentEngine(
            configStore, docker,
            Substitute.For<ReadyStackGo.Domain.IdentityAccess.Organizations.IOrganizationRepository>(),
            Substitute.For<IEnvironmentRepository>(),
            Substitute.For<ILogger<DeploymentEngine>>());

        try
        {
            // 1. Provision the edge (survivor-scoped).
            await provisioner.EnsureEdgeAsync(envId, deploymentName, "test.product", config);

            // 2. Create an ordinary stack container carrying rsgo.stack=<version>.
            await docker.CreateAndStartContainerAsync(envId, new CreateContainerRequest
            {
                Name = stackContainerName,
                Image = "nginx:alpine",
                Networks = new List<string> { network },
                NetworkAliases = new List<string> { "app" },
                Labels = new Dictionary<string, string>
                {
                    ["rsgo.stack"] = stackVersion,
                    ["rsgo.context"] = "app"
                }
            });

            // 3. Tear down the stack — the edge must survive.
            var result = await engine.RemoveStackAsync(envId, stackVersion);
            result.Success.Should().BeTrue();

            var edgeAfter = await docker.GetContainerByNameAsync(envId, edgeName);
            var stackAfter = await docker.GetContainerByNameAsync(envId, stackContainerName);

            edgeAfter.Should().NotBeNull("the edge survives RemoveStackAsync (survival primitive)");
            stackAfter.Should().BeNull("the ordinary stack container is removed");
        }
        finally
        {
            await CleanupAsync(docker, envId, edgeName, network, stackContainerName);
        }
    }

    private static async Task<bool> WaitForRunningAsync(DockerService docker, string envId, string containerName)
    {
        for (var i = 0; i < 20; i++)
        {
            var c = await docker.GetContainerByNameAsync(envId, containerName);
            if (c != null && c.State.Equals("running", StringComparison.OrdinalIgnoreCase))
                return true;
            await Task.Delay(500);
        }
        return false;
    }

    private static async Task CleanupAsync(DockerService docker, string envId, string edgeName, string network, string? extra = null)
    {
        foreach (var name in new[] { edgeName, extra })
        {
            if (name is null) continue;
            try
            {
                var c = await docker.GetContainerByNameAsync(envId, name);
                if (c != null) await docker.RemoveContainerAsync(envId, c.Id, force: true);
            }
            catch { /* best effort */ }
        }
        // Networks are best-effort; leaving them is harmless but we try to keep the host clean.
    }
}

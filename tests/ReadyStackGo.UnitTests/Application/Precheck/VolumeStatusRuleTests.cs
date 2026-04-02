using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Application.Precheck;

public class VolumeStatusRuleTests
{
    private readonly VolumeStatusRule _sut = new();

    [Fact]
    public async Task Execute_NoVolumes_ReturnsOK()
    {
        var context = CreateContext(
            services: [new ServiceTemplate { Name = "web", Image = "nginx" }]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.OK);
        result[0].Title.Should().Contain("No named volumes");
    }

    [Fact]
    public async Task Execute_NewVolume_ReturnsOK()
    {
        var context = CreateContext(
            services: [new ServiceTemplate
            {
                Name = "db",
                Image = "postgres",
                Volumes = [new VolumeMapping { Source = "db-data", Target = "/var/lib/postgresql/data", Type = "volume" }]
            }]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.OK);
        result[0].Title.Should().Contain("will be created");
    }

    [Fact]
    public async Task Execute_ExistingVolume_NoDeployment_ReturnsWarning()
    {
        var context = CreateContext(
            services: [new ServiceTemplate
            {
                Name = "db",
                Image = "postgres",
                Volumes = [new VolumeMapping { Source = "db-data", Target = "/var/lib/postgresql/data", Type = "volume" }]
            }],
            existingVolumes: [new DockerVolumeRaw { Name = "db-data", Driver = "local" }]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.Warning);
        result[0].Title.Should().Contain("already exists");
    }

    [Fact]
    public async Task Execute_ExistingVolume_WithDeployment_ReturnsOK()
    {
        var deployment = TestHelpers.CreateDeployment(DeploymentStatus.Running);
        var context = CreateContext(
            services: [new ServiceTemplate
            {
                Name = "db",
                Image = "postgres",
                Volumes = [new VolumeMapping { Source = "db-data", Target = "/var/lib/postgresql/data", Type = "volume" }]
            }],
            existingVolumes: [new DockerVolumeRaw { Name = "db-data", Driver = "local" }],
            existingDeployment: deployment);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.OK);
        result[0].Title.Should().Contain("upgrade");
    }

    [Fact]
    public async Task Execute_PrefixedVolume_DetectsExisting()
    {
        var context = CreateContext(
            stackName: "my-stack",
            services: [new ServiceTemplate
            {
                Name = "db",
                Image = "postgres",
                Volumes = [new VolumeMapping { Source = "db-data", Target = "/var/lib/postgresql/data", Type = "volume" }]
            }],
            existingVolumes: [new DockerVolumeRaw { Name = "my-stack_db-data", Driver = "local" }]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Severity.Should().Be(PrecheckSeverity.Warning);
    }

    [Fact]
    public async Task Execute_BindMount_Ignored()
    {
        var context = CreateContext(
            services: [new ServiceTemplate
            {
                Name = "web",
                Image = "nginx",
                Volumes = [new VolumeMapping { Source = "/host/path", Target = "/container/path", Type = "bind" }]
            }]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle().Which.Title.Should().Contain("No named volumes");
    }

    #region Helpers

    private static PrecheckContext CreateContext(
        IReadOnlyList<ServiceTemplate>? services = null,
        IReadOnlyList<DockerVolumeRaw>? existingVolumes = null,
        Deployment? existingDeployment = null,
        string stackName = "test-stack")
    {
        return new PrecheckContext
        {
            EnvironmentId = Guid.NewGuid().ToString(),
            StackName = stackName,
            StackDefinition = TestHelpers.CreateStackDefinition(services: services ?? []),
            Variables = new Dictionary<string, string>(),
            RunningContainers = [],
            ExistingVolumes = existingVolumes ?? [],
            ExistingDeployment = existingDeployment
        };
    }

    #endregion
}

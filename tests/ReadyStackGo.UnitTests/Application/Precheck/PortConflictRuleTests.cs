using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Application.Precheck;

public class PortConflictRuleTests
{
    private readonly PortConflictRule _sut = new();

    #region No Conflicts

    [Fact]
    public async Task Execute_NoPorts_ReturnsOK()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx")],
            containers: []);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    [Fact]
    public async Task Execute_NoConflicts_ReturnsOK()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx", hostPort: "8080")],
            containers: [CreateContainer("other-svc", publicPort: 9090)]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    #endregion

    #region Port Conflicts

    [Fact]
    public async Task Execute_PortConflict_ReturnsError()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx", hostPort: "8080")],
            containers: [CreateContainer("other-svc", publicPort: 8080)]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.Error);
        result[0].Title.Should().Contain("8080");
    }

    [Fact]
    public async Task Execute_MultiplePortConflicts_ReturnsMultipleErrors()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx", hostPort: "8080"),
                       CreateService("api", "node", hostPort: "3000")],
            containers: [CreateContainer("existing1", publicPort: 8080),
                         CreateContainer("existing2", publicPort: 3000)]);

        var errors = (await _sut.ExecuteAsync(context, CancellationToken.None))
            .Where(c => c.Severity == PrecheckSeverity.Error);

        errors.Should().HaveCount(2);
    }

    #endregion

    #region Own Stack (Upgrade / Redeploy)

    [Fact]
    public async Task Execute_OwnStackContainerByLabel_SkipsConflict()
    {
        // Container belonging to the same stack (upgrade scenario) should not be a conflict
        // Identified via the authoritative rsgo.stack label
        var context = CreateContext(
            stackName: "my-stack",
            services: [CreateService("web", "nginx", hostPort: "8080")],
            containers: [CreateContainer("/my-stack_web", publicPort: 8080, rsgoStackLabel: "my-stack")]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    [Fact]
    public async Task Execute_OwnStackContainerByComposeProjectLabel_SkipsConflict()
    {
        // Compose-deployed container uses com.docker.compose.project label
        var context = CreateContext(
            stackName: "my-stack",
            services: [CreateService("web", "nginx", hostPort: "8080")],
            containers: [CreateContainer("/my-stack_web", publicPort: 8080, composeProject: "my-stack")]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    [Fact]
    public async Task Execute_RedeployScenario_WithUnderscoreSeparator_SkipsOwnContainer()
    {
        // Regression: containers use {stackName}_{service} naming (underscore, not dash)
        // and Docker prefixes names with '/'. The old implementation missed both.
        var context = CreateContext(
            stackName: "e2e-test-platform",
            services: [CreateService("frontend", "nginx", hostPort: "8091")],
            containers: [CreateContainer("/e2e-test-platform_frontend", publicPort: 8091, rsgoStackLabel: "e2e-test-platform")]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    [Fact]
    public async Task Execute_DifferentStackWithSamePort_ReportsConflict()
    {
        // Container from a different stack using the same port IS a conflict
        var context = CreateContext(
            stackName: "my-stack",
            services: [CreateService("web", "nginx", hostPort: "8080")],
            containers: [CreateContainer("/other-stack_web", publicPort: 8080, rsgoStackLabel: "other-stack")]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().Contain(c => c.Severity == PrecheckSeverity.Error);
    }

    [Fact]
    public void BelongsToStack_RsgoStackLabel_Matches()
    {
        var container = CreateContainer("/my-stack_web", rsgoStackLabel: "my-stack");
        PortConflictRule.BelongsToStack(container, "my-stack").Should().BeTrue();
    }

    [Fact]
    public void BelongsToStack_ComposeProjectLabel_Matches()
    {
        var container = CreateContainer("/my-stack_web", composeProject: "my-stack");
        PortConflictRule.BelongsToStack(container, "my-stack").Should().BeTrue();
    }

    [Fact]
    public void BelongsToStack_DifferentStack_DoesNotMatch()
    {
        var container = CreateContainer("/my-stack_web", rsgoStackLabel: "other-stack");
        PortConflictRule.BelongsToStack(container, "my-stack").Should().BeFalse();
    }

    [Fact]
    public void BelongsToStack_NoLabels_DoesNotMatch()
    {
        var container = CreateContainer("/my-stack_web");
        PortConflictRule.BelongsToStack(container, "my-stack").Should().BeFalse();
    }

    #endregion

    #region Random Port

    [Fact]
    public async Task Execute_RandomPort_SkipsCheck()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx", hostPort: "0")],
            containers: [CreateContainer("other", publicPort: 8080)]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    #endregion

    #region Port Range

    [Fact]
    public async Task Execute_PortRange_ChecksAllPorts()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx", hostPort: "8080-8082")],
            containers: [CreateContainer("other", publicPort: 8081)]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().Contain(c => c.Severity == PrecheckSeverity.Error);
        result.First(c => c.Severity == PrecheckSeverity.Error).Title.Should().Contain("8081");
    }

    #endregion

    #region Non-Running Containers

    [Fact]
    public async Task Execute_StoppedContainer_SkipsConflict()
    {
        var context = CreateContext(
            services: [CreateService("web", "nginx", hostPort: "8080")],
            containers: [CreateContainer("other", publicPort: 8080, state: "exited")]);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    #endregion

    #region ExpandPortRange

    [Fact]
    public void ExpandPortRange_SinglePort_ReturnsSingle()
    {
        PortConflictRule.ExpandPortRange("8080").Should().Equal(8080);
    }

    [Fact]
    public void ExpandPortRange_Range_ReturnsAll()
    {
        PortConflictRule.ExpandPortRange("8080-8083").Should().Equal(8080, 8081, 8082, 8083);
    }

    [Fact]
    public void ExpandPortRange_InvalidPort_ReturnsEmpty()
    {
        PortConflictRule.ExpandPortRange("abc").Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static PrecheckContext CreateContext(
        IReadOnlyList<ServiceTemplate>? services = null,
        IReadOnlyList<ContainerDto>? containers = null,
        string stackName = "test-stack")
    {
        var stackDef = TestHelpers.CreateStackDefinition(services: services ?? []);
        return new PrecheckContext
        {
            EnvironmentId = Guid.NewGuid().ToString(),
            StackName = stackName,
            StackDefinition = stackDef,
            Variables = new Dictionary<string, string>(),
            RunningContainers = containers ?? [],
            ExistingVolumes = [],
        };
    }

    private static ServiceTemplate CreateService(string name, string image, string? hostPort = null)
    {
        var ports = hostPort != null
            ? new[] { new PortMapping { HostPort = hostPort, ContainerPort = "80" } }
            : Array.Empty<PortMapping>();

        return new ServiceTemplate
        {
            Name = name,
            Image = image,
            Ports = ports
        };
    }

    private static ContainerDto CreateContainer(
        string name,
        int publicPort = 0,
        string state = "running",
        string? rsgoStackLabel = null,
        string? composeProject = null)
    {
        var ports = publicPort > 0
            ? new List<PortDto> { new() { PublicPort = publicPort, PrivatePort = 80 } }
            : new List<PortDto>();

        var labels = new Dictionary<string, string>();
        if (rsgoStackLabel != null) labels["rsgo.stack"] = rsgoStackLabel;
        if (composeProject != null) labels["com.docker.compose.project"] = composeProject;

        return new ContainerDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Image = "some-image",
            State = state,
            Status = "running",
            Ports = ports,
            Labels = labels
        };
    }

    #endregion
}

using FluentAssertions;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.StackManagement.Stacks;
using ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Infrastructure.Precheck;

public class PortConflictRuleVariableTests
{
    private readonly PortConflictRule _rule = new();

    private static PrecheckContext CreateContext(
        IReadOnlyList<ServiceTemplate>? services = null,
        IReadOnlyList<ContainerDto>? containers = null,
        string stackName = "test-stack",
        Dictionary<string, string>? variables = null)
    {
        var stack = new StackDefinition(
            "source", "test", null,
            services: services ?? [],
            variables: []);

        return new PrecheckContext
        {
            EnvironmentId = "env-1",
            StackName = stackName,
            StackDefinition = stack,
            Variables = variables ?? new Dictionary<string, string>(),
            RunningContainers = containers ?? [],
            ExistingVolumes = [],
        };
    }

    private static ServiceTemplate CreateService(string hostPort, string containerPort = "80")
    {
        return new ServiceTemplate
        {
            Name = "web",
            Image = "nginx:latest",
            Ports = [new PortMapping { HostPort = hostPort, ContainerPort = containerPort }]
        };
    }

    private static ContainerDto CreateContainer(string name, int publicPort)
    {
        return new ContainerDto
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Image = "some:image",
            State = "running",
            Status = "Up",
            Ports = [new PortDto { PublicPort = publicPort, PrivatePort = 80 }]
        };
    }

    [Fact]
    public async Task Execute_VariablePort_ResolvesAndDetectsConflict()
    {
        var services = new[] { CreateService("${WEB_PORT}") };
        var containers = new[] { CreateContainer("other-app", 8080) };
        var variables = new Dictionary<string, string> { { "WEB_PORT", "8080" } };

        var context = CreateContext(services, containers, variables: variables);
        var result = await _rule.ExecuteAsync(context, CancellationToken.None);

        result.Should().Contain(c => c.Severity == PrecheckSeverity.Error && c.Title.Contains("8080"));
    }

    [Fact]
    public async Task Execute_VariablePort_ResolvesAndNoConflict()
    {
        var services = new[] { CreateService("${WEB_PORT}") };
        var containers = new[] { CreateContainer("other-app", 9090) };
        var variables = new Dictionary<string, string> { { "WEB_PORT", "8080" } };

        var context = CreateContext(services, containers, variables: variables);
        var result = await _rule.ExecuteAsync(context, CancellationToken.None);

        result.Should().Contain(c => c.Severity == PrecheckSeverity.OK);
        result.Should().NotContain(c => c.Severity == PrecheckSeverity.Error);
    }

    [Fact]
    public async Task Execute_UnresolvedVariable_SkipsGracefully()
    {
        var services = new[] { CreateService("${UNKNOWN_PORT}") };
        var containers = new[] { CreateContainer("other-app", 8080) };

        var context = CreateContext(services, containers);
        var result = await _rule.ExecuteAsync(context, CancellationToken.None);

        // Unresolved variable → int.TryParse fails → no conflict reported, just OK
        result.Should().Contain(c => c.Severity == PrecheckSeverity.OK);
    }

    [Fact]
    public async Task Execute_VariableWithDefault_UsesDefault()
    {
        var services = new[] { CreateService("${PORT:-8080}") };
        var containers = new[] { CreateContainer("other-app", 8080) };

        var context = CreateContext(services, containers);
        var result = await _rule.ExecuteAsync(context, CancellationToken.None);

        result.Should().Contain(c => c.Severity == PrecheckSeverity.Error && c.Title.Contains("8080"));
    }

    [Fact]
    public async Task Execute_LiteralPort_StillWorks()
    {
        var services = new[] { CreateService("3000") };
        var containers = new[] { CreateContainer("other-app", 3000) };

        var context = CreateContext(services, containers);
        var result = await _rule.ExecuteAsync(context, CancellationToken.None);

        result.Should().Contain(c => c.Severity == PrecheckSeverity.Error && c.Title.Contains("3000"));
    }
}

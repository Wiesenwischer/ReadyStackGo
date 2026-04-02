using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Application.UseCases.Deployments.Precheck.Rules;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Application.Precheck;

public class ExistingDeploymentRuleTests
{
    private readonly ExistingDeploymentRule _sut = new();

    [Fact]
    public async Task Execute_NoExistingDeployment_ReturnsOK()
    {
        var context = CreateContext(existingDeployment: null);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
        result[0].Title.Should().Contain("No existing deployment");
    }

    [Fact]
    public async Task Execute_RunningDeployment_ReturnsWarning()
    {
        var deployment = CreateDeployment(DeploymentStatus.Running);
        var context = CreateContext(existingDeployment: deployment);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.Warning);
        result[0].Title.Should().Contain("upgrade");
    }

    [Fact]
    public async Task Execute_InstallingDeployment_ReturnsError()
    {
        var deployment = CreateDeployment(DeploymentStatus.Installing);
        var context = CreateContext(existingDeployment: deployment);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.Error);
        result[0].Title.Should().Contain("in progress");
    }

    [Fact]
    public async Task Execute_UpgradingDeployment_ReturnsError()
    {
        var deployment = CreateDeployment(DeploymentStatus.Upgrading);
        var context = CreateContext(existingDeployment: deployment);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.Error);
    }

    [Fact]
    public async Task Execute_FailedDeployment_ReturnsWarning()
    {
        var deployment = CreateDeployment(DeploymentStatus.Failed);
        var context = CreateContext(existingDeployment: deployment);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.Warning);
        result[0].Title.Should().Contain("failed");
    }

    [Fact]
    public async Task Execute_RemovedDeployment_ReturnsOK()
    {
        var deployment = CreateDeployment(DeploymentStatus.Removed);
        var context = CreateContext(existingDeployment: deployment);

        var result = await _sut.ExecuteAsync(context, CancellationToken.None);

        result.Should().ContainSingle()
            .Which.Severity.Should().Be(PrecheckSeverity.OK);
    }

    #region Helpers

    private static PrecheckContext CreateContext(Deployment? existingDeployment)
    {
        var stackDef = TestHelpers.CreateStackDefinition();
        return new PrecheckContext
        {
            EnvironmentId = Guid.NewGuid().ToString(),
            StackName = "test-stack",
            StackDefinition = stackDef,
            Variables = new Dictionary<string, string>(),
            RunningContainers = [],
            ExistingVolumes = [],
            ExistingDeployment = existingDeployment
        };
    }

    private static Deployment CreateDeployment(DeploymentStatus status)
    {
        return TestHelpers.CreateDeployment(status);
    }

    #endregion
}

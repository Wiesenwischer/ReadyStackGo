using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Hooks.RedeployStack;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class RedeployStackHandlerTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<RedeployStackHandler>> _loggerMock;
    private readonly RedeployStackHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestStackId = "source1:product1:my-stack";
    private const string TestStackName = "my-stack";

    public RedeployStackHandlerTests()
    {
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<RedeployStackHandler>>();
        _handler = new RedeployStackHandler(
            _deploymentRepoMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    private static Deployment CreateRunningDeployment(
        string stackName = TestStackName,
        string? stackId = null,
        string? environmentId = null,
        Dictionary<string, string>? variables = null)
    {
        var envId = new EnvironmentId(Guid.Parse(environmentId ?? TestEnvironmentId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.Create(),
            envId,
            stackId ?? TestStackId,
            stackName,
            $"rsgo-{stackName}",
            UserId.Create());

        if (variables != null)
        {
            deployment.SetVariables(variables);
        }

        deployment.MarkAsRunning();
        return deployment;
    }

    #region Successful Redeploy

    [Fact]
    public async Task Handle_RunningDeployment_ReturnsSuccess()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.StackName.Should().Be(TestStackName);
        result.Message.Should().Contain("Successfully");
    }

    [Fact]
    public async Task Handle_RunningDeployment_DelegatesToDeployStackCommand()
    {
        var variables = new Dictionary<string, string>
        {
            ["DB_HOST"] = "localhost",
            ["DB_PORT"] = "5432"
        };
        var deployment = CreateRunningDeployment(variables: variables);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.EnvironmentId == TestEnvironmentId &&
                cmd.StackId == TestStackId &&
                cmd.StackName == TestStackName &&
                cmd.Variables["DB_HOST"] == "localhost" &&
                cmd.Variables["DB_PORT"] == "5432" &&
                cmd.SessionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RunningDeployment_ReturnsDeploymentIdFromDeployResult()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);

        var expectedDeploymentId = Guid.NewGuid().ToString();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = expectedDeploymentId
            });

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.DeploymentId.Should().Be(expectedDeploymentId);
    }

    [Fact]
    public async Task Handle_RunningDeployment_PreservesExistingVariables()
    {
        var variables = new Dictionary<string, string>
        {
            ["SECRET"] = "super-secret",
            ["URL"] = "https://example.com"
        };
        var deployment = CreateRunningDeployment(variables: variables);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 2 &&
                cmd.Variables["SECRET"] == "super-secret" &&
                cmd.Variables["URL"] == "https://example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DeploymentWithVersion_ReturnsVersionInResponse()
    {
        var deployment = CreateRunningDeployment();
        deployment.SetStackVersion("2.1.0");
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.StackVersion.Should().Be("2.1.0");
    }

    #endregion

    #region Deployment Not Found

    [Fact]
    public async Task Handle_UnknownStackName_ReturnsNotFound()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((Deployment?)null);

        var result = await _handler.Handle(
            new RedeployStackCommand("nonexistent-stack", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No deployment found");
        result.Message.Should().Contain("nonexistent-stack");
    }

    #endregion

    #region Invalid Status

    [Fact]
    public async Task Handle_FailedDeployment_ReturnsError()
    {
        var deployment = CreateFailedDeployment();
        SetupDeploymentLookup(deployment);

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only running deployments");
        result.Message.Should().Contain("Failed");
    }

    [Fact]
    public async Task Handle_InstallingDeployment_ReturnsError()
    {
        var deployment = CreateInstallingDeployment();
        SetupDeploymentLookup(deployment);

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only running deployments");
        result.Message.Should().Contain("Installing");
    }

    [Fact]
    public async Task Handle_RemovedDeployment_ReturnsError()
    {
        var deployment = CreateRemovedDeployment();
        SetupDeploymentLookup(deployment);

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only running deployments");
        result.Message.Should().Contain("Removed");
    }

    #endregion

    #region Invalid Input

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, "not-a-guid"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_EmptyEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID");
    }

    #endregion

    #region Deploy Failure Propagation

    [Fact]
    public async Task Handle_DeployCommandFails_PropagatesError()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployStackResponse.Failed("Image pull failed"));

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Image pull failed");
    }

    [Fact]
    public async Task Handle_DeployCommandFails_DoesNotReturnDeploymentId()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployStackResponse.Failed("Connection refused"));

        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        result.DeploymentId.Should().BeNull();
    }

    #endregion

    #region No SessionId for Webhook

    [Fact]
    public async Task Handle_AlwaysSendsNullSessionId()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.SessionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers

    private void SetupDeploymentLookup(Deployment deployment)
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                It.Is<string>(s => s == deployment.StackName)))
            .Returns(deployment);
    }

    private void SetupSuccessfulDeploy()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString()
            });
    }

    private static Deployment CreateFailedDeployment()
    {
        var envId = new EnvironmentId(Guid.Parse(TestEnvironmentId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.Create(), envId, TestStackId, TestStackName, $"rsgo-{TestStackName}", UserId.Create());
        deployment.MarkAsFailed("Something went wrong");
        return deployment;
    }

    private static Deployment CreateInstallingDeployment()
    {
        var envId = new EnvironmentId(Guid.Parse(TestEnvironmentId));
        return Deployment.StartInstallation(
            DeploymentId.Create(), envId, TestStackId, TestStackName, $"rsgo-{TestStackName}", UserId.Create());
    }

    private static Deployment CreateRemovedDeployment()
    {
        var envId = new EnvironmentId(Guid.Parse(TestEnvironmentId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.Create(), envId, TestStackId, TestStackName, $"rsgo-{TestStackName}", UserId.Create());
        deployment.MarkAsRunning();
        deployment.MarkAsRemoved();
        return deployment;
    }

    #endregion
}

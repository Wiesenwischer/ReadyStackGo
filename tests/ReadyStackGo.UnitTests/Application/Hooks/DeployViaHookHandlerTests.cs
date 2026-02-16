using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Hooks.DeployStack;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class DeployViaHookHandlerTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<DeployViaHookHandler>> _loggerMock;
    private readonly DeployViaHookHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestStackId = "source1:product1:my-stack";
    private const string TestStackName = "my-stack";

    public DeployViaHookHandlerTests()
    {
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<DeployViaHookHandler>>();
        _handler = new DeployViaHookHandler(
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

    #region Fresh Deploy (No Existing Deployment)

    [Fact]
    public async Task Handle_NoExistingDeployment_ReturnsSuccessWithDeployedAction()
    {
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("deployed");
        result.StackName.Should().Be(TestStackName);
        result.Message.Should().Contain("deployed");
    }

    [Fact]
    public async Task Handle_NoExistingDeployment_DelegatesToDeployStackCommand()
    {
        var variables = new Dictionary<string, string>
        {
            ["DB_HOST"] = "localhost",
            ["DB_PORT"] = "5432"
        };
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, variables),
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
    public async Task Handle_NoExistingDeployment_ReturnsDeploymentIdFromResult()
    {
        SetupNoExistingDeployment();

        var expectedDeploymentId = Guid.NewGuid().ToString();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = expectedDeploymentId,
                StackVersion = "1.0.0"
            });

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.DeploymentId.Should().Be(expectedDeploymentId);
        result.StackVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_FreshDeploy_AlwaysSendsNullSessionId()
    {
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.SessionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Idempotent Redeploy (Existing Running Deployment)

    [Fact]
    public async Task Handle_ExistingRunningDeployment_ReturnsSuccessWithRedeployedAction()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("redeployed");
        result.StackName.Should().Be(TestStackName);
        result.Message.Should().Contain("redeployed");
    }

    [Fact]
    public async Task Handle_ExistingRunningDeployment_UsesExistingStackId()
    {
        var existingStackId = "existing-source:existing-product:my-stack";
        var deployment = CreateRunningDeployment(stackId: existingStackId);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new DeployViaHookCommand("different-stack-id", TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackId == existingStackId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingRunningDeployment_MergesStoredAndWebhookVariables()
    {
        var existingVariables = new Dictionary<string, string>
        {
            ["DB_HOST"] = "stored-host",
            ["DB_PORT"] = "5432",
            ["LOG_LEVEL"] = "Warning"
        };
        var deployment = CreateRunningDeployment(variables: existingVariables);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var requestVariables = new Dictionary<string, string>
        {
            ["NEW_VAR"] = "new-value"
        };

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, requestVariables),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 4 &&
                cmd.Variables["DB_HOST"] == "stored-host" &&
                cmd.Variables["DB_PORT"] == "5432" &&
                cmd.Variables["LOG_LEVEL"] == "Warning" &&
                cmd.Variables["NEW_VAR"] == "new-value"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingRunningDeployment_WebhookVariablesOverrideStored()
    {
        var existingVariables = new Dictionary<string, string>
        {
            ["DB_HOST"] = "stored-host",
            ["LOG_LEVEL"] = "Warning"
        };
        var deployment = CreateRunningDeployment(variables: existingVariables);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var requestVariables = new Dictionary<string, string>
        {
            ["LOG_LEVEL"] = "Debug"
        };

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, requestVariables),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables["DB_HOST"] == "stored-host" &&
                cmd.Variables["LOG_LEVEL"] == "Debug"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingRunningDeployment_EmptyWebhookVarsUsesAllStoredVars()
    {
        var existingVariables = new Dictionary<string, string>
        {
            ["REDIS_DB"] = "cachedata:6379",
            ["AMS_DB"] = "Server=sql;Database=AMS;",
            ["MIN_LOG_LEVEL"] = "Fatal"
        };
        var deployment = CreateRunningDeployment(variables: existingVariables);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 3 &&
                cmd.Variables["REDIS_DB"] == "cachedata:6379" &&
                cmd.Variables["AMS_DB"] == "Server=sql;Database=AMS;" &&
                cmd.Variables["MIN_LOG_LEVEL"] == "Fatal"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingRunningDeployment_NoStoredVarsUsesWebhookVars()
    {
        var deployment = CreateRunningDeployment(variables: null);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var requestVariables = new Dictionary<string, string>
        {
            ["NEW_VAR"] = "value"
        };

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, requestVariables),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 1 &&
                cmd.Variables["NEW_VAR"] == "value"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingRunningDeployment_ReturnsVersionFromDeployResult()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString(),
                StackVersion = "3.0.0"
            });

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.StackVersion.Should().Be("3.0.0");
    }

    #endregion

    #region Existing Non-Running Deployment

    [Fact]
    public async Task Handle_FailedDeployment_ReturnsError()
    {
        var deployment = CreateFailedDeployment();
        SetupDeploymentLookup(deployment);

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
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
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
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
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only running deployments");
        result.Message.Should().Contain("Removed");
    }

    [Fact]
    public async Task Handle_NonRunningDeployment_DoesNotDelegateToDeployCommand()
    {
        var deployment = CreateFailedDeployment();
        SetupDeploymentLookup(deployment);

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.IsAny<DeployStackCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Invalid Input

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, "not-a-guid", new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_EmptyEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, "", new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_EmptyStackId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand("", TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("StackId is required");
    }

    [Fact]
    public async Task Handle_WhitespaceStackId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand("   ", TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("StackId is required");
    }

    [Fact]
    public async Task Handle_EmptyStackName_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, "", TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("StackName is required");
    }

    [Fact]
    public async Task Handle_WhitespaceStackName_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, "   ", TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("StackName is required");
    }

    #endregion

    #region Deploy Failure Propagation

    [Fact]
    public async Task Handle_DeployCommandFails_PropagatesError()
    {
        SetupNoExistingDeployment();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployStackResponse.Failed("Image pull failed"));

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Image pull failed");
    }

    [Fact]
    public async Task Handle_DeployCommandFails_DoesNotReturnDeploymentId()
    {
        SetupNoExistingDeployment();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployStackResponse.Failed("Connection refused"));

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.DeploymentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RedeployCommandFails_PropagatesError()
    {
        var deployment = CreateRunningDeployment();
        SetupDeploymentLookup(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployStackResponse.Failed("Container restart failed"));

        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Container restart failed");
    }

    #endregion

    #region Helpers

    private void SetupNoExistingDeployment()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((Deployment?)null);
    }

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

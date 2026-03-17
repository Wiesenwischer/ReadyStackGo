using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;
using ReadyStackGo.Application.UseCases.Hooks.RedeployStack;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class RedeployStackHandlerTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IProductDeploymentRepository> _productDeploymentRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<RedeployStackHandler>> _loggerMock;
    private readonly RedeployStackHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestStackId = "source1:product1:my-stack";
    private const string TestStackName = "my-stack";

    public RedeployStackHandlerTests()
    {
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _productDeploymentRepoMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<RedeployStackHandler>>();
        _handler = new RedeployStackHandler(
            _deploymentRepoMock.Object,
            _productDeploymentRepoMock.Object,
            Mock.Of<IEnvironmentRepository>(),
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
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_EmptyEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RedeployStackCommand(TestStackName, ""),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    [Fact]
    public async Task Handle_NoStackNameOrProductId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("stackName or productId");
    }

    [Fact]
    public async Task Handle_EmptyStackNameAndNoProductId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RedeployStackCommand("", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("stackName or productId");
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

    #region Variable Merging

    [Fact]
    public async Task Handle_NoWebhookVars_UsesAllStoredVars()
    {
        var storedVars = new Dictionary<string, string>
        {
            ["DB_HOST"] = "db.internal",
            ["LOG_LEVEL"] = "info"
        };
        var deployment = CreateRunningDeployment(variables: storedVars);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 2 &&
                cmd.Variables["DB_HOST"] == "db.internal" &&
                cmd.Variables["LOG_LEVEL"] == "info"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullWebhookVars_UsesAllStoredVars()
    {
        var storedVars = new Dictionary<string, string>
        {
            ["API_URL"] = "https://api.example.com"
        };
        var deployment = CreateRunningDeployment(variables: storedVars);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId, null),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 1 &&
                cmd.Variables["API_URL"] == "https://api.example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WebhookVarsOverrideStoredVars()
    {
        var storedVars = new Dictionary<string, string>
        {
            ["DB_HOST"] = "old-host",
            ["DB_PORT"] = "5432",
            ["LOG_LEVEL"] = "info"
        };
        var webhookVars = new Dictionary<string, string>
        {
            ["DB_HOST"] = "new-host",
            ["LOG_LEVEL"] = "debug"
        };
        var deployment = CreateRunningDeployment(variables: storedVars);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId, webhookVars),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 3 &&
                cmd.Variables["DB_HOST"] == "new-host" &&
                cmd.Variables["DB_PORT"] == "5432" &&
                cmd.Variables["LOG_LEVEL"] == "debug"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WebhookVarsAddNewVars()
    {
        var storedVars = new Dictionary<string, string>
        {
            ["DB_HOST"] = "localhost"
        };
        var webhookVars = new Dictionary<string, string>
        {
            ["FEATURE_FLAG"] = "enabled"
        };
        var deployment = CreateRunningDeployment(variables: storedVars);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId, webhookVars),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 2 &&
                cmd.Variables["DB_HOST"] == "localhost" &&
                cmd.Variables["FEATURE_FLAG"] == "enabled"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyWebhookVars_UsesAllStoredVars()
    {
        var storedVars = new Dictionary<string, string>
        {
            ["SECRET"] = "keep-me"
        };
        var webhookVars = new Dictionary<string, string>();
        var deployment = CreateRunningDeployment(variables: storedVars);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId, webhookVars),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 1 &&
                cmd.Variables["SECRET"] == "keep-me"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoStoredVars_UsesWebhookVarsOnly()
    {
        var webhookVars = new Dictionary<string, string>
        {
            ["NEW_VAR"] = "new-value"
        };
        var deployment = CreateRunningDeployment(); // no variables
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new RedeployStackCommand(TestStackName, TestEnvironmentId, webhookVars),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables.Count == 1 &&
                cmd.Variables["NEW_VAR"] == "new-value"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Product Redeploy

    [Fact]
    public async Task Handle_ProductId_RoutesToProductRedeploy()
    {
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);
        SetupSuccessfulProductRedeploy(productDeployment.Id.Value.ToString());

        var result = await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId, ProductId: "test.product"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProductDeploymentId.Should().Be(productDeployment.Id.Value.ToString());
        result.Message.Should().Contain("test-product");
    }

    [Fact]
    public async Task Handle_ProductId_DispatchesRedeployProductCommand()
    {
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);
        SetupSuccessfulProductRedeploy(productDeployment.Id.Value.ToString());

        await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId, ProductId: "test.product"),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RedeployProductCommand>(cmd =>
                cmd.EnvironmentId == TestEnvironmentId &&
                cmd.ProductDeploymentId == productDeployment.Id.Value.ToString() &&
                cmd.StackNames == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithStackDefinitionName_RedeploysOnlyNamedStack()
    {
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);
        SetupSuccessfulProductRedeploy(productDeployment.Id.Value.ToString());

        await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId,
                ProductId: "test.product", StackDefinitionName: "Analytics"),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RedeployProductCommand>(cmd =>
                cmd.StackNames != null &&
                cmd.StackNames.Count == 1 &&
                cmd.StackNames[0] == "Analytics"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithVariables_PassesVariablesToCommand()
    {
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);
        SetupSuccessfulProductRedeploy(productDeployment.Id.Value.ToString());

        var variables = new Dictionary<string, string> { ["BUILD_NUM"] = "42" };

        await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId, variables, ProductId: "test.product"),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RedeployProductCommand>(cmd =>
                cmd.VariableOverrides != null &&
                cmd.VariableOverrides["BUILD_NUM"] == "42"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdNotFound_ReturnsError()
    {
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((ProductDeployment?)null);

        var result = await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId, ProductId: "unknown.product"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No active product deployment");
        result.Message.Should().Contain("unknown.product");
    }

    [Fact]
    public async Task Handle_ProductRedeployFails_PropagatesError()
    {
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RedeployProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeployProductResponse.Failed("Lock acquisition failed"));

        var result = await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId, ProductId: "test.product"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Lock acquisition failed");
    }

    [Fact]
    public async Task Handle_ProductIdWithInvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RedeployStackCommand(null, "not-a-guid", ProductId: "test.product"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ProductIdNullStackDefinitionName_RedeploysAllStacks()
    {
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);
        SetupSuccessfulProductRedeploy(productDeployment.Id.Value.ToString());

        await _handler.Handle(
            new RedeployStackCommand(null, TestEnvironmentId,
                ProductId: "test.product", StackDefinitionName: null),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RedeployProductCommand>(cmd => cmd.StackNames == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithStackNameIgnored_UsesProductPath()
    {
        // When productId is set, stackName should be ignored and product path should be used
        var productDeployment = CreateRunningProductDeployment("test.product");
        SetupProductDeploymentLookup("test.product", productDeployment);
        SetupSuccessfulProductRedeploy(productDeployment.Id.Value.ToString());

        var result = await _handler.Handle(
            new RedeployStackCommand("some-stack-name", TestEnvironmentId, ProductId: "test.product"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<RedeployProductCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<DeployStackCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
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

    private static ProductDeployment CreateRunningProductDeployment(string productGroupId)
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("Analytics", "Analytics", "source1:analytics", 2, new Dictionary<string, string>()),
            new("Backend", "Backend", "source1:backend", 1, new Dictionary<string, string>())
        };

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            productGroupId, $"{productGroupId}:1.0.0",
            "test-product", "Test Product", "1.0.0",
            UserId.Create(), "test-deployment",
            stackConfigs, new Dictionary<string, string>());

        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId());
            deployment.CompleteStack(stack.StackName);
        }

        return deployment;
    }

    private void SetupProductDeploymentLookup(string productGroupId, ProductDeployment productDeployment)
    {
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                It.Is<string>(s => s == productGroupId)))
            .Returns(productDeployment);
    }

    private void SetupSuccessfulProductRedeploy(string productDeploymentId)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RedeployProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployProductResponse
            {
                Success = true,
                ProductDeploymentId = productDeploymentId,
                Message = "Redeploy completed"
            });
    }

    #endregion
}

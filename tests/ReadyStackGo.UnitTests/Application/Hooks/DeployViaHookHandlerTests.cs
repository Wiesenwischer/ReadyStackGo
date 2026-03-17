using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Hooks.DeployStack;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class DeployViaHookHandlerTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IProductDeploymentRepository> _productDeploymentRepoMock;
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<DeployViaHookHandler>> _loggerMock;
    private readonly DeployViaHookHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestStackId = "source1:product1:my-stack";
    private const string TestStackName = "my-stack";

    public DeployViaHookHandlerTests()
    {
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _productDeploymentRepoMock = new Mock<IProductDeploymentRepository>();
        _productSourceMock = new Mock<IProductSourceService>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<DeployViaHookHandler>>();
        _handler = new DeployViaHookHandler(
            _deploymentRepoMock.Object,
            _productDeploymentRepoMock.Object,
            _productSourceMock.Object,
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

    private static ProductDefinition CreateSingleStackProduct(
        string productId = "com.test.product",
        string? version = null,
        string sourceId = "source1",
        string stackName = "default-stack")
    {
        var stack = new StackDefinition(
            sourceId, stackName, new ProductId(productId),
            productVersion: version);

        return new ProductDefinition(
            sourceId, productId, "Test Product",
            stacks: new List<StackDefinition> { stack },
            productVersion: version,
            productId: productId);
    }

    private static ProductDefinition CreateMultiStackProduct(
        string productId = "com.test.multistack",
        string? version = null,
        string sourceId = "source1",
        params string[] stackNames)
    {
        var stacks = stackNames.Select(name =>
            new StackDefinition(
                sourceId, name, new ProductId(productId),
                productVersion: version)).ToList();

        return new ProductDefinition(
            sourceId, productId, "Multi-Stack Product",
            stacks: stacks,
            productVersion: version,
            productId: productId);
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
        result.Message.Should().Contain("Invalid EnvironmentId format");
    }

    [Fact]
    public async Task Handle_EmptyEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, "", new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("required");
    }

    [Fact]
    public async Task Handle_NeitherStackIdNorProductId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Either StackId or ProductId is required");
    }

    [Fact]
    public async Task Handle_EmptyStackIdAndNoProductId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand("", TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Either StackId or ProductId is required");
    }

    [Fact]
    public async Task Handle_WhitespaceStackIdAndNoProductId_ReturnsError()
    {
        var result = await _handler.Handle(
            new DeployViaHookCommand("   ", TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Either StackId or ProductId is required");
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

    #region ProductId Resolution — Happy Path

    [Fact]
    public async Task Handle_ProductIdOnly_ResolvesLatestVersionAndDeploys()
    {
        var product = CreateSingleStackProduct("com.test.product", version: "2.0.0");
        SetupProductLookup("com.test.product", product);
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.product"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("deployed");

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackId == product.DefaultStack.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithVersion_ResolvesSpecificVersion()
    {
        var productV1 = CreateSingleStackProduct("com.test.product", version: "1.0.0");
        var productV2 = CreateSingleStackProduct("com.test.product", version: "2.0.0");
        SetupProductLookup("com.test.product", productV2);
        SetupProductVersions("com.test.product", new[] { productV2, productV1 });
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.product", Version: "1.0.0"),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackId == productV1.DefaultStack.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithStackDefinitionName_ResolvesCorrectStack()
    {
        var product = CreateMultiStackProduct("com.test.multi", version: "1.0.0",
            sourceId: "source1", stackNames: new[] { "web", "api", "worker" });
        SetupProductLookup("com.test.multi", product);
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.multi", StackDefinitionName: "api"),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var expectedStackId = product.GetStack("api")!.Id.Value;
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackId == expectedStackId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BothStackIdAndProductId_UsesStackId()
    {
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.product"),
            CancellationToken.None);

        // StackId takes precedence — ProductId resolution should NOT be called
        _productSourceMock.Verify(
            s => s.GetProductAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackId == TestStackId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithExistingDeployment_UsesExistingStackId()
    {
        var existingStackId = "existing-source:existing-product:1.0.0:my-stack";
        var deployment = CreateRunningDeployment(stackId: existingStackId);
        SetupDeploymentLookup(deployment);
        SetupSuccessfulDeploy();

        var product = CreateSingleStackProduct("com.test.product", version: "2.0.0");
        SetupProductLookup("com.test.product", product);

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.product"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("redeployed");

        // Existing deployment's StackId should be used, not the resolved one
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackId == existingStackId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ProductId Resolution — Error Cases

    [Fact]
    public async Task Handle_ProductIdNotFound_ReturnsError()
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync("com.nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.nonexistent"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("com.nonexistent");
        result.Message.Should().Contain("not found in catalog");
    }

    [Fact]
    public async Task Handle_VersionNotFound_ReturnsErrorWithAvailableVersions()
    {
        var productV1 = CreateSingleStackProduct("com.test.product", version: "1.0.0");
        var productV2 = CreateSingleStackProduct("com.test.product", version: "2.0.0");
        SetupProductLookup("com.test.product", productV2);
        SetupProductVersions("com.test.product", new[] { productV2, productV1 });

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.product", Version: "9.9.9"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("9.9.9");
        result.Message.Should().Contain("not found");
        result.Message.Should().Contain("2.0.0");
        result.Message.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task Handle_MultiStackWithoutStackDefinitionName_ReturnsErrorWithAvailableStacks()
    {
        var product = CreateMultiStackProduct("com.test.multi", version: "1.0.0",
            sourceId: "source1", stackNames: new[] { "web", "api", "worker" });
        SetupProductLookup("com.test.multi", product);

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.multi"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("multiple stacks");
        result.Message.Should().Contain("stackDefinitionName");
        result.Message.Should().Contain("web");
        result.Message.Should().Contain("api");
        result.Message.Should().Contain("worker");
    }

    [Fact]
    public async Task Handle_StackDefinitionNameNotFound_ReturnsErrorWithAvailableStacks()
    {
        var product = CreateMultiStackProduct("com.test.multi", version: "1.0.0",
            sourceId: "source1", stackNames: new[] { "web", "api" });
        SetupProductLookup("com.test.multi", product);

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, TestStackName, TestEnvironmentId, new(),
                ProductId: "com.test.multi", StackDefinitionName: "nonexistent"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("nonexistent");
        result.Message.Should().Contain("not found");
        result.Message.Should().Contain("web");
        result.Message.Should().Contain("api");
    }

    #endregion

    #region ProductDeployment-Aware Stack Name Resolution

    [Fact]
    public async Task Handle_ProductIdWithActiveProductDeployment_UsesDeploymentStackName()
    {
        // Product deployed as "ams-project" with stack "Analytics" → derived name "ams-project-analytics"
        var product = CreateSingleStackProduct("com.test.product", version: "1.0.0",
            sourceId: "source1", stackName: "Analytics");
        SetupProductLookup("com.test.product", product);

        var productDeployment = CreateRunningProductDeployment(
            "com.test.product", "ams-project",
            ("Analytics", "Analytics", product.DefaultStack.Id.Value));

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "com.test.product"))
            .Returns(productDeployment);

        // The existing deployment uses the derived name "ams-project-analytics"
        var existingDeployment = CreateRunningDeployment(
            stackName: "ams-project-analytics",
            stackId: product.DefaultStack.Id.Value);
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "ams-project-analytics"))
            .Returns(existingDeployment);

        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, "Analytics", TestEnvironmentId, new(),
                ProductId: "com.test.product", StackDefinitionName: "Analytics"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("redeployed");

        // Should use the derived deployment stack name, not the raw request name
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackName == "ams-project-analytics"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithActiveProductDeployment_MergesVariablesFromExistingDeployment()
    {
        var product = CreateSingleStackProduct("com.test.product", version: "1.0.0",
            sourceId: "source1", stackName: "Analytics");
        SetupProductLookup("com.test.product", product);

        var productDeployment = CreateRunningProductDeployment(
            "com.test.product", "ams-project",
            ("Analytics", "Analytics", product.DefaultStack.Id.Value));

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "com.test.product"))
            .Returns(productDeployment);

        var existingVars = new Dictionary<string, string>
        {
            ["DB_HOST"] = "stored-host",
            ["LOG_LEVEL"] = "Warning"
        };
        var existingDeployment = CreateRunningDeployment(
            stackName: "ams-project-analytics",
            stackId: product.DefaultStack.Id.Value,
            variables: existingVars);
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "ams-project-analytics"))
            .Returns(existingDeployment);

        SetupSuccessfulDeploy();

        var webhookVars = new Dictionary<string, string> { ["LOG_LEVEL"] = "Debug" };
        var result = await _handler.Handle(
            new DeployViaHookCommand(null, "Analytics", TestEnvironmentId, webhookVars,
                ProductId: "com.test.product", StackDefinitionName: "Analytics"),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd =>
                cmd.Variables["DB_HOST"] == "stored-host" &&
                cmd.Variables["LOG_LEVEL"] == "Debug"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithActiveProductDeployment_NoMatchingStack_FallsBackToRequestStackName()
    {
        var product = CreateSingleStackProduct("com.test.product", version: "1.0.0",
            sourceId: "source1", stackName: "Analytics");
        SetupProductLookup("com.test.product", product);

        // Product deployment exists but has different stacks
        var productDeployment = CreateRunningProductDeployment(
            "com.test.product", "ams-project",
            ("OtherStack", "Other Stack", "some-other-id"));

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "com.test.product"))
            .Returns(productDeployment);

        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, "Analytics", TestEnvironmentId, new(),
                ProductId: "com.test.product", StackDefinitionName: "Analytics"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("deployed");

        // Should use the raw request stack name since no matching product stack was found
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackName == "Analytics"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductIdWithNoActiveProductDeployment_DeploysAsStandalone()
    {
        var product = CreateSingleStackProduct("com.test.product", version: "1.0.0",
            sourceId: "source1", stackName: "Analytics");
        SetupProductLookup("com.test.product", product);

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.IsAny<EnvironmentId>(), "com.test.product"))
            .Returns((ProductDeployment?)null);

        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, "Analytics", TestEnvironmentId, new(),
                ProductId: "com.test.product", StackDefinitionName: "Analytics"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("deployed");

        // No product deployment → use raw request stack name
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackName == "Analytics"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StackIdOnly_DoesNotCheckProductDeployment()
    {
        SetupNoExistingDeployment();
        SetupSuccessfulDeploy();

        await _handler.Handle(
            new DeployViaHookCommand(TestStackId, TestStackName, TestEnvironmentId, new()),
            CancellationToken.None);

        // StackId path should NOT look up ProductDeployments
        _productDeploymentRepoMock.Verify(
            r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ProductIdWithActiveProductDeployment_ResponseUsesRequestStackName()
    {
        var product = CreateSingleStackProduct("com.test.product", version: "1.0.0",
            sourceId: "source1", stackName: "Analytics");
        SetupProductLookup("com.test.product", product);

        var productDeployment = CreateRunningProductDeployment(
            "com.test.product", "ams-project",
            ("Analytics", "Analytics", product.DefaultStack.Id.Value));

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "com.test.product"))
            .Returns(productDeployment);

        var existingDeployment = CreateRunningDeployment(
            stackName: "ams-project-analytics",
            stackId: product.DefaultStack.Id.Value);
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "ams-project-analytics"))
            .Returns(existingDeployment);

        SetupSuccessfulDeploy();

        var result = await _handler.Handle(
            new DeployViaHookCommand(null, "Analytics", TestEnvironmentId, new(),
                ProductId: "com.test.product", StackDefinitionName: "Analytics"),
            CancellationToken.None);

        // Response should use the user-facing request stack name, not the derived name
        result.StackName.Should().Be("Analytics");
        result.Message.Should().Contain("Analytics");
    }

    [Fact]
    public async Task Handle_ProductIdWithActiveProductDeployment_CaseInsensitiveStackMatch()
    {
        var product = CreateSingleStackProduct("com.test.product", version: "1.0.0",
            sourceId: "source1", stackName: "ProjectManagement");
        SetupProductLookup("com.test.product", product);

        var productDeployment = CreateRunningProductDeployment(
            "com.test.product", "ams-project",
            ("ProjectManagement", "Project Management", product.DefaultStack.Id.Value));

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "com.test.product"))
            .Returns(productDeployment);

        var existingDeployment = CreateRunningDeployment(
            stackName: "ams-project-projectmanagement",
            stackId: product.DefaultStack.Id.Value);
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(
                It.Is<EnvironmentId>(e => e.Value == Guid.Parse(TestEnvironmentId)),
                "ams-project-projectmanagement"))
            .Returns(existingDeployment);

        SetupSuccessfulDeploy();

        // StackDefinitionName with different casing
        var result = await _handler.Handle(
            new DeployViaHookCommand(null, "ProjectManagement", TestEnvironmentId, new(),
                ProductId: "com.test.product", StackDefinitionName: "projectmanagement"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Action.Should().Be("redeployed");

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(cmd => cmd.StackName == "ams-project-projectmanagement"),
            It.IsAny<CancellationToken>()), Times.Once);
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

    private void SetupProductLookup(string productId, ProductDefinition product)
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void SetupProductVersions(string productId, IEnumerable<ProductDefinition> versions)
    {
        var versionList = versions.ToList();
        // GroupId is the productId since we set explicit productId in CreateSingleStackProduct
        var groupId = versionList.First().GroupId;
        _productSourceMock
            .Setup(s => s.GetProductVersionsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versionList);
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

    private static ProductDeployment CreateRunningProductDeployment(
        string productGroupId,
        string deploymentName,
        params (string stackName, string displayName, string stackId)[] stacks)
    {
        var envId = new EnvironmentId(Guid.Parse(TestEnvironmentId));
        var stackConfigs = stacks.Select(s =>
            new StackDeploymentConfig(s.stackName, s.displayName, s.stackId, 1,
                new Dictionary<string, string>())).ToList();

        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), envId,
            productGroupId, $"source1:{productGroupId}:1.0.0",
            productGroupId, productGroupId, "1.0.0",
            UserId.NewId(), deploymentName,
            stackConfigs,
            new Dictionary<string, string>());

        // Transition each stack to Running
        foreach (var stack in pd.GetStacksInDeployOrder())
        {
            pd.StartStack(stack.StackName, DeploymentId.NewId());
            pd.CompleteStack(stack.StackName);
        }

        return pd;
    }

    #endregion
}

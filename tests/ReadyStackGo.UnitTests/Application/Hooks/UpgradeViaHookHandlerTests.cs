using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.UpgradeStack;
using ReadyStackGo.Application.UseCases.Hooks.UpgradeViaHook;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class UpgradeViaHookHandlerTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<UpgradeViaHookHandler>> _loggerMock;
    private readonly UpgradeViaHookHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestStackId = "source1:myproduct:my-stack";
    private const string TestStackName = "my-stack";
    private const string TestGroupId = "source1:myproduct";

    public UpgradeViaHookHandlerTests()
    {
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _productSourceMock = new Mock<IProductSourceService>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<UpgradeViaHookHandler>>();
        _handler = new UpgradeViaHookHandler(
            _deploymentRepoMock.Object,
            _productSourceMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    private static Deployment CreateRunningDeployment(
        string? stackId = null,
        string? version = null)
    {
        var envId = new EnvironmentId(Guid.Parse(TestEnvironmentId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.Create(),
            envId,
            stackId ?? TestStackId,
            TestStackName,
            $"rsgo-{TestStackName}",
            UserId.Create());
        deployment.MarkAsRunning();
        if (version != null)
        {
            deployment.SetStackVersion(version);
        }
        return deployment;
    }

    private static ProductDefinition CreateProduct(string version, string? groupId = null)
    {
        var stack = new StackDefinition(
            "source1", "my-stack", new ProductId("myproduct"),
            productVersion: version);

        return new ProductDefinition(
            "source1", "myproduct", "My Product",
            stacks: new List<StackDefinition> { stack },
            productVersion: version,
            productId: groupId);
    }

    #region Successful Upgrade

    [Fact]
    public async Task Handle_RunningDeployment_ValidVersion_DelegatesToUpgradeCommand()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0", "2.0.0");
        SetupSuccessfulUpgrade("1.0.0", "2.0.0");

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PreviousVersion.Should().Be("1.0.0");
        result.NewVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Handle_PassesCorrectParametersToUpgradeCommand()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0", "2.0.0");
        SetupSuccessfulUpgrade("1.0.0", "2.0.0");

        await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<UpgradeStackCommand>(cmd =>
                cmd.EnvironmentId == TestEnvironmentId &&
                cmd.DeploymentId == deployment.Id.Value.ToString() &&
                cmd.NewStackId == "source1:myproduct:2.0.0:my-stack" &&
                cmd.SessionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithVariables_PassesVariablesToUpgradeCommand()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0", "2.0.0");
        SetupSuccessfulUpgrade("1.0.0", "2.0.0");

        var variables = new Dictionary<string, string> { ["NEW_VAR"] = "value" };

        await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId, variables),
            CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<UpgradeStackCommand>(cmd =>
                cmd.Variables != null &&
                cmd.Variables["NEW_VAR"] == "value"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsDeploymentIdFromUpgradeResult()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0", "2.0.0");

        var expectedId = Guid.NewGuid().ToString();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpgradeStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpgradeStackResponse
            {
                Success = true,
                DeploymentId = expectedId,
                PreviousVersion = "1.0.0",
                NewVersion = "2.0.0"
            });

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.DeploymentId.Should().Be(expectedId);
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
            new UpgradeViaHookCommand("nonexistent", "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No deployment found");
    }

    #endregion

    #region Invalid Status

    [Fact]
    public async Task Handle_FailedDeployment_ReturnsError()
    {
        var envId = new EnvironmentId(Guid.Parse(TestEnvironmentId));
        var deployment = Deployment.StartInstallation(
            DeploymentId.Create(), envId, TestStackId, TestStackName, $"rsgo-{TestStackName}", UserId.Create());
        deployment.MarkAsFailed("Something broke");
        SetupDeploymentLookup(deployment);

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only running deployments");
    }

    #endregion

    #region Version Not Found

    [Fact]
    public async Task Handle_TargetVersionNotInCatalog_ReturnsError()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0");

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "9.9.9", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Version '9.9.9' not found in catalog");
        result.Message.Should().Contain("Available versions");
    }

    [Fact]
    public async Task Handle_TargetVersionNotFound_ListsAvailableVersions()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0", "2.0.0", "3.0.0");

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "5.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("1.0.0");
        result.Message.Should().Contain("2.0.0");
        result.Message.Should().Contain("3.0.0");
    }

    #endregion

    #region Product Not Found

    [Fact]
    public async Task Handle_ProductNotInCatalog_ReturnsError()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        _productSourceMock
            .Setup(s => s.GetProductAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no longer available in catalog");
    }

    #endregion

    #region Invalid Input

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", "not-a-guid"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID");
    }

    #endregion

    #region Non-Catalog Deployment

    [Fact]
    public async Task Handle_NonCatalogDeployment_ReturnsError()
    {
        // A deployment with a non-parseable StackId (e.g., manual YAML deploy)
        var deployment = CreateRunningDeployment(stackId: "invalid-stack-id");
        SetupDeploymentLookup(deployment);

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not created from catalog");
    }

    #endregion

    #region Upgrade Failure Propagation

    [Fact]
    public async Task Handle_UpgradeCommandFails_PropagatesError()
    {
        var deployment = CreateRunningDeployment(version: "1.0.0");
        SetupDeploymentLookup(deployment);
        SetupProductLookup("1.0.0", "2.0.0");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpgradeStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpgradeStackResponse.Failed("Downgrade not supported"));

        var result = await _handler.Handle(
            new UpgradeViaHookCommand(TestStackName, "2.0.0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Downgrade not supported");
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

    private void SetupProductLookup(params string[] versions)
    {
        var products = versions.Select(v => CreateProduct(v)).ToList();
        var product = products.First();

        _productSourceMock
            .Setup(s => s.GetProductAsync(TestGroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productSourceMock
            .Setup(s => s.GetProductVersionsAsync(product.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
    }

    private void SetupSuccessfulUpgrade(string previousVersion, string newVersion)
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpgradeStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpgradeStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString(),
                PreviousVersion = previousVersion,
                NewVersion = newVersion
            });
    }

    #endregion
}

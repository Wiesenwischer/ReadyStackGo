using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class RedeployProductHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IDeploymentService> _deploymentServiceMock;
    private readonly Mock<ILogger<RedeployProductHandler>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RedeployProductHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public RedeployProductHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _deploymentServiceMock = new Mock<IDeploymentService>();
        _loggerMock = new Mock<ILogger<RedeployProductHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero));

        // Default: removal succeeds
        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = true });

        // Default: deploy succeeds with a valid deployment ID
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse { Success = true, DeploymentId = Guid.NewGuid().ToString() });

        _handler = new RedeployProductHandler(
            _repositoryMock.Object,
            _mediatorMock.Object,
            _deploymentServiceMock.Object,
            _loggerMock.Object,
            timeProvider: _timeProvider);
    }

    #region Helpers

    private static ProductDeployment CreateRunningDeployment(int stackCount = 2, string deploymentName = "test-deploy")
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), deploymentName,
            CreateStackConfigs(stackCount),
            new Dictionary<string, string>());

        for (var i = 0; i < stackCount; i++)
        {
            pd.StartStack($"stack-{i}", DeploymentId.NewId());
            pd.CompleteStack($"stack-{i}");
        }

        return pd;
    }

    private static List<StackDeploymentConfig> CreateStackConfigs(int count)
    {
        var configs = new List<StackDeploymentConfig>();
        for (var i = 0; i < count; i++)
        {
            configs.Add(new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"sid:{i}", 2,
                new Dictionary<string, string> { { $"VAR_{i}", $"value_{i}" } }));
        }
        return configs;
    }

    private RedeployProductCommand CreateCommand(
        string productDeploymentId,
        List<string>? stackNames = null,
        bool continueOnError = false)
    {
        return new RedeployProductCommand(
            TestEnvironmentId,
            productDeploymentId,
            stackNames,
            null,
            null,
            continueOnError);
    }

    private void SetupDeploymentFound(ProductDeployment pd)
    {
        _repositoryMock
            .Setup(r => r.Get(It.IsAny<ProductDeploymentId>()))
            .Returns(pd);
    }

    #endregion

    #region Input Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFailed()
    {
        var command = CreateCommand("not-a-guid");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid product deployment ID");
    }

    [Fact]
    public async Task Handle_ProductDeploymentNotFound_ReturnsFailed()
    {
        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns((ProductDeployment?)null);
        var command = CreateCommand(Guid.NewGuid().ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeploymentCannotRedeploy_ReturnsFailed()
    {
        // A deployment in Deploying state cannot be redeployed
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "stacks:testproduct", "stacks:testproduct:1.0.0",
            "testproduct", "Test Product", "1.0.0",
            UserId.NewId(), "test-deploy",
            CreateStackConfigs(1),
            new Dictionary<string, string>());
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot be redeployed");
    }

    #endregion

    #region Remove Before Redeploy

    [Fact]
    public async Task Handle_AllStacksRedeploy_CallsRemoveForEachStack()
    {
        var pd = CreateRunningDeployment(2);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        await _handler.Handle(command, CancellationToken.None);

        _deploymentServiceMock.Verify(
            d => d.RemoveDeploymentAsync(TestEnvironmentId, It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_AllStacksRedeploy_CallsRemoveBeforeDeploy()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var callOrder = new List<string>();
        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, _) => callOrder.Add("remove"))
            .ReturnsAsync(new DeployComposeResponse { Success = true });
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<DeployStackResponse>, CancellationToken>((_, _) => callOrder.Add("deploy"))
            .ReturnsAsync(new DeployStackResponse { Success = true, DeploymentId = Guid.NewGuid().ToString() });

        await _handler.Handle(command, CancellationToken.None);

        callOrder.Should().Equal("remove", "deploy");
    }

    [Fact]
    public async Task Handle_RemoveFails_FallsBackToMarkAsRemoved()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = false, Message = "Docker error" });

        await _handler.Handle(command, CancellationToken.None);

        _deploymentServiceMock.Verify(
            d => d.MarkDeploymentAsRemovedAsync(TestEnvironmentId, It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RemoveFails_ContinuesWithDeploy()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        _deploymentServiceMock
            .Setup(d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeployComposeResponse { Success = false, Message = "Docker error" });

        var result = await _handler.Handle(command, CancellationToken.None);

        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SelectiveRedeploy_OnlyRemovesTargetedStacks()
    {
        var pd = CreateRunningDeployment(2);
        SetupDeploymentFound(pd);
        // Only redeploy stack-0; stack-1 stays Running
        var command = CreateCommand(pd.Id.Value.ToString(), stackNames: new List<string> { "stack-0" });

        await _handler.Handle(command, CancellationToken.None);

        // Remove called once (only for stack-0)
        _deploymentServiceMock.Verify(
            d => d.RemoveDeploymentAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
        // Deploy called once (only for stack-0)
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Success / Failure Outcomes

    [Fact]
    public async Task Handle_AllStacksSucceed_ReturnsSuccessWithRunningStatus()
    {
        var pd = CreateRunningDeployment(2);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ProductDeploymentStatus.Running.ToString());
        result.StackResults.Should().HaveCount(2);
        result.StackResults.Should().AllSatisfy(s => s.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_DeployFails_ContinueOnErrorFalse_AbortsRemainingStacks()
    {
        var pd = CreateRunningDeployment(3);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString(), continueOnError: false);

        var callCount = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new DeployStackResponse { Success = false, Message = "First stack failed" }
                    : new DeployStackResponse { Success = true, DeploymentId = Guid.NewGuid().ToString() };
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeployFails_ContinueOnErrorTrue_DeploysAllStacks()
    {
        var pd = CreateRunningDeployment(3);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString(), continueOnError: true);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse { Success = false, Message = "Stack failed" });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_ReturnsSessionId_WhenProvidedInCommand()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = new RedeployProductCommand(
            TestEnvironmentId,
            pd.Id.Value.ToString(),
            null, null,
            SessionId: "my-session",
            ContinueOnError: true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.SessionId.Should().Be("my-session");
    }

    [Fact]
    public async Task Handle_GeneratesSessionId_WhenNotProvided()
    {
        var pd = CreateRunningDeployment(1);
        SetupDeploymentFound(pd);
        var command = CreateCommand(pd.Id.Value.ToString());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.SessionId.Should().StartWith("product-redeploy-");
    }

    #endregion
}

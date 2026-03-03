using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;
using ReadyStackGo.Application.UseCases.Hooks.StopContainers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class StopContainersViaHookHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _productDeploymentRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<StopContainersViaHookHandler>> _loggerMock;
    private readonly StopContainersViaHookHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public StopContainersViaHookHandlerTests()
    {
        _productDeploymentRepoMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<StopContainersViaHookHandler>>();
        _handler = new StopContainersViaHookHandler(
            _productDeploymentRepoMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    #region Test Helpers

    private static ProductDeployment CreateRunningDeployment(
        string productGroupId = "com.test.product", int stackCount = 2)
    {
        var stackConfigs = Enumerable.Range(0, stackCount).Select(i =>
            new StackDeploymentConfig(
                $"stack-{i}", $"Stack {i}", $"stacks:test:1.0.0:stack-{i}",
                2, new Dictionary<string, string>()))
            .ToList();

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

    #endregion

    [Fact]
    public async Task Handle_ResolvesProductAndStopsAllContainers()
    {
        var deployment = CreateRunningDeployment();

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.IsAny<EnvironmentId>(), "com.test.product"))
            .Returns(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<StopProductContainersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StopProductContainersResponse
            {
                Success = true, Message = "Stopped", TotalStacks = 2, StoppedStacks = 2
            });

        var result = await _handler.Handle(
            new StopContainersViaHookCommand("com.test.product", null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(2);
        result.StoppedStacks.Should().Be(2);

        _mediatorMock.Verify(m => m.Send(
            It.Is<StopProductContainersCommand>(c =>
                c.ProductDeploymentId == deployment.Id.Value.ToString() &&
                c.StackNames == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithStackDefinitionName_PassesStackFilter()
    {
        var deployment = CreateRunningDeployment();

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), "com.test.product"))
            .Returns(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<StopProductContainersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StopProductContainersResponse { Success = true, TotalStacks = 1, StoppedStacks = 1 });

        var result = await _handler.Handle(
            new StopContainersViaHookCommand("com.test.product", "stack-0", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        _mediatorMock.Verify(m => m.Send(
            It.Is<StopProductContainersCommand>(c =>
                c.StackNames != null && c.StackNames.Contains("stack-0")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyProductId_ReturnsFailed()
    {
        var result = await _handler.Handle(
            new StopContainersViaHookCommand("", null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("ProductId is required");
    }

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsFailed()
    {
        var result = await _handler.Handle(
            new StopContainersViaHookCommand("com.test.product", null, "not-a-guid"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid EnvironmentId");
    }

    [Fact]
    public async Task Handle_NoActiveDeployment_ReturnsFailed()
    {
        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((ProductDeployment?)null);

        var result = await _handler.Handle(
            new StopContainersViaHookCommand("com.test.product", null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No active product deployment found");
    }
}

using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;
using ReadyStackGo.Application.UseCases.Hooks.RestartContainers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Hooks;

public class RestartContainersViaHookHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _productDeploymentRepoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<RestartContainersViaHookHandler>> _loggerMock;
    private readonly RestartContainersViaHookHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public RestartContainersViaHookHandlerTests()
    {
        _productDeploymentRepoMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<RestartContainersViaHookHandler>>();
        _handler = new RestartContainersViaHookHandler(
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
    public async Task Handle_ResolvesProductAndRestartsAllContainers()
    {
        var deployment = CreateRunningDeployment();

        _productDeploymentRepoMock
            .Setup(r => r.GetActiveByProductGroupId(
                It.IsAny<EnvironmentId>(), "com.test.product"))
            .Returns(deployment);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RestartProductContainersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestartProductContainersResponse
            {
                Success = true, Message = "Restarted", TotalStacks = 2, RestartedStacks = 2
            });

        var result = await _handler.Handle(
            new RestartContainersViaHookCommand("com.test.product", null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TotalStacks.Should().Be(2);
        result.RestartedStacks.Should().Be(2);

        _mediatorMock.Verify(m => m.Send(
            It.Is<RestartProductContainersCommand>(c =>
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
            .Setup(m => m.Send(It.IsAny<RestartProductContainersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestartProductContainersResponse { Success = true, TotalStacks = 1, RestartedStacks = 1 });

        var result = await _handler.Handle(
            new RestartContainersViaHookCommand("com.test.product", "stack-1", TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        _mediatorMock.Verify(m => m.Send(
            It.Is<RestartProductContainersCommand>(c =>
                c.StackNames != null && c.StackNames.Contains("stack-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyProductId_ReturnsFailed()
    {
        var result = await _handler.Handle(
            new RestartContainersViaHookCommand("", null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("ProductId is required");
    }

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsFailed()
    {
        var result = await _handler.Handle(
            new RestartContainersViaHookCommand("com.test.product", null, "not-a-guid"),
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
            new RestartContainersViaHookCommand("com.test.product", null, TestEnvironmentId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No active product deployment found");
    }
}

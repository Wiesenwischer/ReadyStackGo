using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class DeployProductHandlerTests
{
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IDeploymentNotificationService> _notificationMock;
    private readonly Mock<INotificationService> _inAppNotificationMock;
    private readonly Mock<ILogger<DeployProductHandler>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DeployProductHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestUserId = Guid.NewGuid().ToString();

    public DeployProductHandlerTests()
    {
        _productSourceMock = new Mock<IProductSourceService>();
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _notificationMock = new Mock<IDeploymentNotificationService>();
        _inAppNotificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<DeployProductHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 17, 12, 0, 0, TimeSpan.Zero));

        _repositoryMock.Setup(r => r.NextIdentity()).Returns(ProductDeploymentId.NewId());

        _handler = new DeployProductHandler(
            _productSourceMock.Object,
            _repositoryMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object,
            _notificationMock.Object,
            _inAppNotificationMock.Object,
            _timeProvider);
    }

    #region Test Helpers

    private static ProductDefinition CreateTestProduct(
        int stackCount = 3,
        string name = "test-product",
        string version = "1.0.0",
        string sourceId = "stacks")
    {
        var stacks = Enumerable.Range(0, stackCount).Select(i =>
        {
            var stackName = $"stack-{i}";
            var productId = new ProductId($"{sourceId}:{name}");
            return new StackDefinition(
                sourceId,
                stackName,
                productId,
                services: new[]
                {
                    new ServiceTemplate { Name = $"svc-{i}-a", Image = "test:latest" },
                    new ServiceTemplate { Name = $"svc-{i}-b", Image = "test:latest" }
                },
                variables: new[]
                {
                    new Variable("SHARED_VAR", "default-shared"),
                    new Variable($"STACK_{i}_VAR", $"default-{i}")
                },
                productName: name,
                productDisplayName: $"Test Product {name}",
                productVersion: version);
        }).ToList();

        return new ProductDefinition(
            sourceId, name, $"Test Product {name}", stacks,
            productVersion: version);
    }

    private DeployProductCommand CreateCommand(
        ProductDefinition product,
        Dictionary<string, string>? sharedVariables = null,
        bool continueOnError = true,
        string? sessionId = null)
    {
        var stackConfigs = product.Stacks.Select(s => new DeployProductStackConfig(
            s.Id.Value,
            $"{product.Name}-{s.Name}",
            new Dictionary<string, string>())).ToList();

        return new DeployProductCommand(
            TestEnvironmentId,
            product.Id,
            stackConfigs,
            sharedVariables ?? new Dictionary<string, string>(),
            sessionId,
            continueOnError,
            TestUserId);
    }

    private void SetupProductFound(ProductDefinition product)
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void SetupNoExistingDeployment()
    {
        _repositoryMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns((ProductDeployment?)null);
    }

    private void SetupAllStacksSucceed()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeployStackCommand cmd, CancellationToken _) => new DeployStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString(),
                StackName = cmd.StackName,
                Message = "Deployed successfully"
            });
    }

    private void SetupStackFailsAtIndex(int failIndex, bool withDeploymentId = true)
    {
        var callIndex = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeployStackCommand cmd, CancellationToken _) =>
            {
                var currentIndex = callIndex++;
                if (currentIndex == failIndex)
                {
                    return new DeployStackResponse
                    {
                        Success = false,
                        DeploymentId = withDeploymentId ? Guid.NewGuid().ToString() : null,
                        StackName = cmd.StackName,
                        Message = $"Stack '{cmd.StackName}' deployment failed"
                    };
                }

                return new DeployStackResponse
                {
                    Success = true,
                    DeploymentId = Guid.NewGuid().ToString(),
                    StackName = cmd.StackName,
                    Message = "Deployed successfully"
                };
            });
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task Handle_AllStacksSucceed_ReturnsRunningStatus()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Running");
        result.ProductDeploymentId.Should().NotBeNullOrEmpty();
        result.ProductName.Should().Be("test-product");
        result.ProductVersion.Should().Be("1.0.0");
        result.StackResults.Should().HaveCount(3);
        result.StackResults.Should().AllSatisfy(sr => sr.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_SingleStackProduct_Succeeds()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Running");
        result.StackResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithProvidedSessionId_UsesIt()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateCommand(product, sessionId: "custom-session"), CancellationToken.None);

        result.SessionId.Should().Be("custom-session");
    }

    [Fact]
    public async Task Handle_WithoutSessionId_GeneratesOne()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.SessionId.Should().StartWith("product-test-product-");
    }

    [Fact]
    public async Task Handle_PersistsAggregate()
    {
        var product = CreateTestProduct(2);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        _repositoryMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Once);
        // Initial save + per-stack save (2) + final save = 4
        _repositoryMock.Verify(r => r.SaveChanges(), Times.AtLeast(3));
    }

    [Fact]
    public async Task Handle_DeploysStacksInOrder()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var deployedStackIds = new List<string>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeployStackCommand cmd, CancellationToken _) =>
            {
                deployedStackIds.Add(cmd.StackId);
                return new DeployStackResponse
                {
                    Success = true,
                    DeploymentId = Guid.NewGuid().ToString(),
                    StackName = cmd.StackName
                };
            });

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        deployedStackIds.Should().HaveCount(3);
        deployedStackIds[0].Should().Contain("stack-0");
        deployedStackIds[1].Should().Contain("stack-1");
        deployedStackIds[2].Should().Contain("stack-2");
    }

    [Fact]
    public async Task Handle_SuppressesPerStackNotifications()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(c => c.SuppressNotification == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsFailed()
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        var command = new DeployProductCommand(
            TestEnvironmentId, "nonexistent:product:1.0.0",
            new List<DeployProductStackConfig>(),
            new Dictionary<string, string>(),
            UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ActiveDeploymentInProgress_ReturnsFailed()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);

        var existingDeployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            "1.0.0", global::ReadyStackGo.Domain.Deployment.UserId.Create(),
            new[] { new StackDeploymentConfig("s", "S", "sid", 1, new Dictionary<string, string>()) },
            new Dictionary<string, string>());

        _repositoryMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns(existingDeployment);

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already in progress");
    }

    [Fact]
    public async Task Handle_ProductAlreadyDeployed_ReturnsFailed()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);

        // Create a running deployment
        var existingDeployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            "1.0.0", global::ReadyStackGo.Domain.Deployment.UserId.Create(),
            new[] { new StackDeploymentConfig("s", "S", "sid", 1, new Dictionary<string, string>()) },
            new Dictionary<string, string>());
        var deploymentId = global::ReadyStackGo.Domain.Deployment.Deployments.DeploymentId.NewId();
        existingDeployment.StartStack("s", deploymentId, "test-s");
        existingDeployment.CompleteStack("s");

        _repositoryMock
            .Setup(r => r.GetActiveByProductGroupId(It.IsAny<EnvironmentId>(), It.IsAny<string>()))
            .Returns(existingDeployment);

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already deployed");
    }

    [Fact]
    public async Task Handle_EmptyStackConfigs_ReturnsFailed()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var command = new DeployProductCommand(
            TestEnvironmentId, product.Id,
            new List<DeployProductStackConfig>(),
            new Dictionary<string, string>(),
            UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("At least one stack");
    }

    [Fact]
    public async Task Handle_InvalidStackId_ReturnsFailed()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var command = new DeployProductCommand(
            TestEnvironmentId, product.Id,
            new List<DeployProductStackConfig>
            {
                new("nonexistent:stack:id", "test-stack", new Dictionary<string, string>())
            },
            new Dictionary<string, string>(),
            UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found in product");
    }

    #endregion

    #region Partial Failure (ContinueOnError: true)

    [Fact]
    public async Task Handle_MiddleStackFails_ContinueOnError_DeploysAllStacks()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(1);

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: true), CancellationToken.None);

        result.StackResults.Should().HaveCount(3);
        result.StackResults[0].Success.Should().BeTrue();
        result.StackResults[1].Success.Should().BeFalse();
        result.StackResults[2].Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MiddleStackFails_ContinueOnError_StatusPartiallyRunning()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(1);

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: true), CancellationToken.None);

        result.Success.Should().BeTrue(); // Partial success is still "success" from API perspective
        result.Status.Should().Be("PartiallyRunning");
    }

    [Fact]
    public async Task Handle_AllStacksFail_StatusFailed()
    {
        var product = CreateTestProduct(2);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = false,
                DeploymentId = Guid.NewGuid().ToString(),
                Message = "Failed"
            });

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Handle_AllStacksFail_ContinueOnError_AttemptsAllStacks()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var callCount = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeployStackCommand cmd, CancellationToken _) =>
            {
                callCount++;
                return new DeployStackResponse
                {
                    Success = false,
                    DeploymentId = Guid.NewGuid().ToString(),
                    StackName = cmd.StackName,
                    Message = "Failed"
                };
            });

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: true), CancellationToken.None);

        // All 3 stacks should have been attempted despite every one failing
        callCount.Should().Be(3);
        result.StackResults.Should().HaveCount(3);
        result.StackResults.Should().AllSatisfy(sr => sr.Success.Should().BeFalse());
        result.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Handle_FirstAndLastFail_MiddleSucceeds_ContinueOnError_StatusPartiallyRunning()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var callIndex = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeployStackCommand cmd, CancellationToken _) =>
            {
                var current = callIndex++;
                var success = current == 1; // Only second stack (index 1) succeeds
                return new DeployStackResponse
                {
                    Success = success,
                    DeploymentId = Guid.NewGuid().ToString(),
                    StackName = cmd.StackName,
                    Message = success ? "OK" : "Failed"
                };
            });

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: true), CancellationToken.None);

        result.StackResults.Should().HaveCount(3);
        result.StackResults[0].Success.Should().BeFalse();
        result.StackResults[1].Success.Should().BeTrue();
        result.StackResults[2].Success.Should().BeFalse();
        result.Status.Should().Be("PartiallyRunning");
    }

    #endregion

    #region Abort on Failure (ContinueOnError: false)

    [Fact]
    public async Task Handle_FirstStackFails_AbortOnError_StopsAfterFailure()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(0);

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: false), CancellationToken.None);

        // Only 1 stack result (the failed one), remaining 2 were never attempted
        result.StackResults.Should().HaveCount(1);
        result.StackResults[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SecondStackFails_AbortOnError_FirstSucceedsSecondFails()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(1);

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: false), CancellationToken.None);

        result.StackResults.Should().HaveCount(2);
        result.StackResults[0].Success.Should().BeTrue();
        result.StackResults[1].Success.Should().BeFalse();
        result.Status.Should().Be("PartiallyRunning");
    }

    [Fact]
    public async Task Handle_FirstStackFails_AbortOnError_StatusFailed_NoSuccessfulStacks()
    {
        var product = CreateTestProduct(3);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(0);

        var result = await _handler.Handle(
            CreateCommand(product, continueOnError: false), CancellationToken.None);

        // When the first stack fails and abort is set, no stacks succeeded
        result.StackResults.Should().HaveCount(1);
        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task Handle_AbortOnError_RemainingStacksNotAttempted()
    {
        var product = CreateTestProduct(4);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(1); // Second stack fails

        var callCount = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeployStackCommand cmd, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 2) // Second call fails
                {
                    return new DeployStackResponse
                    {
                        Success = false,
                        DeploymentId = Guid.NewGuid().ToString(),
                        StackName = cmd.StackName,
                        Message = "Failed"
                    };
                }
                return new DeployStackResponse
                {
                    Success = true,
                    DeploymentId = Guid.NewGuid().ToString(),
                    StackName = cmd.StackName,
                    Message = "OK"
                };
            });

        await _handler.Handle(
            CreateCommand(product, continueOnError: false), CancellationToken.None);

        // Only 2 stacks attempted (first succeeds, second fails, third and fourth not attempted)
        callCount.Should().Be(2);
    }

    #endregion

    #region Pre-Deployment Failure

    [Fact]
    public async Task Handle_DeployReturnsNoDeploymentId_StackFailsFromPending()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(0, withDeploymentId: false);

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StackResults[0].Success.Should().BeFalse();
        result.StackResults[0].DeploymentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DeployThrowsException_StackFails()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var result = await _handler.Handle(CreateCommand(product), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StackResults[0].Success.Should().BeFalse();
        result.StackResults[0].ErrorMessage.Should().Contain("Connection refused");
    }

    #endregion

    #region Variable Merging

    [Fact]
    public async Task Handle_MergesSharedVariablesIntoEachStack()
    {
        var product = CreateTestProduct(2);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var capturedCommands = new List<DeployStackCommand>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<DeployStackResponse>, CancellationToken>((req, _) =>
                capturedCommands.Add((DeployStackCommand)req))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString()
            });

        var sharedVars = new Dictionary<string, string> { ["SHARED_VAR"] = "shared-override" };
        await _handler.Handle(
            CreateCommand(product, sharedVariables: sharedVars), CancellationToken.None);

        capturedCommands.Should().HaveCount(2);
        capturedCommands[0].Variables.Should().ContainKey("SHARED_VAR")
            .WhoseValue.Should().Be("shared-override");
        capturedCommands[1].Variables.Should().ContainKey("SHARED_VAR")
            .WhoseValue.Should().Be("shared-override");
    }

    [Fact]
    public async Task Handle_PerStackVariablesOverrideShared()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var capturedCommands = new List<DeployStackCommand>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<DeployStackResponse>, CancellationToken>((req, _) =>
                capturedCommands.Add((DeployStackCommand)req))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString()
            });

        var sharedVars = new Dictionary<string, string> { ["SHARED_VAR"] = "shared-value" };
        var stackConfigs = new List<DeployProductStackConfig>
        {
            new(product.Stacks[0].Id.Value, "test-stack-0",
                new Dictionary<string, string> { ["SHARED_VAR"] = "per-stack-value" })
        };

        var command = new DeployProductCommand(
            TestEnvironmentId, product.Id, stackConfigs, sharedVars, UserId: TestUserId);

        await _handler.Handle(command, CancellationToken.None);

        capturedCommands[0].Variables["SHARED_VAR"].Should().Be("per-stack-value");
    }

    [Fact]
    public async Task Handle_StackDefaultsUsedWhenNoOverride()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();

        var capturedCommands = new List<DeployStackCommand>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<DeployStackResponse>, CancellationToken>((req, _) =>
                capturedCommands.Add((DeployStackCommand)req))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = true,
                DeploymentId = Guid.NewGuid().ToString()
            });

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        // Variable "STACK_0_VAR" has default "default-0", should be included
        capturedCommands[0].Variables.Should().ContainKey("STACK_0_VAR")
            .WhoseValue.Should().Be("default-0");
    }

    #endregion

    #region SignalR Progress

    [Fact]
    public async Task Handle_SendsProgressBeforeEachStack()
    {
        var product = CreateTestProduct(2);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyProgressAsync(
            It.IsAny<string>(),
            "ProductDeploy",
            It.Is<string>(m => m.Contains("1/2")),
            It.IsAny<int>(),
            It.IsAny<string>(),
            2, // totalStacks
            It.IsAny<int>(),
            0, 0,
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationMock.Verify(n => n.NotifyProgressAsync(
            It.IsAny<string>(),
            "ProductDeploy",
            It.Is<string>(m => m.Contains("2/2")),
            It.IsAny<int>(),
            It.IsAny<string>(),
            2,
            It.IsAny<int>(),
            0, 0,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SendsCompletedOnSuccess()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyCompletedAsync(
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("successfully")),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SendsErrorOnFailure()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupStackFailsAtIndex(0);

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyErrorAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullNotificationService_DoesNotThrow()
    {
        var handler = new DeployProductHandler(
            _productSourceMock.Object,
            _repositoryMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);

        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        var act = () => handler.Handle(CreateCommand(product), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region In-App Notification

    [Fact]
    public async Task Handle_Success_CreatesInAppNotification()
    {
        var product = CreateTestProduct(1);
        SetupProductFound(product);
        SetupNoExistingDeployment();
        SetupAllStacksSucceed();

        await _handler.Handle(CreateCommand(product), CancellationToken.None);

        _inAppNotificationMock.Verify(n => n.AddAsync(
            It.Is<global::ReadyStackGo.Application.Notifications.Notification>(notif =>
                notif.Type == global::ReadyStackGo.Application.Notifications.NotificationType.ProductDeploymentResult),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

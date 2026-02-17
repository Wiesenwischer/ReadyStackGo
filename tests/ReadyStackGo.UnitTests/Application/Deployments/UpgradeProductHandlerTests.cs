using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Application.UseCases.Deployments.UpgradeProduct;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class UpgradeProductHandlerTests
{
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IDeploymentNotificationService> _notificationMock;
    private readonly Mock<INotificationService> _inAppNotificationMock;
    private readonly Mock<ILogger<UpgradeProductHandler>> _loggerMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly UpgradeProductHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();
    private static readonly string TestUserId = Guid.NewGuid().ToString();

    public UpgradeProductHandlerTests()
    {
        _productSourceMock = new Mock<IProductSourceService>();
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _mediatorMock = new Mock<IMediator>();
        _notificationMock = new Mock<IDeploymentNotificationService>();
        _inAppNotificationMock = new Mock<INotificationService>();
        _loggerMock = new Mock<ILogger<UpgradeProductHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 17, 12, 0, 0, TimeSpan.Zero));

        _repositoryMock.Setup(r => r.NextIdentity()).Returns(ProductDeploymentId.NewId());

        _handler = new UpgradeProductHandler(
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
        string sourceId = "stacks",
        List<string>? stackNames = null)
    {
        var stacks = Enumerable.Range(0, stackCount).Select(i =>
        {
            var stackName = stackNames != null && i < stackNames.Count ? stackNames[i] : $"stack-{i}";
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

    private static ProductDeployment CreateExistingDeployment(
        ProductDefinition product,
        ProductDeploymentStatus targetStatus = ProductDeploymentStatus.Running)
    {
        // Use deployment-style names as StackName (matching production behavior),
        // and logical names as StackDisplayName
        var stackConfigs = product.Stacks.Select((s, i) => new StackDeploymentConfig(
            $"test-{s.Name}", s.Name, s.Id.Value, s.Services.Count,
            new Dictionary<string, string>
            {
                ["SHARED_VAR"] = "existing-shared",
                [$"STACK_{i}_VAR"] = $"existing-{i}"
            })).ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            product.ProductVersion ?? "1.0.0",
            UserId.Create(),
            stackConfigs,
            new Dictionary<string, string> { ["SHARED_VAR"] = "existing-shared" });

        // Transition to Running by completing all stacks
        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            var depId = DeploymentId.NewId();
            deployment.StartStack(stack.StackName, depId, stack.StackName);
            deployment.CompleteStack(stack.StackName);
        }

        if (targetStatus == ProductDeploymentStatus.PartiallyRunning)
        {
            // This won't work directly since all stacks completed, but for PartiallyRunning
            // we'd need mixed results. For testing purposes, Running is close enough.
        }

        return deployment;
    }

    private UpgradeProductCommand CreateUpgradeCommand(
        ProductDeployment existing,
        ProductDefinition targetProduct,
        Dictionary<string, string>? sharedVariables = null,
        bool continueOnError = true,
        string? sessionId = null)
    {
        var stackConfigs = targetProduct.Stacks.Select(s =>
        {
            // Match by StackDisplayName (logical name from manifest), reuse StackName (deployment name) if exists
            var existingStack = existing.Stacks.FirstOrDefault(es =>
                es.StackDisplayName.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            var deploymentStackName = existingStack?.StackName ?? $"{targetProduct.Name}-{s.Name}";

            return new UpgradeProductStackConfig(
                s.Id.Value,
                deploymentStackName,
                new Dictionary<string, string>());
        }).ToList();

        return new UpgradeProductCommand(
            TestEnvironmentId,
            existing.Id.Value.ToString(),
            targetProduct.Id,
            stackConfigs,
            sharedVariables ?? new Dictionary<string, string>(),
            sessionId,
            continueOnError,
            TestUserId);
    }

    private void SetupExistingDeployment(ProductDeployment deployment)
    {
        _repositoryMock
            .Setup(r => r.Get(It.Is<ProductDeploymentId>(id => id == deployment.Id)))
            .Returns(deployment);
    }

    private void SetupTargetProductFound(ProductDefinition product)
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
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
                        Message = $"Stack '{cmd.StackName}' upgrade failed"
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
        var currentProduct = CreateTestProduct(3, version: "1.0.0");
        var targetProduct = CreateTestProduct(3, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Running");
        result.ProductDeploymentId.Should().NotBeNullOrEmpty();
        result.ProductName.Should().Be("test-product");
        result.PreviousVersion.Should().Be("1.0.0");
        result.NewVersion.Should().Be("2.0.0");
        result.StackResults.Should().HaveCount(3);
        result.StackResults.Should().AllSatisfy(sr => sr.Success.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_SingleStackProduct_Succeeds()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("Running");
        result.StackResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithProvidedSessionId_UsesIt()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, sessionId: "custom-session"),
            CancellationToken.None);

        result.SessionId.Should().Be("custom-session");
    }

    [Fact]
    public async Task Handle_WithoutSessionId_GeneratesOne()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.SessionId.Should().StartWith("product-upgrade-test-product-");
    }

    [Fact]
    public async Task Handle_PersistsNewAggregate()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var targetProduct = CreateTestProduct(2, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        _repositoryMock.Verify(r => r.Add(It.IsAny<ProductDeployment>()), Times.Once);
        _repositoryMock.Verify(r => r.SaveChanges(), Times.AtLeast(3));
    }

    [Fact]
    public async Task Handle_NewAggregateHasUpgradeStatus()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        ProductDeployment? capturedDeployment = null;
        _repositoryMock
            .Setup(r => r.Add(It.IsAny<ProductDeployment>()))
            .Callback<ProductDeployment>(pd => capturedDeployment = pd);

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        capturedDeployment.Should().NotBeNull();
        capturedDeployment!.PreviousVersion.Should().Be("1.0.0");
        capturedDeployment.ProductVersion.Should().Be("2.0.0");
        capturedDeployment.UpgradeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SuppressesPerStackNotifications()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeployStackCommand>(c => c.SuppressNotification == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidProductDeploymentId_ReturnsFailed()
    {
        var command = new UpgradeProductCommand(
            TestEnvironmentId, "not-a-guid", "target-product",
            new List<UpgradeProductStackConfig>(),
            new Dictionary<string, string>(), UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid product deployment ID");
    }

    [Fact]
    public async Task Handle_ProductDeploymentNotFound_ReturnsFailed()
    {
        _repositoryMock
            .Setup(r => r.Get(It.IsAny<ProductDeploymentId>()))
            .Returns((ProductDeployment?)null);

        var command = new UpgradeProductCommand(
            TestEnvironmentId, Guid.NewGuid().ToString(), "target-product",
            new List<UpgradeProductStackConfig>(),
            new Dictionary<string, string>(), UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeploymentNotOperational_ReturnsFailed()
    {
        // Create a deployment that is still in Deploying status (not Running)
        var product = CreateTestProduct(1, version: "1.0.0");
        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            "1.0.0", UserId.Create(),
            new[] { new StackDeploymentConfig("s", "S", "sid", 1, new Dictionary<string, string>()) },
            new Dictionary<string, string>());

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);

        var command = new UpgradeProductCommand(
            TestEnvironmentId, deployment.Id.Value.ToString(), "target-product",
            new List<UpgradeProductStackConfig>(),
            new Dictionary<string, string>(), UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot be upgraded");
    }

    [Fact]
    public async Task Handle_TargetProductNotFound_ReturnsFailed()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var existing = CreateExistingDeployment(currentProduct);
        SetupExistingDeployment(existing);

        _productSourceMock
            .Setup(s => s.GetProductAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        var command = new UpgradeProductCommand(
            TestEnvironmentId, existing.Id.Value.ToString(), "nonexistent:product",
            new List<UpgradeProductStackConfig>
            {
                new("sid", "name", new Dictionary<string, string>())
            },
            new Dictionary<string, string>(), UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found in catalog");
    }

    [Fact]
    public async Task Handle_SameVersion_ReturnsFailed()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "1.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already running version");
    }

    [Fact]
    public async Task Handle_DowngradeAttempt_ReturnsFailed()
    {
        var currentProduct = CreateTestProduct(1, version: "2.0.0");
        var targetProduct = CreateTestProduct(1, version: "1.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Downgrade");
    }

    [Fact]
    public async Task Handle_EmptyStackConfigs_ReturnsFailed()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

        var command = new UpgradeProductCommand(
            TestEnvironmentId, existing.Id.Value.ToString(), targetProduct.Id,
            new List<UpgradeProductStackConfig>(),
            new Dictionary<string, string>(), UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("At least one stack");
    }

    [Fact]
    public async Task Handle_InvalidStackIdInTarget_ReturnsFailed()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

        var command = new UpgradeProductCommand(
            TestEnvironmentId, existing.Id.Value.ToString(), targetProduct.Id,
            new List<UpgradeProductStackConfig>
            {
                new("nonexistent:stack:id", "test-stack", new Dictionary<string, string>())
            },
            new Dictionary<string, string>(), UserId: TestUserId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found in target product");
    }

    #endregion

    #region Partial Failure (ContinueOnError: true)

    [Fact]
    public async Task Handle_MiddleStackFails_ContinueOnError_UpgradesAllStacks()
    {
        var currentProduct = CreateTestProduct(3, version: "1.0.0");
        var targetProduct = CreateTestProduct(3, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupStackFailsAtIndex(1);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, continueOnError: true),
            CancellationToken.None);

        result.StackResults.Should().HaveCount(3);
        result.StackResults[0].Success.Should().BeTrue();
        result.StackResults[1].Success.Should().BeFalse();
        result.StackResults[2].Success.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MiddleStackFails_ContinueOnError_StatusPartiallyRunning()
    {
        var currentProduct = CreateTestProduct(3, version: "1.0.0");
        var targetProduct = CreateTestProduct(3, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupStackFailsAtIndex(1);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, continueOnError: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("PartiallyRunning");
    }

    [Fact]
    public async Task Handle_AllStacksFail_StatusFailed()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var targetProduct = CreateTestProduct(2, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeployStackResponse
            {
                Success = false,
                DeploymentId = Guid.NewGuid().ToString(),
                Message = "Failed"
            });

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, continueOnError: true),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be("Failed");
    }

    #endregion

    #region Abort on Failure (ContinueOnError: false)

    [Fact]
    public async Task Handle_FirstStackFails_AbortOnError_StopsAfterFailure()
    {
        var currentProduct = CreateTestProduct(3, version: "1.0.0");
        var targetProduct = CreateTestProduct(3, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupStackFailsAtIndex(0);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, continueOnError: false),
            CancellationToken.None);

        result.StackResults.Should().HaveCount(1);
        result.StackResults[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SecondStackFails_AbortOnError_FirstSucceedsSecondFails()
    {
        var currentProduct = CreateTestProduct(3, version: "1.0.0");
        var targetProduct = CreateTestProduct(3, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupStackFailsAtIndex(1);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, continueOnError: false),
            CancellationToken.None);

        result.StackResults.Should().HaveCount(2);
        result.StackResults[0].Success.Should().BeTrue();
        result.StackResults[1].Success.Should().BeFalse();
        result.Status.Should().Be("PartiallyRunning");
    }

    #endregion

    #region Pre-Deployment Failure

    [Fact]
    public async Task Handle_DeployReturnsNoDeploymentId_StackFailsFromPending()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupStackFailsAtIndex(0, withDeploymentId: false);

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StackResults[0].Success.Should().BeFalse();
        result.StackResults[0].DeploymentId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DeployThrowsException_StackFails()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeployStackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StackResults[0].Success.Should().BeFalse();
        result.StackResults[0].ErrorMessage.Should().Contain("Connection refused");
    }

    #endregion

    #region Variable Merging

    [Fact]
    public async Task Handle_ExistingVariablesPreservedDuringUpgrade()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

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

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        capturedCommands.Should().HaveCount(1);
        // Existing deployment had STACK_0_VAR = "existing-0"
        capturedCommands[0].Variables.Should().ContainKey("STACK_0_VAR")
            .WhoseValue.Should().Be("existing-0");
    }

    [Fact]
    public async Task Handle_SharedOverridesExistingValues()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

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

        var sharedVars = new Dictionary<string, string> { ["SHARED_VAR"] = "new-shared-override" };
        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct, sharedVariables: sharedVars),
            CancellationToken.None);

        capturedCommands[0].Variables.Should().ContainKey("SHARED_VAR")
            .WhoseValue.Should().Be("new-shared-override");
    }

    [Fact]
    public async Task Handle_PerStackOverridesEverything()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

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
        var stackConfigs = new List<UpgradeProductStackConfig>
        {
            new(targetProduct.Stacks[0].Id.Value, "test-stack-0",
                new Dictionary<string, string> { ["SHARED_VAR"] = "per-stack-value" })
        };

        var command = new UpgradeProductCommand(
            TestEnvironmentId, existing.Id.Value.ToString(), targetProduct.Id,
            stackConfigs, sharedVars, UserId: TestUserId);

        await _handler.Handle(command, CancellationToken.None);

        capturedCommands[0].Variables["SHARED_VAR"].Should().Be("per-stack-value");
    }

    [Fact]
    public async Task Handle_NewStackDefaults_UsedWhenNoExistingValues()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0",
            stackNames: new List<string> { "stack-0" });
        var targetProduct = CreateTestProduct(2, version: "2.0.0",
            stackNames: new List<string> { "stack-0", "new-stack" });
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);

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

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        capturedCommands.Should().HaveCount(2);
        // New stack should use defaults since no existing values
        capturedCommands[1].Variables.Should().ContainKey("STACK_1_VAR")
            .WhoseValue.Should().Be("default-1");
    }

    #endregion

    #region Stack Matching — New and Missing Stacks

    [Fact]
    public async Task Handle_NewStacksInTarget_MarkedAsNew()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0",
            stackNames: new List<string> { "stack-0" });
        var targetProduct = CreateTestProduct(2, version: "2.0.0",
            stackNames: new List<string> { "stack-0", "new-stack" });
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.StackResults.Should().HaveCount(2);
        result.StackResults[0].IsNewInUpgrade.Should().BeFalse();
        result.StackResults[1].IsNewInUpgrade.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingStacksInTarget_WarningGenerated()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0",
            stackNames: new List<string> { "stack-0", "old-stack" });
        var targetProduct = CreateTestProduct(1, version: "2.0.0",
            stackNames: new List<string> { "stack-0" });
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Warnings.Should().NotBeNull();
        result.Warnings.Should().ContainSingle(w => w.Contains("old-stack"));
    }

    [Fact]
    public async Task Handle_NoMissingStacks_NoWarnings()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var targetProduct = CreateTestProduct(2, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var result = await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        result.Warnings.Should().BeNull();
    }

    #endregion

    #region SignalR Progress

    [Fact]
    public async Task Handle_SendsProgressBeforeEachStack()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var targetProduct = CreateTestProduct(2, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyProgressAsync(
            It.IsAny<string>(),
            "ProductUpgrade",
            It.Is<string>(m => m.Contains("1/2")),
            It.IsAny<int>(),
            It.IsAny<string>(),
            2,
            It.IsAny<int>(),
            0, 0,
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationMock.Verify(n => n.NotifyProgressAsync(
            It.IsAny<string>(),
            "ProductUpgrade",
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
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        _notificationMock.Verify(n => n.NotifyCompletedAsync(
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("upgraded") && m.Contains("v2.0.0")),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SendsErrorOnFailure()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupStackFailsAtIndex(0);

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

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
        var handler = new UpgradeProductHandler(
            _productSourceMock.Object,
            _repositoryMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);

        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        var act = () => handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region In-App Notification

    [Fact]
    public async Task Handle_Success_CreatesInAppNotification()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0");
        var targetProduct = CreateTestProduct(1, version: "2.0.0");
        var existing = CreateExistingDeployment(currentProduct);

        SetupExistingDeployment(existing);
        SetupTargetProductFound(targetProduct);
        SetupAllStacksSucceed();

        await _handler.Handle(
            CreateUpgradeCommand(existing, targetProduct), CancellationToken.None);

        _inAppNotificationMock.Verify(n => n.AddAsync(
            It.Is<global::ReadyStackGo.Application.Notifications.Notification>(notif =>
                notif.Type == global::ReadyStackGo.Application.Notifications.NotificationType.ProductDeploymentResult),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Version Comparison

    [Fact]
    public void CompareVersions_HigherVersion_ReturnsNegative()
    {
        UpgradeProductHandler.CompareVersions("1.0.0", "2.0.0").Should().Be(-1);
    }

    [Fact]
    public void CompareVersions_SameVersion_ReturnsZero()
    {
        UpgradeProductHandler.CompareVersions("1.0.0", "1.0.0").Should().Be(0);
    }

    [Fact]
    public void CompareVersions_LowerVersion_ReturnsPositive()
    {
        UpgradeProductHandler.CompareVersions("2.0.0", "1.0.0").Should().Be(1);
    }

    [Fact]
    public void CompareVersions_WithVPrefix_WorksCorrectly()
    {
        UpgradeProductHandler.CompareVersions("v1.0.0", "v2.0.0").Should().Be(-1);
    }

    [Fact]
    public void CompareVersions_InvalidVersion_ReturnsNull()
    {
        UpgradeProductHandler.CompareVersions("abc", "def").Should().BeNull();
    }

    [Fact]
    public void CompareVersions_NullVersions_ReturnsZero()
    {
        UpgradeProductHandler.CompareVersions(null, null).Should().Be(0);
    }

    [Fact]
    public void CompareVersions_NullFirst_ReturnsNegative()
    {
        UpgradeProductHandler.CompareVersions(null, "1.0.0").Should().Be(-1);
    }

    #endregion

    #region MergeVariables

    [Fact]
    public void MergeVariables_FourTierPriority_WorksCorrectly()
    {
        var stackDef = CreateTestProduct(1).Stacks[0];
        var existing = new Dictionary<string, string> { ["SHARED_VAR"] = "existing" };
        var shared = new Dictionary<string, string> { ["SHARED_VAR"] = "shared" };
        var perStack = new Dictionary<string, string> { ["SHARED_VAR"] = "per-stack" };

        var result = UpgradeProductHandler.MergeVariables(stackDef, existing, shared, perStack);

        result["SHARED_VAR"].Should().Be("per-stack"); // Highest priority wins
    }

    [Fact]
    public void MergeVariables_WithoutExistingValues_UsesThreeTierMerge()
    {
        var stackDef = CreateTestProduct(1).Stacks[0];
        var shared = new Dictionary<string, string> { ["SHARED_VAR"] = "shared" };
        var perStack = new Dictionary<string, string>();

        var result = UpgradeProductHandler.MergeVariables(stackDef, null, shared, perStack);

        result["SHARED_VAR"].Should().Be("shared");
        result["STACK_0_VAR"].Should().Be("default-0"); // Stack default
    }

    [Fact]
    public void MergeVariables_ExistingOverridesDefaults()
    {
        var stackDef = CreateTestProduct(1).Stacks[0];
        var existing = new Dictionary<string, string>
        {
            ["SHARED_VAR"] = "user-configured",
            ["STACK_0_VAR"] = "user-configured-stack"
        };

        var result = UpgradeProductHandler.MergeVariables(
            stackDef, existing, new Dictionary<string, string>(), new Dictionary<string, string>());

        result["SHARED_VAR"].Should().Be("user-configured");
        result["STACK_0_VAR"].Should().Be("user-configured-stack");
    }

    #endregion
}

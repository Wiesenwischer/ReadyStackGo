using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class RunProductPrecheckHandlerTests
{
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<RunProductPrecheckHandler>> _loggerMock;
    private readonly RunProductPrecheckHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public RunProductPrecheckHandlerTests()
    {
        _productSourceMock = new Mock<IProductSourceService>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<RunProductPrecheckHandler>>();

        _handler = new RunProductPrecheckHandler(
            _productSourceMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    #region Test Helpers

    private static ProductDefinition CreateTestProduct(
        int stackCount = 2,
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
                    new ServiceTemplate { Name = $"svc-{i}", Image = "test:latest" }
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

    private RunProductPrecheckQuery CreateQuery(
        ProductDefinition product,
        Dictionary<string, string>? sharedVariables = null)
    {
        var stackConfigs = product.Stacks.Select(s => new ProductPrecheckStackConfig(
            s.Id.Value,
            new Dictionary<string, string>())).ToList();

        return new RunProductPrecheckQuery(
            TestEnvironmentId,
            product.Id,
            "test-deployment",
            stackConfigs,
            sharedVariables ?? new Dictionary<string, string>());
    }

    private void SetupProductFound(ProductDefinition product)
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void SetupAllStackPrechecksOK()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RunDeploymentPrecheckQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")]));
    }

    #endregion

    [Fact]
    public async Task Handle_ProductNotFound_ReturnsError()
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDefinition?)null);

        var query = new RunProductPrecheckQuery(
            TestEnvironmentId, "nonexistent", "test", [], new Dictionary<string, string>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.Stacks.Should().HaveCount(1);
        result.Stacks[0].Result.Checks.Should().Contain(c => c.Title == "Product not found");
    }

    [Fact]
    public async Task Handle_EmptyStackConfigs_ReturnsError()
    {
        var product = CreateTestProduct();
        SetupProductFound(product);

        var query = new RunProductPrecheckQuery(
            TestEnvironmentId, product.Id, "test", [], new Dictionary<string, string>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.Stacks.Should().HaveCount(1);
        result.Stacks[0].Result.Checks.Should().Contain(c => c.Title == "No stack configurations provided");
    }

    [Fact]
    public async Task Handle_InvalidStackId_ReturnsErrorForThatStack()
    {
        var product = CreateTestProduct(stackCount: 1);
        SetupProductFound(product);
        SetupAllStackPrechecksOK();

        var stackConfigs = new List<ProductPrecheckStackConfig>
        {
            new(product.Stacks[0].Id.Value, new Dictionary<string, string>()),
            new("nonexistent-stack", new Dictionary<string, string>())
        };

        var query = new RunProductPrecheckQuery(
            TestEnvironmentId, product.Id, "test", stackConfigs, new Dictionary<string, string>());

        var result = await _handler.Handle(query, CancellationToken.None);

        result.CanDeploy.Should().BeFalse();
        result.Stacks.Should().HaveCount(2);
        result.Stacks[1].Result.HasErrors.Should().BeTrue();
        result.Stacks[1].Result.Checks.Should().Contain(c => c.Title == "Stack not found");
    }

    [Fact]
    public async Task Handle_AllStacksOK_ReturnsCanDeploy()
    {
        var product = CreateTestProduct(stackCount: 3);
        SetupProductFound(product);
        SetupAllStackPrechecksOK();

        var query = CreateQuery(product);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.CanDeploy.Should().BeTrue();
        result.HasErrors.Should().BeFalse();
        result.Stacks.Should().HaveCount(3);
        result.Stacks.Should().AllSatisfy(s => s.Result.CanDeploy.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_OneStackHasError_ReturnsCannotDeploy()
    {
        var product = CreateTestProduct(stackCount: 2);
        SetupProductFound(product);

        // First stack OK, second stack has error
        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RunDeploymentPrecheckQuery>(q => q.StackId == product.Stacks[0].Id.Value),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")]));

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RunDeploymentPrecheckQuery>(q => q.StackId == product.Stacks[1].Id.Value),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrecheckResult([
                new PrecheckItem("Port", PrecheckSeverity.Error, "Port conflict")]));

        var query = CreateQuery(product);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.Stacks[0].Result.CanDeploy.Should().BeTrue();
        result.Stacks[1].Result.CanDeploy.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MergesSharedAndPerStackVariables()
    {
        var product = CreateTestProduct(stackCount: 1);
        SetupProductFound(product);

        Dictionary<string, string>? capturedVariables = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RunDeploymentPrecheckQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PrecheckResult>, CancellationToken>((req, _) =>
            {
                var query = (RunDeploymentPrecheckQuery)req;
                capturedVariables = query.Variables;
            })
            .ReturnsAsync(PrecheckResult.Empty);

        var sharedVars = new Dictionary<string, string> { { "SHARED_VAR", "from-shared" } };
        var perStackVars = new Dictionary<string, string> { { "STACK_0_VAR", "from-per-stack" } };

        var query = new RunProductPrecheckQuery(
            TestEnvironmentId,
            product.Id,
            "test",
            [new ProductPrecheckStackConfig(product.Stacks[0].Id.Value, perStackVars)],
            sharedVars);

        await _handler.Handle(query, CancellationToken.None);

        capturedVariables.Should().NotBeNull();
        capturedVariables!["SHARED_VAR"].Should().Be("from-shared");
        capturedVariables["STACK_0_VAR"].Should().Be("from-per-stack");
    }

    [Fact]
    public async Task Handle_PerStackVariablesOverrideShared()
    {
        var product = CreateTestProduct(stackCount: 1);
        SetupProductFound(product);

        Dictionary<string, string>? capturedVariables = null;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RunDeploymentPrecheckQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PrecheckResult>, CancellationToken>((req, _) =>
            {
                capturedVariables = ((RunDeploymentPrecheckQuery)req).Variables;
            })
            .ReturnsAsync(PrecheckResult.Empty);

        var sharedVars = new Dictionary<string, string> { { "SHARED_VAR", "shared-value" } };
        var perStackVars = new Dictionary<string, string> { { "SHARED_VAR", "overridden-value" } };

        var query = new RunProductPrecheckQuery(
            TestEnvironmentId,
            product.Id,
            "test",
            [new ProductPrecheckStackConfig(product.Stacks[0].Id.Value, perStackVars)],
            sharedVars);

        await _handler.Handle(query, CancellationToken.None);

        capturedVariables!["SHARED_VAR"].Should().Be("overridden-value");
    }

    [Fact]
    public async Task Handle_DerivesCorrectStackDeploymentNames()
    {
        var product = CreateTestProduct(stackCount: 2);
        SetupProductFound(product);

        var capturedNames = new List<string>();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RunDeploymentPrecheckQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PrecheckResult>, CancellationToken>((req, _) =>
            {
                capturedNames.Add(((RunDeploymentPrecheckQuery)req).StackName);
            })
            .ReturnsAsync(PrecheckResult.Empty);

        var query = CreateQuery(product);

        await _handler.Handle(query, CancellationToken.None);

        capturedNames.Should().Contain("test-deployment-stack-0");
        capturedNames.Should().Contain("test-deployment-stack-1");
    }

    [Fact]
    public async Task Handle_PrecheckThrowsException_ReturnsWarningForThatStack()
    {
        var product = CreateTestProduct(stackCount: 2);
        SetupProductFound(product);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RunDeploymentPrecheckQuery>(q => q.StackId == product.Stacks[0].Id.Value),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")]));

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<RunDeploymentPrecheckQuery>(q => q.StackId == product.Stacks[1].Id.Value),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker connection failed"));

        var query = CreateQuery(product);

        var result = await _handler.Handle(query, CancellationToken.None);

        // Exception produces a warning, not an error — deployment can still proceed
        result.CanDeploy.Should().BeTrue();
        result.Stacks[1].Result.HasWarnings.Should().BeTrue();
        result.Stacks[1].Result.Checks.Should().Contain(c =>
            c.Title == "Precheck failed" && c.Detail!.Contains("Docker connection failed"));
    }

    [Fact]
    public async Task Handle_RunsPrechecksInParallel()
    {
        var product = CreateTestProduct(stackCount: 3);
        SetupProductFound(product);

        var callCount = 0;
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RunDeploymentPrecheckQuery>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<PrecheckResult>, CancellationToken>(async (_, ct) =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10, ct);
                return PrecheckResult.Empty;
            });

        var query = CreateQuery(product);

        var result = await _handler.Handle(query, CancellationToken.None);

        callCount.Should().Be(3);
        result.Stacks.Should().HaveCount(3);
    }
}

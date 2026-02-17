using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.CheckProductUpgrade;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class CheckProductUpgradeHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock;
    private readonly Mock<IProductSourceService> _productSourceMock;
    private readonly Mock<ILogger<CheckProductUpgradeHandler>> _loggerMock;
    private readonly CheckProductUpgradeHandler _handler;

    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    public CheckProductUpgradeHandlerTests()
    {
        _repositoryMock = new Mock<IProductDeploymentRepository>();
        _productSourceMock = new Mock<IProductSourceService>();
        _loggerMock = new Mock<ILogger<CheckProductUpgradeHandler>>();

        _handler = new CheckProductUpgradeHandler(
            _repositoryMock.Object,
            _productSourceMock.Object,
            _loggerMock.Object);
    }

    #region Test Helpers

    private static ProductDefinition CreateTestProduct(
        int stackCount = 2,
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
                sourceId, stackName, productId,
                services: new[] { new ServiceTemplate { Name = $"svc-{i}", Image = "test:latest" } },
                variables: new[] { new Variable($"VAR_{i}", $"default-{i}") },
                productName: name, productDisplayName: $"Test {name}",
                productVersion: version);
        }).ToList();

        return new ProductDefinition(sourceId, name, $"Test {name}", stacks, productVersion: version);
    }

    private static ProductDeployment CreateRunningDeployment(
        ProductDefinition product)
    {
        var stackConfigs = product.Stacks.Select((s, i) => new StackDeploymentConfig(
            s.Name, s.Name, s.Id.Value, s.Services.Count,
            new Dictionary<string, string> { [$"VAR_{i}"] = $"value-{i}" })).ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            product.ProductVersion ?? "1.0.0",
            UserId.Create(),
            stackConfigs,
            new Dictionary<string, string>());

        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId(), $"test-{stack.StackName}");
            deployment.CompleteStack(stack.StackName);
        }

        return deployment;
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task Handle_UpgradeAvailable_ReturnsUpgradeInfo()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var deployment = CreateRunningDeployment(currentProduct);
        var upgradeProduct = CreateTestProduct(2, version: "2.0.0");

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        _productSourceMock
            .Setup(s => s.GetAvailableUpgradesAsync(It.IsAny<string>(), "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { upgradeProduct });

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.UpgradeAvailable.Should().BeTrue();
        result.CurrentVersion.Should().Be("1.0.0");
        result.LatestVersion.Should().Be("2.0.0");
        result.LatestProductId.Should().NotBeNullOrEmpty();
        result.CanUpgrade.Should().BeTrue();
        result.AvailableVersions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NoUpgradeAvailable_ReturnsLatestVersion()
    {
        var product = CreateTestProduct(2, version: "2.0.0");
        var deployment = CreateRunningDeployment(product);

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        _productSourceMock
            .Setup(s => s.GetAvailableUpgradesAsync(It.IsAny<string>(), "2.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<ProductDefinition>());

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.UpgradeAvailable.Should().BeFalse();
        result.CurrentVersion.Should().Be("2.0.0");
        result.LatestVersion.Should().BeNull();
        result.CanUpgrade.Should().BeTrue();
        result.Message.Should().Contain("latest version");
    }

    [Fact]
    public async Task Handle_MultipleUpgradesAvailable_ReturnsAll()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var deployment = CreateRunningDeployment(currentProduct);
        var upgrade1 = CreateTestProduct(2, version: "3.0.0");
        var upgrade2 = CreateTestProduct(2, version: "2.0.0");

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        _productSourceMock
            .Setup(s => s.GetAvailableUpgradesAsync(It.IsAny<string>(), "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { upgrade1, upgrade2 });

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.AvailableVersions.Should().HaveCount(2);
        result.LatestVersion.Should().Be("3.0.0");
    }

    #endregion

    #region Stack Change Detection

    [Fact]
    public async Task Handle_NewStacksInUpgrade_DetectsNewStacks()
    {
        var currentProduct = CreateTestProduct(1, version: "1.0.0",
            stackNames: new List<string> { "stack-a" });
        var deployment = CreateRunningDeployment(currentProduct);
        var upgradeProduct = CreateTestProduct(2, version: "2.0.0",
            stackNames: new List<string> { "stack-a", "stack-b" });

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        _productSourceMock
            .Setup(s => s.GetAvailableUpgradesAsync(It.IsAny<string>(), "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { upgradeProduct });

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.NewStacks.Should().ContainSingle("stack-b");
        result.RemovedStacks.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RemovedStacksInUpgrade_DetectsRemovedStacks()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0",
            stackNames: new List<string> { "stack-a", "stack-b" });
        var deployment = CreateRunningDeployment(currentProduct);
        var upgradeProduct = CreateTestProduct(1, version: "2.0.0",
            stackNames: new List<string> { "stack-a" });

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        _productSourceMock
            .Setup(s => s.GetAvailableUpgradesAsync(It.IsAny<string>(), "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { upgradeProduct });

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.NewStacks.Should().BeNull();
        result.RemovedStacks.Should().ContainSingle("stack-b");
    }

    [Fact]
    public async Task Handle_SameStacks_NoChanges()
    {
        var currentProduct = CreateTestProduct(2, version: "1.0.0");
        var deployment = CreateRunningDeployment(currentProduct);
        var upgradeProduct = CreateTestProduct(2, version: "2.0.0");

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        _productSourceMock
            .Setup(s => s.GetAvailableUpgradesAsync(It.IsAny<string>(), "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { upgradeProduct });

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.NewStacks.Should().BeNull();
        result.RemovedStacks.Should().BeNull();
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidId_ReturnsFailed()
    {
        var query = new CheckProductUpgradeQuery(TestEnvironmentId, "not-a-guid");
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Handle_DeploymentNotFound_ReturnsFailed()
    {
        _repositoryMock
            .Setup(r => r.Get(It.IsAny<ProductDeploymentId>()))
            .Returns((ProductDeployment?)null);

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, Guid.NewGuid().ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeploymentNotOperational_CannotUpgrade()
    {
        var product = CreateTestProduct(1, version: "1.0.0");
        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            "1.0.0", UserId.Create(),
            new[] { new StackDeploymentConfig("s", "S", "sid", 1, new Dictionary<string, string>()) },
            new Dictionary<string, string>());

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);

        var query = new CheckProductUpgradeQuery(TestEnvironmentId, deployment.Id.Value.ToString());
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CanUpgrade.Should().BeFalse();
        result.CannotUpgradeReason.Should().Contain("Deploying");
    }

    #endregion
}

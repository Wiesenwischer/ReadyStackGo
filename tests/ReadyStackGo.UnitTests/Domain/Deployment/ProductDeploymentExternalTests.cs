using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Tests for external deployment methods: CreateFromExternalDeployment and RegisterExternalStack.
/// These methods support the "every stack always has a ProductDeployment" invariant.
/// </summary>
public class ProductDeploymentExternalTests
{
    #region CreateFromExternalDeployment

    [Fact]
    public void CreateFromExternalDeployment_WithValidData_CreatesRunningDeployment()
    {
        var id = ProductDeploymentId.NewId();
        var envId = EnvironmentId.NewId();
        var deploymentId = DeploymentId.NewId();
        var userId = UserId.NewId();

        var pd = ProductDeployment.CreateFromExternalDeployment(
            id, envId, "src:product", "src:product:1.0.0",
            "product", "Product Display", "1.0.0", userId,
            "test-deployment", "web", "Web Stack", "src:product:web",
            deploymentId, "product-web", 3);

        pd.Id.Should().Be(id);
        pd.EnvironmentId.Should().Be(envId);
        pd.ProductGroupId.Should().Be("src:product");
        pd.ProductId.Should().Be("src:product:1.0.0");
        pd.ProductName.Should().Be("product");
        pd.ProductDisplayName.Should().Be("Product Display");
        pd.ProductVersion.Should().Be("1.0.0");
        pd.DeployedBy.Should().Be(userId);
        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.IsOperational.Should().BeTrue();
        pd.IsInProgress.Should().BeFalse();
        pd.CompletedAt.Should().NotBeNull();
        pd.ContinueOnError.Should().BeTrue();
    }

    [Fact]
    public void CreateFromExternalDeployment_HasSingleRunningStack()
    {
        var deploymentId = DeploymentId.NewId();

        var pd = CreateExternalDeployment(deploymentId: deploymentId);

        pd.TotalStacks.Should().Be(1);
        pd.CompletedStacks.Should().Be(1);
        pd.FailedStacks.Should().Be(0);

        var stack = pd.Stacks.Single();
        stack.StackName.Should().Be("web");
        stack.StackDisplayName.Should().Be("Web Stack");
        stack.StackId.Should().Be("src:product:web");
        stack.DeploymentId.Should().Be(deploymentId);
        stack.DeploymentStackName.Should().Be("product-web");
        stack.Status.Should().Be(StackDeploymentStatus.Running);
        stack.ServiceCount.Should().Be(3);
        stack.Order.Should().Be(0);
    }

    [Fact]
    public void CreateFromExternalDeployment_RecordsPhaseHistory()
    {
        var pd = CreateExternalDeployment();

        pd.PhaseHistory.Should().ContainSingle()
            .Which.Message.Should().Contain("external deployment");
    }

    [Fact]
    public void CreateFromExternalDeployment_WithNullDisplayName_UsesProductName()
    {
        var pd = ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product", "src:product:1.0.0",
            "product", null, "1.0.0", UserId.NewId(),
            "test-deployment", "web", "Web", "src:product:web",
            DeploymentId.NewId(), "product-web", 1);

        pd.ProductDisplayName.Should().Be("product");
    }

    [Fact]
    public void CreateFromExternalDeployment_WithEmptyProductGroupId_Throws()
    {
        var act = () => ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "", "src:product:1.0.0",
            "product", "Product", "1.0.0", UserId.NewId(),
            "test-deployment", "web", "Web", "src:product:web",
            DeploymentId.NewId(), "product-web", 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromExternalDeployment_WithEmptyStackName_Throws()
    {
        var act = () => ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product", "src:product:1.0.0",
            "product", "Product", "1.0.0", UserId.NewId(),
            "test-deployment", "", "Web", "src:product:web",
            DeploymentId.NewId(), "product-web", 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateFromExternalDeployment_SupportsHealthSync()
    {
        var pd = CreateExternalDeployment();

        var changed = pd.SyncStackHealth("web", StackDeploymentStatus.Failed, "Crashed");

        changed.Should().BeTrue();
        pd.Stacks.Single().Status.Should().Be(StackDeploymentStatus.Failed);
    }

    #endregion

    #region RegisterExternalStack

    [Fact]
    public void RegisterExternalStack_WhenRunning_AddsStack()
    {
        var pd = CreateExternalDeployment();
        var newDeploymentId = DeploymentId.NewId();

        pd.RegisterExternalStack("db", "Database", "src:product:db",
            newDeploymentId, "product-db", 1);

        pd.TotalStacks.Should().Be(2);
        pd.CompletedStacks.Should().Be(2);

        var dbStack = pd.Stacks.First(s => s.StackName == "db");
        dbStack.Status.Should().Be(StackDeploymentStatus.Running);
        dbStack.DeploymentId.Should().Be(newDeploymentId);
        dbStack.DeploymentStackName.Should().Be("product-db");
        dbStack.ServiceCount.Should().Be(1);
    }

    [Fact]
    public void RegisterExternalStack_AssignsIncrementingOrder()
    {
        var pd = CreateExternalDeployment();

        pd.RegisterExternalStack("db", "Database", "src:product:db",
            DeploymentId.NewId(), "product-db", 1);
        pd.RegisterExternalStack("cache", "Cache", "src:product:cache",
            DeploymentId.NewId(), "product-cache", 1);

        pd.Stacks.First(s => s.StackName == "web").Order.Should().Be(0);
        pd.Stacks.First(s => s.StackName == "db").Order.Should().Be(1);
        pd.Stacks.First(s => s.StackName == "cache").Order.Should().Be(2);
    }

    [Fact]
    public void RegisterExternalStack_WhenPartiallyRunning_AddsStack()
    {
        var pd = CreateExternalDeployment();
        pd.SyncStackHealth("web", StackDeploymentStatus.Failed, "Crashed");
        pd.RecalculateProductStatus();
        // Single stack failed → RecalculateProductStatus doesn't transition (anyFailed && anyRunning required)
        // Manually force to PartiallyRunning using a two-stack setup instead:
        var pd2 = CreateTwoStackExternalDeployment();
        pd2.SyncStackHealth("web", StackDeploymentStatus.Failed, "Crashed");
        pd2.RecalculateProductStatus();
        pd2.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);

        pd2.RegisterExternalStack("cache", "Cache", "src:product:cache",
            DeploymentId.NewId(), "product-cache", 1);

        pd2.TotalStacks.Should().Be(3);
    }

    [Fact]
    public void RegisterExternalStack_WhenDeploying_Throws()
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product", "src:product:1.0.0",
            "product", "Product", "1.0.0", UserId.NewId(),
            "test-deployment",
            new List<StackDeploymentConfig>
            {
                new("web", "Web", "src:product:web", 2, new Dictionary<string, string>())
            },
            new Dictionary<string, string>());

        var act = () => pd.RegisterExternalStack("db", "Database", "src:product:db",
            DeploymentId.NewId(), "product-db", 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterExternalStack_WhenFailed_Throws()
    {
        var pd = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product", "src:product:1.0.0",
            "product", "Product", "1.0.0", UserId.NewId(),
            "test-deployment",
            new List<StackDeploymentConfig>
            {
                new("web", "Web", "src:product:web", 2, new Dictionary<string, string>())
            },
            new Dictionary<string, string>());
        pd.StartStack("web", DeploymentId.NewId());
        pd.FailStack("web", "Error");
        pd.MarkAsFailed("All failed");

        var act = () => pd.RegisterExternalStack("db", "Database", "src:product:db",
            DeploymentId.NewId(), "product-db", 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterExternalStack_DuplicateStackName_Throws()
    {
        var pd = CreateExternalDeployment();

        var act = () => pd.RegisterExternalStack("web", "Web", "src:product:web",
            DeploymentId.NewId(), "product-web", 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterExternalStack_DuplicateStackName_CaseInsensitive_Throws()
    {
        var pd = CreateExternalDeployment();

        var act = () => pd.RegisterExternalStack("WEB", "Web", "src:product:web",
            DeploymentId.NewId(), "product-web", 1);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterExternalStack_RecordsPhaseHistory()
    {
        var pd = CreateExternalDeployment();
        var initialPhaseCount = pd.PhaseHistory.Count;

        pd.RegisterExternalStack("db", "Database", "src:product:db",
            DeploymentId.NewId(), "product-db", 1);

        pd.PhaseHistory.Count.Should().BeGreaterThan(initialPhaseCount);
    }

    #endregion

    #region Helpers

    private static ProductDeployment CreateExternalDeployment(
        DeploymentId? deploymentId = null)
    {
        return ProductDeployment.CreateFromExternalDeployment(
            ProductDeploymentId.NewId(), EnvironmentId.NewId(),
            "src:product", "src:product:1.0.0",
            "product", "Product", "1.0.0", UserId.NewId(),
            "test-deployment", "web", "Web Stack", "src:product:web",
            deploymentId ?? DeploymentId.NewId(), "product-web", 3);
    }

    private static ProductDeployment CreateTwoStackExternalDeployment()
    {
        var pd = CreateExternalDeployment();
        pd.RegisterExternalStack("db", "Database", "src:product:db",
            DeploymentId.NewId(), "product-db", 1);
        return pd;
    }

    #endregion
}

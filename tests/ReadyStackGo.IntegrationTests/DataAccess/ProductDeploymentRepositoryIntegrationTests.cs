namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;
using DeploymentEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Integration tests for ProductDeploymentRepository with real SQLite database.
/// Tests verify EF Core configuration, JSON columns, owned entities, and repository queries.
/// </summary>
public class ProductDeploymentRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly ProductDeploymentRepository _repository;
    private readonly EnvironmentRepository _environmentRepository;

    public ProductDeploymentRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new ProductDeploymentRepository(_fixture.Context);
        _environmentRepository = new EnvironmentRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private (DeploymentEnvironment env, EnvironmentId envId) CreateTestEnvironment()
    {
        var envId = EnvironmentId.Create();
        var orgId = new OrganizationId(Guid.NewGuid());
        var env = DeploymentEnvironment.CreateDefault(envId, orgId, "Test Environment", "test-env");
        _environmentRepository.Add(env);
        _fixture.Context.SaveChanges();
        return (env, envId);
    }

    private static IReadOnlyList<StackDeploymentConfig> CreateStackConfigs(int count = 3)
    {
        var configs = new List<StackDeploymentConfig>();
        for (var i = 0; i < count; i++)
        {
            configs.Add(new StackDeploymentConfig(
                $"stack-{i}",
                $"Stack {i}",
                $"source:product:stack-{i}:1.0.0",
                i + 1,
                new Dictionary<string, string> { { $"VAR_{i}", $"value_{i}" } }));
        }
        return configs;
    }

    private ProductDeployment CreateTestDeployment(
        EnvironmentId envId,
        ProductDeploymentId? id = null,
        string productGroupId = "source:test-product",
        string productVersion = "1.0.0",
        IReadOnlyList<StackDeploymentConfig>? stackConfigs = null,
        IReadOnlyDictionary<string, string>? sharedVars = null)
    {
        return ProductDeployment.InitiateDeployment(
            id ?? ProductDeploymentId.Create(),
            envId,
            productGroupId,
            $"{productGroupId}:{productVersion}",
            "test-product",
            "Test Product",
            productVersion,
            new UserId(Guid.NewGuid()),
            stackConfigs ?? CreateStackConfigs(),
            sharedVars ?? new Dictionary<string, string> { { "SHARED_KEY", "shared_value" } });
    }

    // ═══════════════════════════════════════════════════════════════════
    // Add + Basic Persistence
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersistProductDeployment_WithAllScalarProperties()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pdId = ProductDeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var pd = ProductDeployment.InitiateDeployment(
            pdId, envId,
            "source:ams-project", "source:ams-project:3.1.0",
            "ams-project", "ams.project Enterprise", "3.1.0",
            userId,
            CreateStackConfigs(2),
            new Dictionary<string, string>());

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert - fresh context
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pdId);

        persisted.Should().NotBeNull();
        persisted!.EnvironmentId.Should().Be(envId);
        persisted.ProductGroupId.Should().Be("source:ams-project");
        persisted.ProductId.Should().Be("source:ams-project:3.1.0");
        persisted.ProductName.Should().Be("ams-project");
        persisted.ProductDisplayName.Should().Be("ams.project Enterprise");
        persisted.ProductVersion.Should().Be("3.1.0");
        persisted.DeployedBy.Should().Be(userId);
        persisted.Status.Should().Be(ProductDeploymentStatus.Deploying);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        persisted.CompletedAt.Should().BeNull();
        persisted.ErrorMessage.Should().BeNull();
        persisted.ContinueOnError.Should().BeTrue();
        persisted.PreviousVersion.Should().BeNull();
        persisted.LastUpgradedAt.Should().BeNull();
        persisted.UpgradeCount.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SharedVariables JSON
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersistSharedVariables_AsJson()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var sharedVars = new Dictionary<string, string>
        {
            { "DB_HOST", "localhost" },
            { "LOG_LEVEL", "info" },
            { "CULTURE", "de-DE" }
        };
        var pd = CreateTestDeployment(envId, sharedVars: sharedVars);

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.SharedVariables.Should().HaveCount(3);
        persisted.SharedVariables["DB_HOST"].Should().Be("localhost");
        persisted.SharedVariables["LOG_LEVEL"].Should().Be("info");
        persisted.SharedVariables["CULTURE"].Should().Be("de-DE");
    }

    [Fact]
    public void Add_ShouldPersistEmptySharedVariables()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId, sharedVars: new Dictionary<string, string>());

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.SharedVariables.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PhaseHistory JSON
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersistPhaseHistory_AsJson()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId);
        // InitiateDeployment already records "Deployment initiated" phase

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.PhaseHistory.Should().NotBeEmpty();
        persisted.PhaseHistory.Should().Contain(p => p.Message == "Deployment initiated");
    }

    [Fact]
    public void Update_ShouldPersistAdditionalPhaseHistory()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId);
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        var deploymentId = DeploymentId.Create();
        pd.StartStack("stack-0", deploymentId, "test-product-stack-0");
        pd.CompleteStack("stack-0");

        // Act
        _repository.Update(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.PhaseHistory.Count.Should().BeGreaterThan(1);
        persisted.PhaseHistory.Should().Contain(p => p.Message.Contains("stack-0"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stacks (Owned Entities)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersistStacks_AsOwnedEntities()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId);

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.Stacks.Should().HaveCount(3);

        var stack0 = persisted.Stacks.First(s => s.StackName == "stack-0");
        stack0.StackDisplayName.Should().Be("Stack 0");
        stack0.StackId.Should().Be("source:product:stack-0:1.0.0");
        stack0.Order.Should().Be(0);
        stack0.ServiceCount.Should().Be(1);
        stack0.Status.Should().Be(StackDeploymentStatus.Pending);
        stack0.DeploymentId.Should().BeNull();
        stack0.DeploymentStackName.Should().BeNull();
        stack0.StartedAt.Should().BeNull();
        stack0.CompletedAt.Should().BeNull();
        stack0.ErrorMessage.Should().BeNull();
        stack0.IsNewInUpgrade.Should().BeFalse();
    }

    [Fact]
    public void Add_ShouldPersistStackVariables_AsJson()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var configs = new List<StackDeploymentConfig>
        {
            new("infra", "Infrastructure", "src:infra:1.0", 3,
                new Dictionary<string, string>
                {
                    { "DB_PORT", "5432" },
                    { "CACHE_SIZE", "512" }
                })
        };
        var pd = CreateTestDeployment(envId, stackConfigs: configs);

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        var stack = persisted!.Stacks.Single();
        stack.Variables.Should().HaveCount(2);
        stack.Variables["DB_PORT"].Should().Be("5432");
        stack.Variables["CACHE_SIZE"].Should().Be("512");
    }

    [Fact]
    public void Add_ShouldPersistStack_WithEmptyVariables()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var configs = new List<StackDeploymentConfig>
        {
            new("simple", "Simple Stack", "src:simple:1.0", 1, new Dictionary<string, string>())
        };
        var pd = CreateTestDeployment(envId, stackConfigs: configs);

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.Stacks.Single().Variables.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Get with all related data
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Get_ShouldReturnProductDeployment_WithAllRelatedData()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var sharedVars = new Dictionary<string, string> { { "KEY", "value" } };
        var pd = CreateTestDeployment(envId, sharedVars: sharedVars);
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Act - fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var result = queryRepo.Get(pd.Id);

        // Assert
        result.Should().NotBeNull();
        result!.SharedVariables.Should().ContainKey("KEY");
        result.PhaseHistory.Should().NotBeEmpty();
        result.Stacks.Should().HaveCount(3);
        result.Stacks.All(s => s.Variables.Count > 0).Should().BeTrue();
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenNotFound()
    {
        var result = _repository.Get(ProductDeploymentId.Create());
        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetActiveByProductGroupId
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetActiveByProductGroupId_ShouldReturnActiveDeployment()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId, productGroupId: "source:my-product");
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var result = queryRepo.GetActiveByProductGroupId(envId, "source:my-product");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(pd.Id);
    }

    [Fact]
    public void GetActiveByProductGroupId_ShouldIgnoreRemovedDeployments()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId, productGroupId: "source:removed-product");

        // Complete all stacks so we can start removal
        foreach (var stack in pd.Stacks)
        {
            pd.StartStack(stack.StackName, DeploymentId.Create(), $"test-{stack.StackName}");
            pd.CompleteStack(stack.StackName);
        }
        // Now remove
        pd.StartRemoval();
        foreach (var stack in pd.GetStacksInRemoveOrder())
            pd.MarkStackRemoved(stack.StackName);

        pd.Status.Should().Be(ProductDeploymentStatus.Removed);

        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var result = queryRepo.GetActiveByProductGroupId(envId, "source:removed-product");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetActiveByProductGroupId_ShouldReturnMostRecent()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var older = CreateTestDeployment(envId, productGroupId: "source:multi");
        var newer = CreateTestDeployment(envId, productGroupId: "source:multi");

        _repository.Add(older);
        _fixture.Context.SaveChanges();

        // Small delay to ensure different CreatedAt
        _repository.Add(newer);
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var result = queryRepo.GetActiveByProductGroupId(envId, "source:multi");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(newer.Id);
    }

    [Fact]
    public void GetActiveByProductGroupId_ShouldFilterByEnvironment()
    {
        // Arrange
        var (_, envId1) = CreateTestEnvironment();
        var (_, envId2) = CreateTestEnvironment();

        var pd1 = CreateTestDeployment(envId1, productGroupId: "source:same-product");
        var pd2 = CreateTestDeployment(envId2, productGroupId: "source:same-product");

        _repository.Add(pd1);
        _repository.Add(pd2);
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var result = queryRepo.GetActiveByProductGroupId(envId1, "source:same-product");

        // Assert
        result.Should().NotBeNull();
        result!.EnvironmentId.Should().Be(envId1);
    }

    [Fact]
    public void GetActiveByProductGroupId_ShouldReturnNull_WhenNoMatch()
    {
        var (_, envId) = CreateTestEnvironment();
        var result = _repository.GetActiveByProductGroupId(envId, "nonexistent");
        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetByEnvironment
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetByEnvironment_ShouldReturnAll_OrderedByCreatedAtDesc()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd1 = CreateTestDeployment(envId, productGroupId: "source:prod-a");
        _repository.Add(pd1);
        _fixture.Context.SaveChanges();

        var pd2 = CreateTestDeployment(envId, productGroupId: "source:prod-b");
        _repository.Add(pd2);
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var results = queryRepo.GetByEnvironment(envId).ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].CreatedAt.Should().BeOnOrAfter(results[1].CreatedAt);
    }

    [Fact]
    public void GetByEnvironment_ShouldReturnEmpty_WhenNoDeployments()
    {
        var (_, envId) = CreateTestEnvironment();
        var results = _repository.GetByEnvironment(envId).ToList();
        results.Should().BeEmpty();
    }

    [Fact]
    public void GetByEnvironment_ShouldNotReturnOtherEnvironmentDeployments()
    {
        // Arrange
        var (_, envId1) = CreateTestEnvironment();
        var (_, envId2) = CreateTestEnvironment();

        _repository.Add(CreateTestDeployment(envId1));
        _repository.Add(CreateTestDeployment(envId2));
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var results = queryRepo.GetByEnvironment(envId1).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].EnvironmentId.Should().Be(envId1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetAllActive
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllActive_ShouldReturnDeploying()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        _repository.Add(CreateTestDeployment(envId));
        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var results = queryRepo.GetAllActive().ToList();

        // Assert - newly created is Deploying
        results.Should().HaveCount(1);
        results[0].Status.Should().Be(ProductDeploymentStatus.Deploying);
    }

    [Fact]
    public void GetAllActive_ShouldNotReturnRemovedOrFailed()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();

        // Create a deployment and mark it failed
        var failedPd = CreateTestDeployment(envId, productGroupId: "source:failed");
        failedPd.MarkAsFailed("Test failure");
        _repository.Add(failedPd);

        // Create a deployment and remove it
        var removedPd = CreateTestDeployment(envId, productGroupId: "source:removed");
        foreach (var stack in removedPd.Stacks)
        {
            removedPd.StartStack(stack.StackName, DeploymentId.Create(), $"test-{stack.StackName}");
            removedPd.CompleteStack(stack.StackName);
        }
        removedPd.StartRemoval();
        foreach (var stack in removedPd.GetStacksInRemoveOrder())
            removedPd.MarkStackRemoved(stack.StackName);
        _repository.Add(removedPd);

        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var results = queryRepo.GetAllActive().ToList();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void GetAllActive_ShouldReturnRunningAndPartiallyRunning()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();

        // Running deployment
        var runningPd = CreateTestDeployment(envId, productGroupId: "source:running");
        foreach (var stack in runningPd.Stacks)
        {
            runningPd.StartStack(stack.StackName, DeploymentId.Create(), $"test-{stack.StackName}");
            runningPd.CompleteStack(stack.StackName);
        }
        _repository.Add(runningPd);

        // PartiallyRunning deployment
        var partialPd = CreateTestDeployment(envId, productGroupId: "source:partial");
        partialPd.StartStack("stack-0", DeploymentId.Create(), "test-stack-0");
        partialPd.CompleteStack("stack-0");
        partialPd.StartStack("stack-1", DeploymentId.Create(), "test-stack-1");
        partialPd.FailStack("stack-1", "Connection timeout");
        partialPd.MarkAsPartiallyRunning("1 of 3 stacks failed");
        _repository.Add(partialPd);

        _fixture.Context.SaveChanges();

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepo = new ProductDeploymentRepository(queryContext);
        var results = queryRepo.GetAllActive().ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Status == ProductDeploymentStatus.Running);
        results.Should().Contain(r => r.Status == ProductDeploymentStatus.PartiallyRunning);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Update
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_ShouldPersistStatusAndStackChanges()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId);
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Start and complete first stack
        var deploymentId = DeploymentId.Create();
        pd.StartStack("stack-0", deploymentId, "test-product-stack-0");
        pd.CompleteStack("stack-0");
        _repository.Update(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        var stack0 = persisted!.Stacks.First(s => s.StackName == "stack-0");
        stack0.Status.Should().Be(StackDeploymentStatus.Running);
        stack0.DeploymentId.Should().Be(deploymentId);
        stack0.DeploymentStackName.Should().Be("test-product-stack-0");
        stack0.StartedAt.Should().NotBeNull();
        stack0.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_ShouldPersistFailedStackWithErrorMessage()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId);
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        pd.StartStack("stack-0", DeploymentId.Create(), "test-stack-0");
        pd.FailStack("stack-0", "Image pull failed: registry unreachable");
        _repository.Update(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        var stack0 = persisted!.Stacks.First(s => s.StackName == "stack-0");
        stack0.Status.Should().Be(StackDeploymentStatus.Failed);
        stack0.ErrorMessage.Should().Be("Image pull failed: registry unreachable");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Concurrency Token
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Version_ShouldBePersisted()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var pd = CreateTestDeployment(envId);
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);
        persisted.Should().NotBeNull();
        persisted!.Version.Should().Be(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Upgrade Deployment Persistence
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersistUpgradeDeployment_WithPreviousVersion()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var existing = CreateTestDeployment(envId, productVersion: "1.0.0");
        // Complete all stacks so it's Running
        foreach (var stack in existing.Stacks)
        {
            existing.StartStack(stack.StackName, DeploymentId.Create(), $"test-{stack.StackName}");
            existing.CompleteStack(stack.StackName);
        }
        _repository.Add(existing);
        _fixture.Context.SaveChanges();

        var upgradePd = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.Create(), envId,
            "source:test-product", "source:test-product:2.0.0",
            "test-product", "Test Product", "2.0.0",
            new UserId(Guid.NewGuid()),
            existing,
            CreateStackConfigs(3),
            new Dictionary<string, string> { { "UPGRADED", "true" } });

        // Act
        _repository.Add(upgradePd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(upgradePd.Id);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ProductDeploymentStatus.Upgrading);
        persisted.PreviousVersion.Should().Be("1.0.0");
        persisted.ProductVersion.Should().Be("2.0.0");
        persisted.UpgradeCount.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════════════
    // IsNewInUpgrade flag
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersistIsNewInUpgrade_ForNewStacks()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var existingConfigs = new List<StackDeploymentConfig>
        {
            new("infra", "Infrastructure", "src:infra:1.0", 2, new Dictionary<string, string>())
        };
        var existing = CreateTestDeployment(envId, productVersion: "1.0.0", stackConfigs: existingConfigs);
        foreach (var stack in existing.Stacks)
        {
            existing.StartStack(stack.StackName, DeploymentId.Create(), $"test-{stack.StackName}");
            existing.CompleteStack(stack.StackName);
        }
        _repository.Add(existing);
        _fixture.Context.SaveChanges();

        var targetConfigs = new List<StackDeploymentConfig>
        {
            new("infra", "Infrastructure", "src:infra:2.0", 2, new Dictionary<string, string>()),
            new("monitoring", "Monitoring", "src:monitoring:2.0", 1, new Dictionary<string, string>())
        };
        var upgradePd = ProductDeployment.InitiateUpgrade(
            ProductDeploymentId.Create(), envId,
            "source:test-product", "source:test-product:2.0.0",
            "test-product", "Test Product", "2.0.0",
            new UserId(Guid.NewGuid()),
            existing,
            targetConfigs,
            new Dictionary<string, string>());

        // Act
        _repository.Add(upgradePd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(upgradePd.Id);

        persisted.Should().NotBeNull();
        var infraStack = persisted!.Stacks.First(s => s.StackName == "infra");
        infraStack.IsNewInUpgrade.Should().BeFalse();

        var monitoringStack = persisted.Stacks.First(s => s.StackName == "monitoring");
        monitoringStack.IsNewInUpgrade.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Single-Stack Product
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Add_ShouldPersist_SingleStackProduct()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var configs = new List<StackDeploymentConfig>
        {
            new("main", "Main Stack", "src:single:1.0", 5, new Dictionary<string, string> { { "PORT", "8080" } })
        };
        var pd = CreateTestDeployment(envId, stackConfigs: configs);

        // Act
        _repository.Add(pd);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ProductDeployments.Find(pd.Id);

        persisted.Should().NotBeNull();
        persisted!.Stacks.Should().HaveCount(1);
        persisted.Stacks[0].StackName.Should().Be("main");
        persisted.Stacks[0].ServiceCount.Should().Be(5);
    }

    // ═══════════════════════════════════════════════════════════════════
    // NextIdentity
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void NextIdentity_ShouldReturnUniqueIds()
    {
        var id1 = _repository.NextIdentity();
        var id2 = _repository.NextIdentity();

        id1.Should().NotBe(id2);
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Application.UseCases.Health.GetEnvironmentHealthSummary;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Application.Health;

using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Unit tests for GetEnvironmentHealthSummaryHandler.
/// Verifies correct mapping of domain objects to DTOs.
/// </summary>
public class GetEnvironmentHealthSummaryHandlerTests
{
    private readonly Mock<IHealthSnapshotRepository> _healthSnapshotRepoMock;
    private readonly Mock<IEnvironmentRepository> _environmentRepoMock;
    private readonly Mock<ILogger<GetEnvironmentHealthSummaryHandler>> _loggerMock;
    private readonly GetEnvironmentHealthSummaryHandler _handler;

    private readonly OrganizationId _orgId = OrganizationId.NewId();
    private readonly EnvironmentId _envId = EnvironmentId.NewId();

    public GetEnvironmentHealthSummaryHandlerTests()
    {
        _healthSnapshotRepoMock = new Mock<IHealthSnapshotRepository>();
        _environmentRepoMock = new Mock<IEnvironmentRepository>();
        _loggerMock = new Mock<ILogger<GetEnvironmentHealthSummaryHandler>>();

        _handler = new GetEnvironmentHealthSummaryHandler(
            _healthSnapshotRepoMock.Object,
            _environmentRepoMock.Object,
            _loggerMock.Object);
    }

    private Environment CreateTestEnvironment(string name = "Production")
    {
        return Environment.CreateDefault(_envId, _orgId, name, "Test environment");
    }

    private HealthSnapshot CreateSnapshot(
        DeploymentId deploymentId,
        string stackName,
        HealthStatus overallStatus = null!,
        OperationMode operationMode = null!)
    {
        overallStatus ??= HealthStatus.Healthy;
        operationMode ??= OperationMode.Normal;

        var services = new[]
        {
            ServiceHealth.Create("service-1", HealthStatus.Healthy, "container-1", "container-name-1", null, null),
            ServiceHealth.Create("service-2", HealthStatus.Healthy, "container-2", "container-name-2", null, null)
        };

        var selfHealth = SelfHealth.Create(services);

        return HealthSnapshot.Capture(
            _orgId,
            _envId,
            deploymentId,
            stackName,
            operationMode,
            "1.0.0",
            null,
            null,
            null,
            selfHealth);
    }

    [Fact]
    public async Task Handle_ValidEnvironment_ReturnsDtoWithCorrectDeploymentIds()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var deploymentId1 = DeploymentId.NewId();
        var deploymentId2 = DeploymentId.NewId();

        var snapshots = new[]
        {
            CreateSnapshot(deploymentId1, "stack-1"),
            CreateSnapshot(deploymentId2, "stack-2")
        };

        _environmentRepoMock
            .Setup(r => r.Get(_envId))
            .Returns(environment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(snapshots);

        var query = new GetEnvironmentHealthSummaryQuery(_envId.Value.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Stacks.Should().HaveCount(2);

        // Verify DeploymentIds are correctly mapped (not SnapshotIds!)
        var stack1Dto = result.Data.Stacks.Single(s => s.StackName == "stack-1");
        var stack2Dto = result.Data.Stacks.Single(s => s.StackName == "stack-2");

        stack1Dto.DeploymentId.Should().Be(deploymentId1.Value.ToString());
        stack2Dto.DeploymentId.Should().Be(deploymentId2.Value.ToString());

        // Ensure DeploymentId is NOT the SnapshotId
        stack1Dto.DeploymentId.Should().NotBe(snapshots[0].Id.Value.ToString());
        stack2Dto.DeploymentId.Should().NotBe(snapshots[1].Id.Value.ToString());
    }

    [Fact]
    public async Task Handle_ValidEnvironment_MapsAllStackProperties()
    {
        // Arrange
        var environment = CreateTestEnvironment("Test Environment");
        var deploymentId = DeploymentId.NewId();
        var snapshot = CreateSnapshot(deploymentId, "test-stack", HealthStatus.Degraded, OperationMode.Migrating);

        _environmentRepoMock
            .Setup(r => r.Get(_envId))
            .Returns(environment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(new[] { snapshot });

        var query = new GetEnvironmentHealthSummaryQuery(_envId.Value.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        var stackDto = result.Data!.Stacks.Single();

        stackDto.DeploymentId.Should().Be(deploymentId.Value.ToString());
        stackDto.StackName.Should().Be("test-stack");
        stackDto.CurrentVersion.Should().Be("1.0.0");
        stackDto.OverallStatus.Should().Be("Degraded");
        stackDto.OperationMode.Should().Be("Migrating");
        stackDto.HealthyServices.Should().Be(2);
        stackDto.TotalServices.Should().Be(2);
        stackDto.RequiresAttention.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsFailure()
    {
        // Arrange
        var query = new GetEnvironmentHealthSummaryQuery("not-a-guid");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid environment ID format");
    }

    [Fact]
    public async Task Handle_EnvironmentNotFound_ReturnsFailure()
    {
        // Arrange
        _environmentRepoMock
            .Setup(r => r.Get(_envId))
            .Returns((Environment?)null);

        var query = new GetEnvironmentHealthSummaryQuery(_envId.Value.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_NoSnapshots_ReturnsEmptySummary()
    {
        // Arrange
        var environment = CreateTestEnvironment();

        _environmentRepoMock
            .Setup(r => r.Get(_envId))
            .Returns(environment);

        _healthSnapshotRepoMock
            .Setup(r => r.GetLatestForEnvironment(_envId))
            .Returns(Enumerable.Empty<HealthSnapshot>());

        var query = new GetEnvironmentHealthSummaryQuery(_envId.Value.ToString());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TotalStacks.Should().Be(0);
        result.Data.Stacks.Should().BeEmpty();
    }
}

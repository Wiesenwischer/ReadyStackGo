using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Onboarding.GetOnboardingStatus;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.StackManagement.Sources;
using DeploymentOrgId = ReadyStackGo.Domain.Deployment.OrganizationId;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

namespace ReadyStackGo.UnitTests.Application.Onboarding;

public class GetOnboardingStatusHandlerTests
{
    private readonly Mock<IOrganizationRepository> _orgRepoMock = new();
    private readonly Mock<IEnvironmentRepository> _envRepoMock = new();
    private readonly Mock<IStackSourceRepository> _sourceRepoMock = new();
    private readonly Mock<IRegistryRepository> _registryRepoMock = new();
    private readonly Mock<IOnboardingStateService> _onboardingStateMock = new();

    public GetOnboardingStatusHandlerTests()
    {
        // Default: no organization, no sources, not dismissed
        _orgRepoMock.Setup(r => r.GetAll()).Returns([]);
        _sourceRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StackSource>() as IReadOnlyList<StackSource>);
        _onboardingStateMock.Setup(s => s.IsDismissedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private GetOnboardingStatusHandler CreateHandler() => new(
        _orgRepoMock.Object,
        _envRepoMock.Object,
        _sourceRepoMock.Object,
        _registryRepoMock.Object,
        _onboardingStateMock.Object);

    private Organization SetupOrganization(string name = "Test Org")
    {
        var org = Organization.Provision(OrganizationId.NewId(), name, "Test Organization");
        _orgRepoMock.Setup(r => r.GetAll()).Returns([org]);
        return org;
    }

    private void SetupEnvironments(int count)
    {
        var environments = Enumerable.Range(0, count)
            .Select(_ =>
            {
                var envId = EnvironmentId.NewId();
                return Environment.CreateDockerSocket(envId, DeploymentOrgId.NewId(), $"Env-{envId}", null, "unix:///var/run/docker.sock");
            })
            .ToList();
        _envRepoMock.Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns(environments);
    }

    private void SetupRegistries(int count)
    {
        var registries = Enumerable.Range(0, count)
            .Select(_ => Registry.Create(
                RegistryId.NewId(),
                DeploymentOrgId.NewId(),
                $"Registry-{Guid.NewGuid():N}",
                "docker.io",
                "library/*"))
            .ToList();
        _registryRepoMock.Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns(registries);
    }

    private void SetupSources(int count)
    {
        var sources = Enumerable.Range(0, count)
            .Select(_ => StackSource.CreateGitRepository(
                StackSourceId.NewId(),
                $"source-{Guid.NewGuid():N}",
                "https://github.com/example/repo"))
            .ToList();
        _sourceRepoMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sources);
    }

    #region Fresh Install (No Organization)

    [Fact]
    public async Task Handle_NoOrganization_IsCompleteIsFalse()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoOrganization_OrganizationNotDone()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Organization.Done.Should().BeFalse();
        result.Organization.Count.Should().Be(0);
        result.Organization.Name.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoOrganization_DoesNotQueryEnvironmentsOrRegistries()
    {
        var handler = CreateHandler();

        await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        _envRepoMock.Verify(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()), Times.Never);
        _registryRepoMock.Verify(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoOrganization_EnvironmentAndRegistryCountsAreZero()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Environment.Done.Should().BeFalse();
        result.Environment.Count.Should().Be(0);
        result.Registries.Done.Should().BeFalse();
        result.Registries.Count.Should().Be(0);
    }

    #endregion

    #region Organization Exists

    [Fact]
    public async Task Handle_WithOrganization_IsCompleteIsTrue()
    {
        SetupOrganization();
        SetupEnvironments(0);
        SetupRegistries(0);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithOrganization_OrganizationDoneWithNameAndCount()
    {
        SetupOrganization("My Company");
        SetupEnvironments(0);
        SetupRegistries(0);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Organization.Done.Should().BeTrue();
        result.Organization.Count.Should().Be(1);
        result.Organization.Name.Should().Be("My Company");
    }

    [Fact]
    public async Task Handle_WithOrganizationAndEnvironments_EnvironmentDoneWithCount()
    {
        SetupOrganization();
        SetupEnvironments(3);
        SetupRegistries(0);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Environment.Done.Should().BeTrue();
        result.Environment.Count.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithOrganizationNoEnvironments_EnvironmentNotDone()
    {
        SetupOrganization();
        SetupEnvironments(0);
        SetupRegistries(0);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Environment.Done.Should().BeFalse();
        result.Environment.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithOrganizationAndRegistries_RegistriesDoneWithCount()
    {
        SetupOrganization();
        SetupEnvironments(0);
        SetupRegistries(2);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Registries.Done.Should().BeTrue();
        result.Registries.Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithOrganizationNoRegistries_RegistriesNotDone()
    {
        SetupOrganization();
        SetupEnvironments(0);
        SetupRegistries(0);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.Registries.Done.Should().BeFalse();
        result.Registries.Count.Should().Be(0);
    }

    #endregion

    #region Stack Sources

    [Fact]
    public async Task Handle_WithSources_SourcesDoneWithCount()
    {
        SetupSources(5);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.StackSources.Done.Should().BeTrue();
        result.StackSources.Count.Should().Be(5);
    }

    [Fact]
    public async Task Handle_NoSources_SourcesNotDone()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.StackSources.Done.Should().BeFalse();
        result.StackSources.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SourcesQueriedEvenWithoutOrganization()
    {
        SetupSources(2);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.StackSources.Done.Should().BeTrue();
        result.StackSources.Count.Should().Be(2);
        _sourceRepoMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Dismiss State

    [Fact]
    public async Task Handle_NotDismissed_IsDismissedFalse()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.IsDismissed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Dismissed_IsDismissedTrue()
    {
        _onboardingStateMock.Setup(s => s.IsDismissedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.IsDismissed.Should().BeTrue();
    }

    #endregion

    #region Full Setup Scenario

    [Fact]
    public async Task Handle_FullyConfigured_AllItemsDone()
    {
        SetupOrganization("Production Corp");
        SetupEnvironments(2);
        SetupRegistries(3);
        SetupSources(4);
        var handler = CreateHandler();

        var result = await handler.Handle(new GetOnboardingStatusQuery(), CancellationToken.None);

        result.IsComplete.Should().BeTrue();
        result.Organization.Done.Should().BeTrue();
        result.Organization.Name.Should().Be("Production Corp");
        result.Environment.Done.Should().BeTrue();
        result.Environment.Count.Should().Be(2);
        result.StackSources.Done.Should().BeTrue();
        result.StackSources.Count.Should().Be(4);
        result.Registries.Done.Should().BeTrue();
        result.Registries.Count.Should().Be(3);
    }

    #endregion
}

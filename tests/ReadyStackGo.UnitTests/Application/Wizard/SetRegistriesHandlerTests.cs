using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.Wizard.SetRegistries;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using DeploymentOrgId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.UnitTests.Application.Wizard;

public class SetRegistriesHandlerTests
{
    private readonly Mock<IOrganizationRepository> _orgRepoMock = new();
    private readonly Mock<IRegistryRepository> _registryRepoMock = new();
    private readonly Mock<ILogger<SetRegistriesHandler>> _loggerMock = new();

    private SetRegistriesHandler CreateHandler()
        => new(_orgRepoMock.Object, _registryRepoMock.Object, _loggerMock.Object);

    private Organization SetupOrganization()
    {
        var org = Organization.Provision(
            OrganizationId.NewId(), "Test Org", "Test Organization");
        _orgRepoMock.Setup(r => r.GetAll()).Returns([org]);
        _registryRepoMock
            .Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns([]);
        return org;
    }

    private static RegistryInput CreateInput(
        string name = "Test Registry",
        string host = "docker.io",
        string pattern = "myorg/*",
        bool requiresAuth = true,
        string? username = "user",
        string? password = "pass")
        => new(name, host, pattern, requiresAuth, username, password);

    #region Empty input

    [Fact]
    public async Task Handle_EmptyList_ReturnsSuccessWithZeroCounts()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([]), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(0);
    }

    #endregion

    #region No organization

    [Fact]
    public async Task Handle_NoOrganization_ReturnsFalse()
    {
        _orgRepoMock.Setup(r => r.GetAll()).Returns([]);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput()]), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RegistriesCreated.Should().Be(0);
    }

    #endregion

    #region Successful creation

    [Fact]
    public async Task Handle_ValidInput_CreatesRegistry()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(name: "My Registry", host: "ghcr.io", pattern: "ghcr.io/myorg/*")]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistriesCreated.Should().Be(1);

        _registryRepoMock.Verify(
            r => r.Add(It.Is<Registry>(reg =>
                reg.Name == "My Registry" &&
                reg.Url.Contains("ghcr.io") &&
                reg.Username == "user" &&
                reg.Password == "pass" &&
                reg.ImagePatterns.Contains("ghcr.io/myorg/*"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleInputs_CreatesAll()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var inputs = new[]
        {
            CreateInput(name: "Registry 1", host: "ghcr.io", pattern: "ghcr.io/org1/*"),
            CreateInput(name: "Registry 2", host: "docker.io", pattern: "myorg/*")
        };

        var result = await handler.Handle(
            new SetRegistriesCommand(inputs), CancellationToken.None);

        result.RegistriesCreated.Should().Be(2);
        _registryRepoMock.Verify(r => r.Add(It.IsAny<Registry>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_SaveChangesCalledOnce()
    {
        SetupOrganization();
        var handler = CreateHandler();

        await handler.Handle(
            new SetRegistriesCommand([CreateInput()]), CancellationToken.None);

        _registryRepoMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_HostNormalized_AddsHttpsPrefix()
    {
        SetupOrganization();
        var handler = CreateHandler();

        await handler.Handle(
            new SetRegistriesCommand([CreateInput(host: "ghcr.io")]),
            CancellationToken.None);

        _registryRepoMock.Verify(
            r => r.Add(It.Is<Registry>(reg => reg.Url.StartsWith("https://"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HostWithHttps_NotDoubled()
    {
        SetupOrganization();
        var handler = CreateHandler();

        await handler.Handle(
            new SetRegistriesCommand([CreateInput(host: "https://ghcr.io")]),
            CancellationToken.None);

        _registryRepoMock.Verify(
            r => r.Add(It.Is<Registry>(reg => !reg.Url.Contains("https://https://"))),
            Times.Once);
    }

    #endregion

    #region Public registries — skipped

    [Fact]
    public async Task Handle_PublicRegistry_Skipped()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(requiresAuth: false)]),
            CancellationToken.None);

        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(1);
        _registryRepoMock.Verify(r => r.Add(It.IsAny<Registry>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MixedPublicAndPrivate_OnlyPrivateCreated()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var inputs = new[]
        {
            CreateInput(name: "Public", requiresAuth: false),
            CreateInput(name: "Private", pattern: "myorg/*", requiresAuth: true)
        };

        var result = await handler.Handle(
            new SetRegistriesCommand(inputs), CancellationToken.None);

        result.RegistriesCreated.Should().Be(1);
        result.RegistriesSkipped.Should().Be(1);
    }

    #endregion

    #region Missing credentials — skipped

    [Fact]
    public async Task Handle_AuthRequiredButNoUsername_Skipped()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(username: null)]),
            CancellationToken.None);

        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AuthRequiredButNoPassword_Skipped()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(password: null)]),
            CancellationToken.None);

        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AuthRequiredButEmptyUsername_Skipped()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(username: "  ")]),
            CancellationToken.None);

        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(1);
    }

    #endregion

    #region Duplicate detection

    [Fact]
    public async Task Handle_MatchingPatternExists_Skipped()
    {
        var org = SetupOrganization();

        var existing = Registry.Create(
            RegistryId.NewId(),
            DeploymentOrgId.FromIdentityAccess(org.Id),
            "Existing",
            "https://ghcr.io",
            "user", "pass");
        existing.SetImagePatterns(["ghcr.io/myorg/*"]);

        _registryRepoMock
            .Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns([existing]);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(pattern: "ghcr.io/myorg/*")]),
            CancellationToken.None);

        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PatternMatchingIsCaseInsensitive()
    {
        var org = SetupOrganization();

        var existing = Registry.Create(
            RegistryId.NewId(),
            DeploymentOrgId.FromIdentityAccess(org.Id),
            "Existing",
            "https://ghcr.io",
            "user", "pass");
        existing.SetImagePatterns(["GHCR.IO/MYORG/*"]);

        _registryRepoMock
            .Setup(r => r.GetByOrganization(It.IsAny<DeploymentOrgId>()))
            .Returns([existing]);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new SetRegistriesCommand([CreateInput(pattern: "ghcr.io/myorg/*")]),
            CancellationToken.None);

        result.RegistriesCreated.Should().Be(0);
        result.RegistriesSkipped.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateWithinBatch_SecondSkipped()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var inputs = new[]
        {
            CreateInput(name: "First", pattern: "myorg/*"),
            CreateInput(name: "Second", pattern: "myorg/*")
        };

        var result = await handler.Handle(
            new SetRegistriesCommand(inputs), CancellationToken.None);

        result.RegistriesCreated.Should().Be(1);
        result.RegistriesSkipped.Should().Be(1);
    }

    #endregion

    #region Mixed scenarios

    [Fact]
    public async Task Handle_MixedValidInvalidDuplicate_CorrectCounts()
    {
        SetupOrganization();
        var handler = CreateHandler();

        var inputs = new[]
        {
            CreateInput(name: "Valid", pattern: "org1/*"),
            CreateInput(name: "Public", requiresAuth: false),
            CreateInput(name: "No Creds", pattern: "org2/*", username: null),
            CreateInput(name: "Duplicate", pattern: "org1/*")
        };

        var result = await handler.Handle(
            new SetRegistriesCommand(inputs), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RegistriesCreated.Should().Be(1);
        result.RegistriesSkipped.Should().Be(3);
    }

    #endregion
}

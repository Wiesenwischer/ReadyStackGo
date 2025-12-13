using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Unit tests for DeploymentPrerequisiteValidationService domain service.
/// v0.12: Uses DTOs (UserValidationContext, OrganizationValidationContext) instead of direct repository access.
/// </summary>
public class DeploymentPrerequisiteValidationServiceTests
{
    private readonly Mock<IEnvironmentRepository> _environmentRepositoryMock;
    private readonly DeploymentPrerequisiteValidationService _sut;

    public DeploymentPrerequisiteValidationServiceTests()
    {
        _environmentRepositoryMock = new Mock<IEnvironmentRepository>();
        _sut = new DeploymentPrerequisiteValidationService(_environmentRepositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullEnvironmentRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeploymentPrerequisiteValidationService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("environmentRepository");
    }

    #endregion

    #region Validate - Success Tests

    [Fact]
    public void Validate_WithAllPrerequisitesMet_ReturnsValidResult()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithSystemAdmin_BypassesOrgMembershipCheck()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateSystemAdminContext(); // Not a member but is sysadmin
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeTrue();
        result.HasError(PrerequisiteErrorType.UserNotAuthorized).Should().BeFalse();
    }

    #endregion

    #region Validate - Environment Tests

    [Fact]
    public void Validate_WithNonExistentEnvironment_ReturnsEnvironmentNotFoundError()
    {
        // Arrange
        var envId = EnvironmentId.NewId();
        var userContext = CreateTestUserContext();
        var organizationContext = CreateActiveOrganizationContext(DeploymentOrganizationId.NewId());
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string>();

        _environmentRepositoryMock.Setup(r => r.Get(envId)).Returns((Environment?)null);

        // Act
        var result = _sut.Validate(envId, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.EnvironmentNotFound).Should().BeTrue();
    }

    #endregion

    #region Validate - Organization Tests

    [Fact]
    public void Validate_WithNullOrganizationContext_ReturnsOrganizationNotFoundError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var userContext = CreateTestUserContext();
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string>();

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.OrganizationNotFound).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInactiveOrganization_ReturnsOrganizationInactiveError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateInactiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.OrganizationInactive).Should().BeTrue();
    }

    #endregion

    #region Validate - User Tests

    [Fact]
    public void Validate_WithDisabledUser_ReturnsUserDisabledError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateDisabledUserContext(environment.OrganizationId);
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.UserDisabled).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnauthorizedUser_ReturnsUserNotAuthorizedError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateTestUserContext(); // No org membership
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.UserNotAuthorized).Should().BeTrue();
    }

    #endregion

    #region Validate - Variable Tests

    [Fact]
    public void Validate_WithMissingRequiredVariable_ReturnsRequiredVariableMissingError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateStackInfoWithRequiredVariables();
        var variables = new Dictionary<string, string>(); // Missing required variable

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.RequiredVariableMissing).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyRequiredVariable_ReturnsRequiredVariableMissingError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateStackInfoWithRequiredVariables();
        var variables = new Dictionary<string, string> { { "DB_HOST", "" } }; // Empty value

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.RequiredVariableMissing).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidVariableValue_ReturnsVariableValidationFailedError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateStackInfoWithPortVariable();
        var variables = new Dictionary<string, string> { { "PORT", "invalid" } }; // Invalid port

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.VariableValidationFailed).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOptionalVariablesNotProvided_ReturnsValidResult()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateStackInfoWithOptionalVariables();
        var variables = new Dictionary<string, string>(); // Optional variables not provided

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Validate - Stack Tests

    [Fact]
    public void Validate_WithNoServices_ReturnsNoServicesInStackError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateAuthorizedUserContext(environment.OrganizationId);
        var stack = CreateStackInfoWithNoServices();
        var variables = new Dictionary<string, string>();

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.NoServicesInStack).Should().BeTrue();
    }

    #endregion

    #region Validate - Multiple Errors Tests

    [Fact]
    public void Validate_WithMultipleIssues_ReturnsAllErrors()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateInactiveOrganizationContext(environment.OrganizationId);
        var userContext = CreateDisabledUserContext(environment.OrganizationId);
        var stack = CreateStackInfoWithRequiredVariables();
        var variables = new Dictionary<string, string>(); // Missing variables

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, userContext, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }

    #endregion

    #region ValidateEnvironment Tests

    [Fact]
    public void ValidateEnvironment_WithValidEnvironment_ReturnsValidResult()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateActiveOrganizationContext(environment.OrganizationId);

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.ValidateEnvironment(environment.Id, organizationContext);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEnvironment_WithNonExistentEnvironment_ReturnsError()
    {
        // Arrange
        var envId = EnvironmentId.NewId();
        var organizationContext = CreateActiveOrganizationContext(DeploymentOrganizationId.NewId());
        _environmentRepositoryMock.Setup(r => r.Get(envId)).Returns((Environment?)null);

        // Act
        var result = _sut.ValidateEnvironment(envId, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.EnvironmentNotFound).Should().BeTrue();
    }

    [Fact]
    public void ValidateEnvironment_WithInactiveOrganization_ReturnsError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organizationContext = CreateInactiveOrganizationContext(environment.OrganizationId);

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);

        // Act
        var result = _sut.ValidateEnvironment(environment.Id, organizationContext);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.OrganizationInactive).Should().BeTrue();
    }

    #endregion

    #region DeploymentPrerequisiteResult Tests

    [Fact]
    public void GetErrorsByType_ReturnsOnlyMatchingErrors()
    {
        // Arrange
        var errors = new List<PrerequisiteError>
        {
            new(PrerequisiteErrorType.UserDisabled, "User is disabled"),
            new(PrerequisiteErrorType.RequiredVariableMissing, "Missing var 1"),
            new(PrerequisiteErrorType.RequiredVariableMissing, "Missing var 2"),
            new(PrerequisiteErrorType.OrganizationInactive, "Org inactive")
        };
        var result = new DeploymentPrerequisiteResult(false, errors);

        // Act
        var missingVarErrors = result.GetErrorsByType(PrerequisiteErrorType.RequiredVariableMissing).ToList();

        // Assert
        missingVarErrors.Should().HaveCount(2);
        missingVarErrors.Should().OnlyContain(e => e.Type == PrerequisiteErrorType.RequiredVariableMissing);
    }

    [Fact]
    public void HasError_WithMatchingType_ReturnsTrue()
    {
        // Arrange
        var errors = new List<PrerequisiteError>
        {
            new(PrerequisiteErrorType.UserDisabled, "User is disabled")
        };
        var result = new DeploymentPrerequisiteResult(false, errors);

        // Act & Assert
        result.HasError(PrerequisiteErrorType.UserDisabled).Should().BeTrue();
        result.HasError(PrerequisiteErrorType.OrganizationInactive).Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static Environment CreateTestEnvironment()
    {
        return Environment.CreateDefault(
            EnvironmentId.NewId(),
            DeploymentOrganizationId.NewId(),
            "Test Environment",
            "Test environment for unit tests");
    }

    private static OrganizationValidationContext CreateActiveOrganizationContext(DeploymentOrganizationId orgId)
    {
        return new OrganizationValidationContext(orgId.Value, "Test Organization", IsActive: true);
    }

    private static OrganizationValidationContext CreateInactiveOrganizationContext(DeploymentOrganizationId orgId)
    {
        return new OrganizationValidationContext(orgId.Value, "Test Organization", IsActive: false);
    }

    private static UserValidationContext CreateTestUserContext()
    {
        return new UserValidationContext(
            Guid.NewGuid(),
            IsEnabled: true,
            IsSystemAdmin: false,
            OrganizationMemberships: new HashSet<Guid>());
    }

    private static UserValidationContext CreateAuthorizedUserContext(DeploymentOrganizationId orgId)
    {
        return new UserValidationContext(
            Guid.NewGuid(),
            IsEnabled: true,
            IsSystemAdmin: false,
            OrganizationMemberships: new HashSet<Guid> { orgId.Value });
    }

    private static UserValidationContext CreateSystemAdminContext()
    {
        return new UserValidationContext(
            Guid.NewGuid(),
            IsEnabled: true,
            IsSystemAdmin: true,
            OrganizationMemberships: new HashSet<Guid>());
    }

    private static UserValidationContext CreateDisabledUserContext(DeploymentOrganizationId orgId)
    {
        return new UserValidationContext(
            Guid.NewGuid(),
            IsEnabled: false,
            IsSystemAdmin: false,
            OrganizationMemberships: new HashSet<Guid> { orgId.Value });
    }

    private static StackValidationInfo CreateTestStackInfo()
    {
        return new StackValidationInfo
        {
            StackId = "local:test-stack",
            RequiredVariables = [],
            Variables =
            [
                new VariableValidationInfo
                {
                    Name = "DB_HOST",
                    Label = "Database Host",
                    IsRequired = false,
                    Validate = _ => []
                }
            ],
            ServiceNames = ["web"]
        };
    }

    private static StackValidationInfo CreateStackInfoWithRequiredVariables()
    {
        return new StackValidationInfo
        {
            StackId = "local:test-stack",
            RequiredVariables = [new RequiredVariableInfo("DB_HOST", "Database Host")],
            Variables =
            [
                new VariableValidationInfo
                {
                    Name = "DB_HOST",
                    Label = "Database Host",
                    IsRequired = true,
                    Validate = _ => []
                }
            ],
            ServiceNames = ["web"]
        };
    }

    private static StackValidationInfo CreateStackInfoWithOptionalVariables()
    {
        return new StackValidationInfo
        {
            StackId = "local:test-stack",
            RequiredVariables = [],
            Variables =
            [
                new VariableValidationInfo
                {
                    Name = "LOG_LEVEL",
                    Label = "Log Level",
                    IsRequired = false,
                    Validate = _ => []
                }
            ],
            ServiceNames = ["web"]
        };
    }

    private static StackValidationInfo CreateStackInfoWithPortVariable()
    {
        return new StackValidationInfo
        {
            StackId = "local:test-stack",
            RequiredVariables = [],
            Variables =
            [
                new VariableValidationInfo
                {
                    Name = "PORT",
                    Label = "Port",
                    IsRequired = false,
                    Validate = value =>
                    {
                        if (string.IsNullOrWhiteSpace(value))
                            return [];

                        if (!int.TryParse(value, out var port))
                            return ["Port must be a valid number."];

                        if (port < 1 || port > 65535)
                            return ["Port must be between 1 and 65535."];

                        return [];
                    }
                }
            ],
            ServiceNames = ["web"]
        };
    }

    private static StackValidationInfo CreateStackInfoWithNoServices()
    {
        return new StackValidationInfo
        {
            StackId = "local:test-stack",
            RequiredVariables = [],
            Variables = [],
            ServiceNames = []
        };
    }

    #endregion
}

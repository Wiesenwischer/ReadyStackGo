using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Unit tests for DeploymentPrerequisiteValidator domain service.
/// </summary>
public class DeploymentPrerequisiteValidatorTests
{
    private readonly Mock<IEnvironmentRepository> _environmentRepositoryMock;
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock;
    private readonly DeploymentPrerequisiteValidator _sut;

    public DeploymentPrerequisiteValidatorTests()
    {
        _environmentRepositoryMock = new Mock<IEnvironmentRepository>();
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _sut = new DeploymentPrerequisiteValidator(
            _environmentRepositoryMock.Object,
            _organizationRepositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullEnvironmentRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeploymentPrerequisiteValidator(null!, _organizationRepositoryMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("environmentRepository");
    }

    [Fact]
    public void Constructor_WithNullOrganizationRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DeploymentPrerequisiteValidator(_environmentRepositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("organizationRepository");
    }

    #endregion

    #region Validate - Success Tests

    [Fact]
    public void Validate_WithAllPrerequisitesMet_ReturnsValidResult()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithSystemAdmin_BypassesOrgMembershipCheck()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateSystemAdmin(); // Not a member but is sysadmin
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

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
        var user = CreateTestUser();
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string>();

        _environmentRepositoryMock.Setup(r => r.Get(envId)).Returns((Environment?)null);

        // Act
        var result = _sut.Validate(envId, stack, variables, user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.EnvironmentNotFound).Should().BeTrue();
    }

    #endregion

    #region Validate - Organization Tests

    [Fact]
    public void Validate_WithNonExistentOrganization_ReturnsOrganizationNotFoundError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var user = CreateTestUser();
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string>();

        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);
        _organizationRepositoryMock.Setup(r => r.Get(environment.OrganizationId)).Returns((Organization?)null);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.OrganizationNotFound).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInactiveOrganization_ReturnsOrganizationInactiveError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateInactiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

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
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateDisabledUser(organization.Id);
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.UserDisabled).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnauthorizedUser_ReturnsUserNotAuthorizedError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateTestUser(); // No org membership
        var stack = CreateTestStackInfo();
        var variables = new Dictionary<string, string> { { "DB_HOST", "localhost" } };

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

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
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateStackInfoWithRequiredVariables();
        var variables = new Dictionary<string, string>(); // Missing required variable

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.RequiredVariableMissing).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyRequiredVariable_ReturnsRequiredVariableMissingError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateStackInfoWithRequiredVariables();
        var variables = new Dictionary<string, string> { { "DB_HOST", "" } }; // Empty value

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.RequiredVariableMissing).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidVariableValue_ReturnsVariableValidationFailedError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateStackInfoWithPortVariable();
        var variables = new Dictionary<string, string> { { "PORT", "invalid" } }; // Invalid port

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.VariableValidationFailed).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOptionalVariablesNotProvided_ReturnsValidResult()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateStackInfoWithOptionalVariables();
        var variables = new Dictionary<string, string>(); // Optional variables not provided

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

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
        var organization = CreateActiveOrganization(environment.OrganizationId);
        var user = CreateAuthorizedUser(organization.Id);
        var stack = CreateStackInfoWithNoServices();
        var variables = new Dictionary<string, string>();

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

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
        var organization = CreateInactiveOrganization(environment.OrganizationId);
        var user = CreateDisabledUser(organization.Id); // Wrong org
        var stack = CreateStackInfoWithRequiredVariables();
        var variables = new Dictionary<string, string>(); // Missing variables

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.Validate(environment.Id, stack, variables, user);

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
        var organization = CreateActiveOrganization(environment.OrganizationId);

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.ValidateEnvironment(environment.Id);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEnvironment_WithNonExistentEnvironment_ReturnsError()
    {
        // Arrange
        var envId = EnvironmentId.NewId();
        _environmentRepositoryMock.Setup(r => r.Get(envId)).Returns((Environment?)null);

        // Act
        var result = _sut.ValidateEnvironment(envId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.HasError(PrerequisiteErrorType.EnvironmentNotFound).Should().BeTrue();
    }

    [Fact]
    public void ValidateEnvironment_WithInactiveOrganization_ReturnsError()
    {
        // Arrange
        var environment = CreateTestEnvironment();
        var organization = CreateInactiveOrganization(environment.OrganizationId);

        SetupRepositories(environment, organization);

        // Act
        var result = _sut.ValidateEnvironment(environment.Id);

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

    private void SetupRepositories(Environment environment, Organization organization)
    {
        _environmentRepositoryMock.Setup(r => r.Get(environment.Id)).Returns(environment);
        _organizationRepositoryMock.Setup(r => r.Get(environment.OrganizationId)).Returns(organization);
    }

    private static Environment CreateTestEnvironment()
    {
        return Environment.CreateDefault(
            EnvironmentId.NewId(),
            OrganizationId.NewId(),
            "Test Environment",
            "Test environment for unit tests");
    }

    private static Organization CreateActiveOrganization(OrganizationId orgId)
    {
        var org = Organization.Provision(orgId, "Test Organization", "A test organization");
        org.Activate();
        return org;
    }

    private static Organization CreateInactiveOrganization(OrganizationId orgId)
    {
        return Organization.Provision(orgId, "Test Organization", "A test organization");
    }

    private static User CreateTestUser()
    {
        return User.Register(
            UserId.NewId(),
            "testuser",
            new EmailAddress("test@example.com"),
            HashedPassword.FromHash("hashed"));
    }

    private static User CreateAuthorizedUser(OrganizationId orgId)
    {
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, orgId.Value.ToString()));
        return user;
    }

    private static User CreateSystemAdmin()
    {
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        return user;
    }

    private static User CreateDisabledUser(OrganizationId orgId)
    {
        var user = CreateAuthorizedUser(orgId);
        user.Disable();
        return user;
    }

    /// <summary>
    /// Creates a StackValidationInfo with optional variable that has default value.
    /// </summary>
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

    /// <summary>
    /// Creates a StackValidationInfo with required variable (no default).
    /// </summary>
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

    /// <summary>
    /// Creates a StackValidationInfo with optional variables.
    /// </summary>
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

    /// <summary>
    /// Creates a StackValidationInfo with a port variable that validates numeric input.
    /// </summary>
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

    /// <summary>
    /// Creates a StackValidationInfo with no services.
    /// </summary>
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

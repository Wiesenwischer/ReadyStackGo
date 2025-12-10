namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// Domain service that validates all prerequisites before a deployment can start.
/// Enforces business rules about what conditions must be met for deployment.
/// </summary>
public class DeploymentPrerequisiteValidator
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IOrganizationRepository _organizationRepository;

    public DeploymentPrerequisiteValidator(
        IEnvironmentRepository environmentRepository,
        IOrganizationRepository organizationRepository)
    {
        _environmentRepository = environmentRepository ?? throw new ArgumentNullException(nameof(environmentRepository));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
    }

    /// <summary>
    /// Validates all prerequisites for a deployment.
    /// </summary>
    /// <param name="environmentId">The target environment</param>
    /// <param name="stackInfo">Stack validation info (DTO from StackManagement domain)</param>
    /// <param name="providedVariables">Variables provided by the user</param>
    /// <param name="user">The user initiating the deployment</param>
    /// <returns>Validation result with any errors</returns>
    public DeploymentPrerequisiteResult Validate(
        EnvironmentId environmentId,
        StackValidationInfo stackInfo,
        IDictionary<string, string> providedVariables,
        User user)
    {
        ArgumentNullException.ThrowIfNull(environmentId);
        ArgumentNullException.ThrowIfNull(stackInfo);
        ArgumentNullException.ThrowIfNull(providedVariables);
        ArgumentNullException.ThrowIfNull(user);

        var errors = new List<PrerequisiteError>();

        // 1. Validate environment exists
        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.EnvironmentNotFound,
                $"Environment with ID '{environmentId}' was not found."));
            return new DeploymentPrerequisiteResult(false, errors);
        }

        // 2. Validate organization exists and is active
        var organization = _organizationRepository.Get(environment.OrganizationId);
        if (organization == null)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationNotFound,
                $"Organization with ID '{environment.OrganizationId}' was not found."));
            return new DeploymentPrerequisiteResult(false, errors);
        }

        if (!organization.Active)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationInactive,
                $"Organization '{organization.Name}' is not active."));
        }

        // 3. Validate user is enabled
        if (!user.Enablement.IsEnabled)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.UserDisabled,
                "Your account is disabled."));
        }

        // 4. Validate user has access to the organization
        if (!user.IsSystemAdmin() && !user.IsMemberOfOrganization(organization.Id.Value.ToString()))
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.UserNotAuthorized,
                $"You do not have access to organization '{organization.Name}'."));
        }

        // 5. Validate required variables are provided
        foreach (var variable in stackInfo.RequiredVariables)
        {
            if (!providedVariables.TryGetValue(variable.Name, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new PrerequisiteError(
                    PrerequisiteErrorType.RequiredVariableMissing,
                    $"Required variable '{variable.Label ?? variable.Name}' is not provided."));
            }
        }

        // 6. Validate all provided variables against their constraints
        foreach (var variable in stackInfo.Variables)
        {
            if (providedVariables.TryGetValue(variable.Name, out var value))
            {
                var validationErrors = variable.Validate(value);
                foreach (var error in validationErrors)
                {
                    errors.Add(new PrerequisiteError(
                        PrerequisiteErrorType.VariableValidationFailed,
                        error));
                }
            }
        }

        // 7. Validate stack has at least one service
        if (!stackInfo.HasServices)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.NoServicesInStack,
                "Stack definition contains no services."));
        }

        return new DeploymentPrerequisiteResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates that an environment is ready for deployments.
    /// </summary>
    public DeploymentPrerequisiteResult ValidateEnvironment(EnvironmentId environmentId)
    {
        ArgumentNullException.ThrowIfNull(environmentId);

        var errors = new List<PrerequisiteError>();

        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.EnvironmentNotFound,
                $"Environment with ID '{environmentId}' was not found."));
            return new DeploymentPrerequisiteResult(false, errors);
        }

        var organization = _organizationRepository.Get(environment.OrganizationId);
        if (organization == null)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationNotFound,
                $"Organization with ID '{environment.OrganizationId}' was not found."));
        }
        else if (!organization.Active)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationInactive,
                $"Organization '{organization.Name}' is not active."));
        }

        return new DeploymentPrerequisiteResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// Result of deployment prerequisite validation.
/// </summary>
public record DeploymentPrerequisiteResult(
    bool IsValid,
    IReadOnlyList<PrerequisiteError> Errors)
{
    /// <summary>
    /// Gets all errors of a specific type.
    /// </summary>
    public IEnumerable<PrerequisiteError> GetErrorsByType(PrerequisiteErrorType type)
    {
        return Errors.Where(e => e.Type == type);
    }

    /// <summary>
    /// Checks if there are any errors of a specific type.
    /// </summary>
    public bool HasError(PrerequisiteErrorType type)
    {
        return Errors.Any(e => e.Type == type);
    }
}

/// <summary>
/// A specific prerequisite validation error.
/// </summary>
public record PrerequisiteError(
    PrerequisiteErrorType Type,
    string Message);

/// <summary>
/// Types of prerequisite validation errors.
/// </summary>
public enum PrerequisiteErrorType
{
    EnvironmentNotFound,
    OrganizationNotFound,
    OrganizationInactive,
    UserDisabled,
    UserNotAuthorized,
    RequiredVariableMissing,
    VariableValidationFailed,
    NoServicesInStack
}

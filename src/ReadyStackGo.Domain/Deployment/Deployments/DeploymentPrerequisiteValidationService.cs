namespace ReadyStackGo.Domain.Deployment.Deployments;

using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Domain service that validates all prerequisites before a deployment can start.
/// Enforces business rules about what conditions must be met for deployment.
///
/// NOTE: This validator works with DTOs provided by the Application Layer.
/// The Application Layer acts as Anti-Corruption Layer and resolves IdentityAccess data.
/// </summary>
public class DeploymentPrerequisiteValidationService
{
    private readonly IEnvironmentRepository _environmentRepository;

    public DeploymentPrerequisiteValidationService(IEnvironmentRepository environmentRepository)
    {
        _environmentRepository = environmentRepository ?? throw new ArgumentNullException(nameof(environmentRepository));
    }

    /// <summary>
    /// Validates all prerequisites for a deployment.
    /// </summary>
    /// <param name="environmentId">The target environment</param>
    /// <param name="stackInfo">Stack validation info (DTO from StackManagement domain)</param>
    /// <param name="providedVariables">Variables provided by the user</param>
    /// <param name="userContext">User context DTO (resolved by Application Layer)</param>
    /// <param name="organizationContext">Organization context DTO (resolved by Application Layer)</param>
    /// <returns>Validation result with any errors</returns>
    public DeploymentPrerequisiteResult Validate(
        EnvironmentId environmentId,
        StackValidationInfo stackInfo,
        IDictionary<string, string> providedVariables,
        UserValidationContext userContext,
        OrganizationValidationContext? organizationContext)
    {
        ArgumentNullException.ThrowIfNull(environmentId);
        ArgumentNullException.ThrowIfNull(stackInfo);
        ArgumentNullException.ThrowIfNull(providedVariables);
        ArgumentNullException.ThrowIfNull(userContext);

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

        // 2. Validate organization (context provided by Application Layer)
        if (organizationContext == null)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationNotFound,
                $"Organization with ID '{environment.OrganizationId}' was not found."));
            return new DeploymentPrerequisiteResult(false, errors);
        }

        if (!organizationContext.IsActive)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationInactive,
                $"Organization '{organizationContext.Name}' is not active."));
        }

        // 3. Validate user is enabled
        if (!userContext.IsEnabled)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.UserDisabled,
                "Your account is disabled."));
        }

        // 4. Validate user has access to the organization
        if (!userContext.IsSystemAdmin && !userContext.HasAccessToOrganization(environment.OrganizationId))
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.UserNotAuthorized,
                $"You do not have access to organization '{organizationContext.Name}'."));
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
    public DeploymentPrerequisiteResult ValidateEnvironment(
        EnvironmentId environmentId,
        OrganizationValidationContext? organizationContext)
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

        if (organizationContext == null)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationNotFound,
                $"Organization with ID '{environment.OrganizationId}' was not found."));
        }
        else if (!organizationContext.IsActive)
        {
            errors.Add(new PrerequisiteError(
                PrerequisiteErrorType.OrganizationInactive,
                $"Organization '{organizationContext.Name}' is not active."));
        }

        return new DeploymentPrerequisiteResult(errors.Count == 0, errors);
    }
}

/// <summary>
/// DTO with user information for validation.
/// Provided by Application Layer from IdentityAccess context.
/// </summary>
public record UserValidationContext(
    Guid UserId,
    bool IsEnabled,
    bool IsSystemAdmin,
    IReadOnlySet<Guid> OrganizationMemberships)
{
    public bool HasAccessToOrganization(OrganizationId organizationId)
    {
        return IsSystemAdmin || OrganizationMemberships.Contains(organizationId.Value);
    }
}

/// <summary>
/// DTO with organization information for validation.
/// Provided by Application Layer from IdentityAccess context.
/// </summary>
public record OrganizationValidationContext(
    Guid OrganizationId,
    string Name,
    bool IsActive);

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

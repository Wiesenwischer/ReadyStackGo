namespace ReadyStackGo.Api.Authorization;

/// <summary>
/// Marks an endpoint as requiring a specific permission.
/// Used by RbacPreProcessor to enforce authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// The resource (e.g., "Deployments", "Users", "Environments").
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// The action (e.g., "Create", "Read", "Update", "Delete").
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// Creates a new permission requirement.
    /// </summary>
    /// <param name="resource">The resource name.</param>
    /// <param name="action">The action name.</param>
    public RequirePermissionAttribute(string resource, string action)
    {
        Resource = resource;
        Action = action;
    }
}

/// <summary>
/// Marks an endpoint as requiring SystemAdmin role.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequireSystemAdminAttribute : Attribute
{
}

/// <summary>
/// Marks an endpoint as requiring OrganizationOwner role for the target organization.
/// The organization ID is extracted from the request.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequireOrganizationOwnerAttribute : Attribute
{
    /// <summary>
    /// The name of the request property containing the organization ID.
    /// Default is "OrganizationId".
    /// </summary>
    public string OrganizationIdProperty { get; set; } = "OrganizationId";
}

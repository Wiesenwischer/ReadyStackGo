using System.Security.Claims;
using System.Text.Json;
using ReadyStackGo.Domain.IdentityAccess.Roles;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Service for evaluating RBAC permissions from JWT claims.
/// </summary>
public interface IRbacService
{
    /// <summary>
    /// Checks if the user has the required permission for the given scope.
    /// </summary>
    /// <param name="user">The ClaimsPrincipal from the JWT token.</param>
    /// <param name="permission">The required permission (e.g., "Deployments.Create").</param>
    /// <param name="organizationId">Optional organization scope.</param>
    /// <param name="environmentId">Optional environment scope.</param>
    bool HasPermission(ClaimsPrincipal user, Permission permission, string? organizationId = null, string? environmentId = null);

    /// <summary>
    /// Checks if the user has any of the specified roles.
    /// </summary>
    bool HasRole(ClaimsPrincipal user, params RoleId[] roleIds);

    /// <summary>
    /// Checks if the user is a SystemAdmin.
    /// </summary>
    bool IsSystemAdmin(ClaimsPrincipal user);

    /// <summary>
    /// Checks if the user is an OrganizationOwner for the specified organization.
    /// </summary>
    bool IsOrganizationOwner(ClaimsPrincipal user, string organizationId);

    /// <summary>
    /// Gets the user ID from claims.
    /// </summary>
    string? GetUserId(ClaimsPrincipal user);

    /// <summary>
    /// Gets all role assignments from claims.
    /// </summary>
    IReadOnlyList<RoleAssignmentClaim> GetRoleAssignments(ClaimsPrincipal user);
}

public class RbacService : IRbacService
{
    public bool HasPermission(ClaimsPrincipal user, Permission permission, string? organizationId = null, string? environmentId = null)
    {
        var roleAssignments = GetRoleAssignments(user);

        foreach (var assignment in roleAssignments)
        {
            var role = GetRoleFromAssignment(assignment);
            if (role == null) continue;

            // Check if role has the permission
            if (!role.HasPermission(permission)) continue;

            // Check scope
            if (IsAssignmentValidForScope(assignment, organizationId, environmentId))
            {
                return true;
            }
        }

        // Check direct API key permissions (fallback for API key authenticated requests)
        var apiPermissions = user.FindAll(RbacClaimTypes.ApiPermission);
        foreach (var claim in apiPermissions)
        {
            var parsed = Permission.Parse(claim.Value);
            if (parsed.Includes(permission))
                return true;
        }

        return false;
    }

    public bool HasRole(ClaimsPrincipal user, params RoleId[] roleIds)
    {
        var roleAssignments = GetRoleAssignments(user);
        var roleIdStrings = roleIds.Select(r => r.Value.ToString()).ToHashSet();

        return roleAssignments.Any(ra => roleIdStrings.Contains(ra.Role));
    }

    public bool IsSystemAdmin(ClaimsPrincipal user)
    {
        return HasRole(user, RoleId.SystemAdmin);
    }

    public bool IsOrganizationOwner(ClaimsPrincipal user, string organizationId)
    {
        var roleAssignments = GetRoleAssignments(user);

        return roleAssignments.Any(ra =>
            ra.Role == RoleId.OrganizationOwner.Value.ToString() &&
            ra.Scope == ScopeType.Organization.ToString() &&
            ra.ScopeId == organizationId);
    }

    public string? GetUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(RbacClaimTypes.UserId)?.Value;
    }

    public IReadOnlyList<RoleAssignmentClaim> GetRoleAssignments(ClaimsPrincipal user)
    {
        var rolesClaim = user.FindFirst(RbacClaimTypes.RoleAssignments)?.Value;
        if (string.IsNullOrEmpty(rolesClaim))
        {
            return Array.Empty<RoleAssignmentClaim>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<RoleAssignmentClaim>>(rolesClaim)
                ?? new List<RoleAssignmentClaim>();
        }
        catch
        {
            return Array.Empty<RoleAssignmentClaim>();
        }
    }

    private static Role? GetRoleFromAssignment(RoleAssignmentClaim assignment)
    {
        // Try to get the predefined role by name
        if (string.IsNullOrEmpty(assignment.Role))
            return null;

        var roleId = new RoleId(assignment.Role);
        return Role.GetById(roleId);
    }

    private static bool IsAssignmentValidForScope(RoleAssignmentClaim assignment, string? organizationId, string? environmentId)
    {
        // Global scope has access to everything
        if (assignment.Scope == ScopeType.Global.ToString())
        {
            return true;
        }

        // Organization scope has access to that org and all its environments
        if (assignment.Scope == ScopeType.Organization.ToString())
        {
            // If checking for org access
            if (organizationId != null && assignment.ScopeId == organizationId)
            {
                return true;
            }
            // Environment access requires knowing which org the environment belongs to
            // For now, we assume environment checks also pass the organizationId
            if (environmentId != null && organizationId != null && assignment.ScopeId == organizationId)
            {
                return true;
            }
        }

        // Environment scope only has access to that specific environment
        if (assignment.Scope == ScopeType.Environment.ToString())
        {
            if (environmentId != null && assignment.ScopeId == environmentId)
            {
                return true;
            }
        }

        return false;
    }
}

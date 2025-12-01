using System.Reflection;
using FastEndpoints;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Infrastructure.Authentication;

namespace ReadyStackGo.Api.Authorization;

/// <summary>
/// PreProcessor that enforces RBAC permissions on endpoints.
/// Checks RequirePermission, RequireSystemAdmin, and RequireOrganizationOwner attributes.
/// </summary>
public class RbacPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
    {
        if (ctx.HttpContext.ResponseStarted())
            return;

        var endpointType = ctx.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>()?.EndpointType;
        if (endpointType == null)
            return;

        var rbacService = ctx.HttpContext.RequestServices.GetRequiredService<IRbacService>();
        var user = ctx.HttpContext.User;

        // Check RequireSystemAdmin attribute
        var requireSystemAdmin = endpointType.GetCustomAttribute<RequireSystemAdminAttribute>();
        if (requireSystemAdmin != null)
        {
            if (!rbacService.IsSystemAdmin(user))
            {
                await SendForbidden(ctx, "SystemAdmin role required.", ct);
                return;
            }
        }

        // Check RequireOrganizationOwner attribute
        var requireOrgOwner = endpointType.GetCustomAttribute<RequireOrganizationOwnerAttribute>();
        if (requireOrgOwner != null)
        {
            var organizationId = GetPropertyValue(ctx.Request, requireOrgOwner.OrganizationIdProperty);
            if (organizationId == null)
            {
                await SendForbidden(ctx, "Organization ID not found in request.", ct);
                return;
            }

            // SystemAdmin also has access
            if (!rbacService.IsSystemAdmin(user) && !rbacService.IsOrganizationOwner(user, organizationId))
            {
                await SendForbidden(ctx, "OrganizationOwner role required for this organization.", ct);
                return;
            }
        }

        // Check RequirePermission attributes
        var permissionAttributes = endpointType.GetCustomAttributes<RequirePermissionAttribute>().ToList();
        if (permissionAttributes.Count > 0)
        {
            // Extract scope from request if available
            var organizationId = GetPropertyValue(ctx.Request, "OrganizationId");
            var environmentId = GetPropertyValue(ctx.Request, "EnvironmentId");

            foreach (var attr in permissionAttributes)
            {
                var permission = new Permission(attr.Resource, attr.Action);

                if (!rbacService.HasPermission(user, permission, organizationId, environmentId))
                {
                    await SendForbidden(ctx, $"Permission '{attr.Resource}.{attr.Action}' required.", ct);
                    return;
                }
            }
        }
    }

    private static string? GetPropertyValue(TRequest? request, string propertyName)
    {
        if (request == null) return null;

        var property = typeof(TRequest).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        return property?.GetValue(request)?.ToString();
    }

    private static async Task SendForbidden(IPreProcessorContext<TRequest> ctx, string message, CancellationToken ct)
    {
        ctx.HttpContext.Response.StatusCode = 403;
        await ctx.HttpContext.Response.SendAsync(new
        {
            StatusCode = 403,
            Message = message,
            Error = "Forbidden"
        }, 403, cancellation: ct);
    }
}

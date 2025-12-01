namespace ReadyStackGo.Domain.IdentityAccess.Roles;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Value object representing a permission.
/// Permissions follow the format: Resource.Action (e.g., "Users.Create", "Deployments.Read")
/// </summary>
public sealed class Permission : ValueObject
{
    public string Resource { get; }
    public string Action { get; }

    public Permission(string resource, string action)
    {
        SelfAssertArgumentNotEmpty(resource, "Permission resource is required.");
        SelfAssertArgumentNotEmpty(action, "Permission action is required.");

        Resource = resource;
        Action = action;
    }

    public static Permission Parse(string permissionString)
    {
        AssertionConcern.AssertArgumentNotEmpty(permissionString, "Permission string is required.");
        var parts = permissionString.Split('.');
        AssertionConcern.AssertArgumentTrue(parts.Length == 2, "Permission format must be 'Resource.Action'.");
        return new Permission(parts[0], parts[1]);
    }

    public bool Includes(Permission other)
    {
        if (Resource == "*") return true;
        if (Resource != other.Resource) return false;
        if (Action == "*") return true;
        return Action == other.Action;
    }

    // Pre-defined permissions
    public static class Users
    {
        public static Permission Create => new("Users", "Create");
        public static Permission Read => new("Users", "Read");
        public static Permission Update => new("Users", "Update");
        public static Permission Delete => new("Users", "Delete");
    }

    public static class Deployments
    {
        public static Permission Create => new("Deployments", "Create");
        public static Permission Read => new("Deployments", "Read");
        public static Permission Update => new("Deployments", "Update");
        public static Permission Delete => new("Deployments", "Delete");
    }

    public static class Environments
    {
        public static Permission Create => new("Environments", "Create");
        public static Permission Read => new("Environments", "Read");
        public static Permission Update => new("Environments", "Update");
        public static Permission Delete => new("Environments", "Delete");
    }

    public static class StackSources
    {
        public static Permission Create => new("StackSources", "Create");
        public static Permission Read => new("StackSources", "Read");
        public static Permission Update => new("StackSources", "Update");
        public static Permission Delete => new("StackSources", "Delete");
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Resource;
        yield return Action;
    }

    public override string ToString() => $"{Resource}.{Action}";
}

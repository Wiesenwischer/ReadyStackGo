namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// Security configuration stored in rsgo.security.json
/// </summary>
public class SecurityConfig
{
    public AdminUser? LocalAdmin { get; set; }
    public bool LocalAdminFallbackEnabled { get; set; } = true;
    public OidcConfig? ExternalIdentityProvider { get; set; }
}

public class AdminUser
{
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Salt { get; set; }
    public string Role { get; set; } = "admin";
}

public class OidcConfig
{
    public required string Authority { get; set; }
    public required string ClientId { get; set; }
    public string RoleClaimType { get; set; } = "role";
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Custom claim types for RBAC.
/// </summary>
public static class RbacClaimTypes
{
    /// <summary>
    /// Claim type for user ID.
    /// </summary>
    public const string UserId = "uid";

    /// <summary>
    /// Claim type for role assignments (JSON array).
    /// Format: [{"Role":"SystemAdmin","Scope":"Global","ScopeId":null}, ...]
    /// Note: Program.cs sets MapInboundClaims=false to prevent ASP.NET Core from transforming this claim.
    /// </summary>
    public const string RoleAssignments = "roles";

    /// <summary>
    /// Claim type for API key ID.
    /// </summary>
    public const string ApiKeyId = "apikey_id";

    /// <summary>
    /// Claim type for API key name.
    /// </summary>
    public const string ApiKeyName = "apikey_name";

    /// <summary>
    /// Claim type for direct API key permissions.
    /// Multiple claims with this type can exist (one per permission).
    /// </summary>
    public const string ApiPermission = "api_permission";

    /// <summary>
    /// Claim type for environment ID scope.
    /// </summary>
    public const string EnvironmentId = "env_id";
}

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(RbacClaimTypes.UserId, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add role assignments as JSON claim
        var roleAssignments = user.RoleAssignments.Select(ra => new RoleAssignmentClaim
        {
            Role = ra.RoleId.Value.ToString(),
            Scope = ra.ScopeType.ToString(),
            ScopeId = ra.ScopeId
        }).ToList();

        claims.Add(new Claim(RbacClaimTypes.RoleAssignments, JsonSerializer.Serialize(roleAssignments)));

        // Add legacy role claim for backwards compatibility
        var legacyRole = user.HasRole(RoleId.SystemAdmin) ? "admin" : "user";
        claims.Add(new Claim(ClaimTypes.Role, legacyRole));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// DTO for serializing role assignments in JWT claims.
/// </summary>
public class RoleAssignmentClaim
{
    public string Role { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string? ScopeId { get; set; }
}

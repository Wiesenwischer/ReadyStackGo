using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Stateless password-reset tokens implemented as short-lived JWTs signed with the same key
/// as session tokens, distinguished by a "purpose" claim so they cannot be used as session,
/// email-verification, or any other tokens.
/// </summary>
public class PasswordResetTokenService : IPasswordResetTokenService
{
    private const string PurposeClaim = "purpose";
    private const string PurposeValue = "password_reset";

    private readonly JwtSettings _jwtSettings;

    public PasswordResetTokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string Create(UserId userId, TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(RbacClaimTypes.UserId, userId.Value.ToString()),
            new(PurposeClaim, PurposeValue),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public UserId? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirst(PurposeClaim)?.Value != PurposeValue)
                return null;

            var uid = principal.FindFirst(RbacClaimTypes.UserId)?.Value;
            if (uid == null || !Guid.TryParse(uid, out var guid))
                return null;

            return new UserId(guid);
        }
        catch
        {
            return null;
        }
    }
}

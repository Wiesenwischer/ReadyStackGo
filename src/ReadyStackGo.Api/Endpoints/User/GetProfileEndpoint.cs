using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services.Email;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.User;

/// <summary>
/// GET /api/user/profile - Returns the current user's profile information.
/// </summary>
public class GetProfileEndpoint : EndpointWithoutRequest<UserProfileResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISmtpSettingsService _smtpSettings;

    public GetProfileEndpoint(IUserRepository userRepository, ISmtpSettingsService smtpSettings)
    {
        _userRepository = userRepository;
        _smtpSettings = smtpSettings;
    }

    public override void Configure()
    {
        Get("/api/user/profile");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var user = _userRepository.Get(new UserId(userGuid));
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var role = user.HasRole(RoleId.SystemAdmin) ? "admin" : "user";

        Response = new UserProfileResponse
        {
            Username = user.Username,
            Email = user.Email.Value,
            Role = role,
            CreatedAt = user.CreatedAt,
            PasswordChangedAt = user.PasswordChangedAt,
            EmailVerified = user.IsEmailVerified,
            // Only show the "verify your email" prompt when sending is actually possible.
            SmtpEnabled = await _smtpSettings.IsEnabledAsync(ct)
        };
    }
}

public class UserProfileResponse
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public bool EmailVerified { get; set; }
    public bool SmtpEnabled { get; set; }
}

using FastEndpoints;
using Microsoft.AspNetCore.Http;
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

    public GetProfileEndpoint(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public override void Configure()
    {
        Get("/api/user/profile");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var userIdClaim = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        var user = _userRepository.Get(new UserId(userGuid));
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        var role = user.HasRole(RoleId.SystemAdmin) ? "admin" : "user";

        Response = new UserProfileResponse
        {
            Username = user.Username,
            Role = role,
            CreatedAt = user.CreatedAt,
            PasswordChangedAt = user.PasswordChangedAt
        };

        return Task.CompletedTask;
    }
}

public class UserProfileResponse
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
}

using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Api.Endpoints.User;

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// POST /api/user/change-password - Changes the current user's password.
/// Requires the current password for verification.
/// </summary>
public class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest, ChangePasswordResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public ChangePasswordEndpoint(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public override void Configure()
    {
        Post("/api/user/change-password");
    }

    public override Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
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

        // Verify current password
        if (!user.Password.Verify(req.CurrentPassword, _passwordHasher))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            Response = new ChangePasswordResponse
            {
                Success = false,
                Message = "Current password is incorrect"
            };
            return Task.CompletedTask;
        }

        // Validate and set new password via domain
        try
        {
            var newHashedPassword = HashedPassword.Create(req.NewPassword, _passwordHasher);
            user.ChangePassword(newHashedPassword);
            _userRepository.Update(user);

            Response = new ChangePasswordResponse
            {
                Success = true,
                Message = "Password changed successfully"
            };
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            Response = new ChangePasswordResponse
            {
                Success = false,
                Message = ex.Message
            };
        }

        return Task.CompletedTask;
    }
}

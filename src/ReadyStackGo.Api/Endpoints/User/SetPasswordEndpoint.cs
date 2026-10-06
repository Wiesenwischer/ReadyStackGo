using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Api.Endpoints.User;

public class SetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// POST /api/user/set-password - sets a local password for the current user if they have none
/// (accounts created through an identity provider). 409 if the user already has a password
/// (use change-password), 400 if the password does not meet the rules.
/// </summary>
public class SetPasswordEndpoint : Endpoint<SetPasswordRequest>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public SetPasswordEndpoint(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public override void Configure()
    {
        Post("/api/user/set-password");
        Description(b => b.WithTags("User"));
    }

    public override async Task HandleAsync(SetPasswordRequest req, CancellationToken ct)
    {
        var user = ListExternalIdentitiesEndpoint.CurrentUser(_userRepository, HttpContext);
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (user.HasPassword)
        {
            await HttpContext.Response.SendAsync(
                new { message = "You already have a local password. Change it instead." },
                StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        HashedPassword password;
        try
        {
            password = HashedPassword.Create(req.NewPassword, _passwordHasher);
        }
        catch (ArgumentException ex)
        {
            await HttpContext.Response.SendAsync(new { message = ex.Message }, StatusCodes.Status400BadRequest, cancellation: ct);
            return;
        }

        user.SetInitialPassword(password);
        _userRepository.Update(user);
        await Send.NoContentAsync(ct);
    }
}

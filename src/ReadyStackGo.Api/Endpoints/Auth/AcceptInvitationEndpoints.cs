using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.API.Endpoints.Invitations;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;
using DomainUser = ReadyStackGo.Domain.IdentityAccess.Users.User;

namespace ReadyStackGo.API.Endpoints.Auth;

public class InvitationInfoRequest
{
    [QueryParam]
    public string Token { get; set; } = string.Empty;
}

public class InvitationInfoResponse
{
    public bool Valid { get; set; }
    public string? Email { get; set; }
    public string? RoleId { get; set; }
    public string? ScopeType { get; set; }
}

public class AcceptInvitationRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Username { get; set; }
}

public class AcceptInvitationResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// GET /api/auth/invitation?token=... — validates an invitation token and returns the
/// invited email and target role so the acceptance page can display them. Anonymous.
/// </summary>
public class GetInvitationInfoEndpoint : Endpoint<InvitationInfoRequest, InvitationInfoResponse>
{
    private readonly IInvitationRepository _invitations;

    public GetInvitationInfoEndpoint(IInvitationRepository invitations)
    {
        _invitations = invitations;
    }

    public override void Configure()
    {
        Get("/api/auth/invitation");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override Task HandleAsync(InvitationInfoRequest req, CancellationToken ct)
    {
        var invitation = _invitations.FindPendingByTokenHash(InvitationToken.Hash(req.Token));
        if (invitation == null)
        {
            Response = new InvitationInfoResponse { Valid = false };
            return Task.CompletedTask;
        }

        Response = new InvitationInfoResponse
        {
            Valid = true,
            Email = invitation.Email.Value,
            RoleId = invitation.RoleId.Value,
            ScopeType = invitation.ScopeType.ToString()
        };
        return Task.CompletedTask;
    }
}

/// <summary>
/// POST /api/auth/accept-invitation — creates the user from a pending invitation, sets the
/// password, marks the email verified (the link proves ownership), assigns the invited
/// role, and returns a session token. Anonymous.
/// </summary>
public class AcceptInvitationEndpoint : Endpoint<AcceptInvitationRequest, AcceptInvitationResponse>
{
    private readonly IInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AcceptInvitationEndpoint(
        IInvitationRepository invitations,
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _invitations = invitations;
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public override void Configure()
    {
        Post("/api/auth/accept-invitation");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(AcceptInvitationRequest req, CancellationToken ct)
    {
        var invitation = _invitations.FindPendingByTokenHash(InvitationToken.Hash(req.Token));
        if (invitation == null)
        {
            await SendBadRequest(ct, "Invalid or expired invitation.");
            return;
        }

        // Guard against a race where a user with this email was created meanwhile.
        if (_users.FindByEmail(invitation.Email) != null)
        {
            await SendBadRequest(ct, "A user with this email already exists.");
            return;
        }

        HashedPassword hashedPassword;
        try
        {
            hashedPassword = HashedPassword.Create(req.Password, _passwordHasher);
        }
        catch (ArgumentException ex)
        {
            await SendBadRequest(ct, ex.Message);
            return;
        }

        var now = SystemClock.UtcNow;
        var username = ResolveUsername(req.Username, invitation.Email.Value);

        var user = DomainUser.Register(_users.NextIdentity(), username, invitation.Email, hashedPassword);
        user.VerifyEmail(now); // the invitation link proves email ownership
        user.AssignRole(invitation.ToRoleAssignment());

        try
        {
            invitation.Accept(now);
        }
        catch (InvalidOperationException ex)
        {
            await SendBadRequest(ct, ex.Message);
            return;
        }

        _users.Add(user);
        _invitations.Update(invitation);

        Response = new AcceptInvitationResponse
        {
            Success = true,
            Token = _tokenService.GenerateToken(user),
            Username = user.Username
        };
    }

    /// <summary>Resolves a unique username, defaulting to the email local part.</summary>
    private string ResolveUsername(string? requested, string email)
    {
        var baseName = !string.IsNullOrWhiteSpace(requested)
            ? requested.Trim()
            : email.Split('@')[0];

        if (baseName.Length < 3)
            baseName = baseName.PadRight(3, '0');
        if (baseName.Length > 40)
            baseName = baseName[..40];

        var candidate = baseName;
        var suffix = 1;
        while (_users.FindByUsername(candidate) != null)
        {
            candidate = $"{baseName}{suffix++}";
        }

        return candidate;
    }

    private async Task SendBadRequest(CancellationToken ct, string message)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        Response = new AcceptInvitationResponse { Success = false, Message = message };
        await Task.CompletedTask;
    }
}

public class AcceptInvitationValidator : Validator<AcceptInvitationRequest>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token is required");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}

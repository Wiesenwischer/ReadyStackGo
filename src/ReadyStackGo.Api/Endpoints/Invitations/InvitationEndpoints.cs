using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services.Email;
using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Invitations;

/// <summary>Helpers for generating and hashing invitation tokens.</summary>
internal static class InvitationToken
{
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class CreateInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeId { get; set; }
}

public class InvitationDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string? ScopeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>POST /api/invitations — invite an email address with a target role/scope.</summary>
[RequireSystemAdmin]
public class CreateInvitationEndpoint : Endpoint<CreateInvitationRequest, InvitationDto>
{
    private readonly IInvitationRepository _invitations;
    private readonly IUserRepository _users;
    private readonly ISmtpSettingsService _smtpSettings;

    public CreateInvitationEndpoint(
        IInvitationRepository invitations,
        IUserRepository users,
        ISmtpSettingsService smtpSettings)
    {
        _invitations = invitations;
        _users = users;
        _smtpSettings = smtpSettings;
    }

    public override void Configure()
    {
        Post("/api/invitations");
        Description(b => b.WithTags("Invitations"));
        PreProcessor<RbacPreProcessor<CreateInvitationRequest>>();
    }

    public override async Task HandleAsync(CreateInvitationRequest req, CancellationToken ct)
    {
        // Email must be sendable, otherwise the invitee can never receive the link.
        if (!await _smtpSettings.IsEnabledAsync(ct))
        {
            await SendConflict(ct, "Email is not configured. Configure SMTP before sending invitations.");
            return;
        }

        EmailAddress email;
        try
        {
            email = new EmailAddress(req.Email);
        }
        catch (ArgumentException ex)
        {
            await SendConflict(ct, ex.Message);
            return;
        }

        if (!Enum.TryParse<ScopeType>(req.ScopeType, ignoreCase: true, out var scopeType))
        {
            await SendConflict(ct, $"Invalid scope type '{req.ScopeType}'.");
            return;
        }

        var roleId = new RoleId(req.RoleId);
        var role = Role.GetById(roleId);
        if (role == null)
        {
            await SendConflict(ct, $"Unknown role '{req.RoleId}'.");
            return;
        }

        if (!role.CanBeAssignedToScope(scopeType))
        {
            await SendConflict(ct, $"Role '{req.RoleId}' cannot be assigned at scope '{scopeType}'.");
            return;
        }

        if (scopeType != ScopeType.Global && string.IsNullOrWhiteSpace(req.ScopeId))
        {
            await SendConflict(ct, "A scope id is required for non-global scopes.");
            return;
        }

        if (_users.FindByEmail(email) != null)
        {
            await SendConflict(ct, "A user with this email already exists.");
            return;
        }

        if (_invitations.FindPendingByEmail(email) != null)
        {
            await SendConflict(ct, "A pending invitation for this email already exists.");
            return;
        }

        var invitedByClaim = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;
        if (string.IsNullOrEmpty(invitedByClaim) || !Guid.TryParse(invitedByClaim, out var invitedByGuid))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var token = InvitationToken.Generate();
        var now = DateTime.UtcNow;

        var invitation = Invitation.Create(
            _invitations.NextIdentity(),
            email,
            token,
            InvitationToken.Hash(token),
            roleId,
            scopeType,
            scopeType == ScopeType.Global ? null : req.ScopeId,
            new UserId(invitedByGuid),
            now,
            now.AddDays(7));

        // Persisting dispatches the InvitationCreated domain event, which sends the email.
        _invitations.Add(invitation);

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        Response = ToDto(invitation);
    }

    private async Task SendConflict(CancellationToken ct, string message)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await HttpContext.Response.SendAsync(new { message }, StatusCodes.Status409Conflict, cancellation: ct);
    }

    internal static InvitationDto ToDto(Invitation i) => new()
    {
        Id = i.Id.ToString(),
        Email = i.Email.Value,
        Status = i.Status.ToString(),
        RoleId = i.RoleId.Value,
        ScopeType = i.ScopeType.ToString(),
        ScopeId = i.ScopeId,
        CreatedAt = i.CreatedAt,
        ExpiresAt = i.ExpiresAt
    };
}

/// <summary>GET /api/invitations — list all invitations.</summary>
[RequireSystemAdmin]
public class ListInvitationsEndpoint : EndpointWithoutRequest<List<InvitationDto>>
{
    private readonly IInvitationRepository _invitations;

    public ListInvitationsEndpoint(IInvitationRepository invitations)
    {
        _invitations = invitations;
    }

    public override void Configure()
    {
        Get("/api/invitations");
        Description(b => b.WithTags("Invitations"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response = _invitations.GetAll()
            .OrderByDescending(i => i.CreatedAt)
            .Select(CreateInvitationEndpoint.ToDto)
            .ToList();
        return Task.CompletedTask;
    }
}

public class RevokeInvitationRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>DELETE /api/invitations/{id} — revoke a pending invitation.</summary>
[RequireSystemAdmin]
public class RevokeInvitationEndpoint : Endpoint<RevokeInvitationRequest>
{
    private readonly IInvitationRepository _invitations;

    public RevokeInvitationEndpoint(IInvitationRepository invitations)
    {
        _invitations = invitations;
    }

    public override void Configure()
    {
        Delete("/api/invitations/{id}");
        Description(b => b.WithTags("Invitations"));
        PreProcessor<RbacPreProcessor<RevokeInvitationRequest>>();
    }

    public override async Task HandleAsync(RevokeInvitationRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(req.Id, out var guid))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var invitation = _invitations.Get(new InvitationId(guid));
        if (invitation == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            invitation.Revoke();
            _invitations.Update(invitation);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await HttpContext.Response.SendAsync(new { message = ex.Message }, StatusCodes.Status409Conflict, cancellation: ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public class CreateInvitationValidator : Validator<CreateInvitationRequest>
{
    public CreateInvitationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email is invalid");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role is required");

        RuleFor(x => x.ScopeType)
            .NotEmpty().WithMessage("Scope type is required");
    }
}

using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Email;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.API.Endpoints.Auth;

public class RequestPasswordResetRequest
{
    /// <summary>Email address or username.</summary>
    public string Identifier { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// POST /api/auth/request-password-reset — sends a reset link if the account exists and email
/// is configured. Always returns success to avoid leaking which accounts exist. Anonymous.
/// </summary>
public class RequestPasswordResetEndpoint : Endpoint<RequestPasswordResetRequest, SimpleResultResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ISmtpSettingsService _smtpSettings;
    private readonly IConfigStore _configStore;

    public RequestPasswordResetEndpoint(
        IUserRepository users,
        IPasswordResetTokenService tokenService,
        IEmailService emailService,
        ISmtpSettingsService smtpSettings,
        IConfigStore configStore)
    {
        _users = users;
        _tokenService = tokenService;
        _emailService = emailService;
        _smtpSettings = smtpSettings;
        _configStore = configStore;
    }

    public override void Configure()
    {
        Post("/api/auth/request-password-reset");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override async Task HandleAsync(RequestPasswordResetRequest req, CancellationToken ct)
    {
        // Generic response regardless of outcome (no account enumeration).
        var genericResponse = new SimpleResultResponse
        {
            Success = true,
            Message = "If an account exists for that identifier, a reset email has been sent."
        };

        var user = FindByIdentifier(req.Identifier);
        if (user != null && await _smtpSettings.IsEnabledAsync(ct))
        {
            var token = _tokenService.Create(user.Id, TimeSpan.FromHours(1));
            var systemConfig = await _configStore.GetSystemConfigAsync();
            var link = $"{systemConfig.BaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

            await _emailService.SendAsync(
                user.Email.Value,
                "Reset your ReadyStackGo password",
                $"<p>We received a request to reset your password. Click the link below to choose a new one:</p>" +
                $"<p><a href=\"{link}\">Reset password</a></p>" +
                $"<p>This link expires in 1 hour. If you did not request this, you can ignore this email.</p>",
                ct);
        }

        Response = genericResponse;
    }

    private User? FindByIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        if (identifier.Contains('@'))
        {
            try
            {
                var byEmail = _users.FindByEmail(new EmailAddress(identifier));
                if (byEmail != null) return byEmail;
            }
            catch (ArgumentException)
            {
                // Not a valid email; fall through to username lookup.
            }
        }

        return _users.FindByUsername(identifier);
    }
}

/// <summary>
/// POST /api/auth/reset-password — sets a new password from a valid reset token. Anonymous.
/// </summary>
public class ResetPasswordEndpoint : Endpoint<ResetPasswordRequest, SimpleResultResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordEndpoint(
        IUserRepository users,
        IPasswordResetTokenService tokenService,
        IPasswordHasher passwordHasher)
    {
        _users = users;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public override void Configure()
    {
        Post("/api/auth/reset-password");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override Task HandleAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var userId = _tokenService.Validate(req.Token);
        if (userId == null)
        {
            return Fail("Invalid or expired token.");
        }

        var user = _users.Get(userId);
        if (user == null)
        {
            return Fail("Invalid or expired token.");
        }

        try
        {
            var hashed = HashedPassword.Create(req.NewPassword, _passwordHasher);
            user.ChangePassword(hashed);
            _users.Update(user);
        }
        catch (ArgumentException ex)
        {
            return Fail(ex.Message);
        }

        Response = new SimpleResultResponse { Success = true, Message = "Password updated." };
        return Task.CompletedTask;
    }

    private Task Fail(string message)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        Response = new SimpleResultResponse { Success = false, Message = message };
        return Task.CompletedTask;
    }
}

public class RequestPasswordResetValidator : Validator<RequestPasswordResetRequest>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Identifier is required");
    }
}

public class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Token is required");
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}

using FastEndpoints;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Email;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Auth;

public class SimpleResultResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// POST /api/auth/request-email-verification — sends a verification link to the current
/// user's email. Authenticated; requires SMTP to be configured.
/// </summary>
public class RequestEmailVerificationEndpoint : EndpointWithoutRequest<SimpleResultResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ISmtpSettingsService _smtpSettings;
    private readonly IConfigStore _configStore;

    public RequestEmailVerificationEndpoint(
        IUserRepository userRepository,
        IEmailVerificationTokenService tokenService,
        IEmailService emailService,
        ISmtpSettingsService smtpSettings,
        IConfigStore configStore)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _emailService = emailService;
        _smtpSettings = smtpSettings;
        _configStore = configStore;
    }

    public override void Configure()
    {
        Post("/api/auth/request-email-verification");
        Description(b => b.WithTags("Auth"));
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

        if (user.IsEmailVerified)
        {
            Response = new SimpleResultResponse { Success = true, Message = "Email already verified." };
            return;
        }

        if (!await _smtpSettings.IsEnabledAsync(ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            Response = new SimpleResultResponse
            {
                Success = false,
                Message = "Email is not configured. Configure SMTP first."
            };
            return;
        }

        var token = _tokenService.Create(user.Id, TimeSpan.FromHours(24));
        var systemConfig = await _configStore.GetSystemConfigAsync();
        var link = $"{systemConfig.BaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";

        var result = await _emailService.SendAsync(
            user.Email.Value,
            "Verify your ReadyStackGo email address",
            $"<p>Confirm your email address by clicking the link below:</p>" +
            $"<p><a href=\"{link}\">Verify email address</a></p>" +
            $"<p>This link expires in 24 hours.</p>",
            ct);

        Response = new SimpleResultResponse
        {
            Success = result.Success,
            Message = result.Success ? "Verification email sent." : result.Error
        };
    }
}

/// <summary>
/// POST /api/auth/verify-email — verifies an email address from a signed token. Anonymous.
/// </summary>
public class VerifyEmailEndpoint : Endpoint<VerifyEmailRequest, SimpleResultResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenService _tokenService;

    public VerifyEmailEndpoint(IUserRepository userRepository, IEmailVerificationTokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public override void Configure()
    {
        Post("/api/auth/verify-email");
        AllowAnonymous();
        Description(b => b.WithTags("Auth"));
    }

    public override Task HandleAsync(VerifyEmailRequest req, CancellationToken ct)
    {
        var userId = _tokenService.Validate(req.Token);
        if (userId == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            Response = new SimpleResultResponse { Success = false, Message = "Invalid or expired token." };
            return Task.CompletedTask;
        }

        var user = _userRepository.Get(userId);
        if (user == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            Response = new SimpleResultResponse { Success = false, Message = "Invalid or expired token." };
            return Task.CompletedTask;
        }

        user.VerifyEmail(SystemClock.UtcNow);
        _userRepository.Update(user);

        Response = new SimpleResultResponse { Success = true, Message = "Email verified." };
        return Task.CompletedTask;
    }
}

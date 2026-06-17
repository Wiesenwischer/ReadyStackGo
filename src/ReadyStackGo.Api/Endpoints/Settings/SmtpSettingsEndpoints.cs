using FastEndpoints;
using FluentValidation;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services.Email;

namespace ReadyStackGo.API.Endpoints.Settings;

/// <summary>
/// DTO for reading/writing SMTP settings. The password is write-only: on read it is never
/// returned (only <see cref="HasPassword"/> indicates whether one is stored); on write an
/// empty value means "keep the existing password".
/// </summary>
public class SmtpSettingsDto
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ReadyStackGo";

    /// <summary>Write-only. Empty on write keeps the stored password. Always null on read.</summary>
    public string? Password { get; set; }

    /// <summary>Read-only. True if a password is currently stored.</summary>
    public bool HasPassword { get; set; }
}

public class TestSmtpRequest : SmtpSettingsDto
{
    public string ToAddress { get; set; } = string.Empty;
}

/// <summary>GET /api/settings/smtp — read current SMTP settings (password never returned).</summary>
[RequireSystemAdmin]
public class GetSmtpSettingsEndpoint : EndpointWithoutRequest<SmtpSettingsDto>
{
    private readonly ISmtpSettingsService _settingsService;

    public GetSmtpSettingsEndpoint(ISmtpSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override void Configure()
    {
        Get("/api/settings/smtp");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        Response = new SmtpSettingsDto
        {
            Enabled = settings.Enabled,
            Host = settings.Host,
            Port = settings.Port,
            UseStartTls = settings.UseStartTls,
            Username = settings.Username,
            FromAddress = settings.FromAddress,
            FromName = settings.FromName,
            Password = null,
            HasPassword = !string.IsNullOrEmpty(settings.Password)
        };
    }
}

/// <summary>PUT /api/settings/smtp — persist SMTP settings.</summary>
[RequireSystemAdmin]
public class SaveSmtpSettingsEndpoint : Endpoint<SmtpSettingsDto>
{
    private readonly ISmtpSettingsService _settingsService;

    public SaveSmtpSettingsEndpoint(ISmtpSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override void Configure()
    {
        Put("/api/settings/smtp");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<SmtpSettingsDto>>();
    }

    public override async Task HandleAsync(SmtpSettingsDto req, CancellationToken ct)
    {
        await _settingsService.SaveAsync(new SmtpSettings
        {
            Enabled = req.Enabled,
            Host = req.Host,
            Port = req.Port,
            UseStartTls = req.UseStartTls,
            Username = req.Username,
            Password = req.Password,
            FromAddress = req.FromAddress,
            FromName = req.FromName
        }, ct);

        await Send.NoContentAsync(ct);
    }
}

/// <summary>POST /api/settings/smtp/test — send a test email with the given settings.</summary>
[RequireSystemAdmin]
public class TestSmtpSettingsEndpoint : Endpoint<TestSmtpRequest, EmailSendResult>
{
    private readonly IEmailService _emailService;
    private readonly ISmtpSettingsService _settingsService;

    public TestSmtpSettingsEndpoint(IEmailService emailService, ISmtpSettingsService settingsService)
    {
        _emailService = emailService;
        _settingsService = settingsService;
    }

    public override void Configure()
    {
        Post("/api/settings/smtp/test");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<TestSmtpRequest>>();
    }

    public override async Task HandleAsync(TestSmtpRequest req, CancellationToken ct)
    {
        // If the password field is empty, fall back to the stored password so the user can
        // test an existing configuration without re-entering the secret.
        var password = req.Password;
        if (string.IsNullOrEmpty(password))
        {
            var stored = await _settingsService.GetAsync(ct);
            password = stored.Password;
        }

        var settings = new SmtpSettings
        {
            Enabled = true,
            Host = req.Host,
            Port = req.Port,
            UseStartTls = req.UseStartTls,
            Username = req.Username,
            Password = password,
            FromAddress = req.FromAddress,
            FromName = req.FromName
        };

        Response = await _emailService.SendTestAsync(settings, req.ToAddress, ct);
    }
}

public class TestSmtpRequestValidator : Validator<TestSmtpRequest>
{
    public TestSmtpRequestValidator()
    {
        RuleFor(x => x.ToAddress)
            .NotEmpty().WithMessage("Recipient address is required")
            .EmailAddress().WithMessage("Recipient address is invalid");

        RuleFor(x => x.Host)
            .NotEmpty().WithMessage("SMTP host is required");

        RuleFor(x => x.FromAddress)
            .NotEmpty().WithMessage("From address is required")
            .EmailAddress().WithMessage("From address is invalid");
    }
}

public class SaveSmtpSettingsValidator : Validator<SmtpSettingsDto>
{
    public SaveSmtpSettingsValidator()
    {
        // Only validate connection details when email is enabled; a disabled config may be blank.
        When(x => x.Enabled, () =>
        {
            RuleFor(x => x.Host).NotEmpty().WithMessage("SMTP host is required");
            RuleFor(x => x.Port).InclusiveBetween(1, 65535).WithMessage("Port must be between 1 and 65535");
            RuleFor(x => x.FromAddress)
                .NotEmpty().WithMessage("From address is required")
                .EmailAddress().WithMessage("From address is invalid");
        });
    }
}

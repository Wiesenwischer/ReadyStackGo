using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Email;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services.Email;

/// <summary>
/// Persists SMTP settings in rsgo.smtp.json via <see cref="IConfigStore"/>, encrypting the
/// password at rest with <see cref="ICredentialEncryptionService"/>.
/// </summary>
public class SmtpSettingsService : ISmtpSettingsService
{
    private readonly IConfigStore _configStore;
    private readonly ICredentialEncryptionService _encryption;

    public SmtpSettingsService(IConfigStore configStore, ICredentialEncryptionService encryption)
    {
        _configStore = configStore;
        _encryption = encryption;
    }

    public async Task<SmtpSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetSmtpConfigAsync();

        return new SmtpSettings
        {
            Enabled = config.Enabled,
            Host = config.Host,
            Port = config.Port,
            UseStartTls = config.UseStartTls,
            Username = config.Username,
            Password = string.IsNullOrEmpty(config.EncryptedPassword)
                ? null
                : _encryption.Decrypt(config.EncryptedPassword),
            FromAddress = config.FromAddress,
            FromName = config.FromName
        };
    }

    public async Task SaveAsync(SmtpSettings settings, CancellationToken cancellationToken = default)
    {
        // Preserve the existing encrypted password when the caller did not supply a new one
        // (the UI sends an empty password to mean "keep the current one").
        var existing = await _configStore.GetSmtpConfigAsync();

        var encryptedPassword = string.IsNullOrEmpty(settings.Password)
            ? existing.EncryptedPassword
            : _encryption.Encrypt(settings.Password);

        var config = new SmtpConfig
        {
            Enabled = settings.Enabled,
            Host = settings.Host,
            Port = settings.Port,
            UseStartTls = settings.UseStartTls,
            Username = settings.Username,
            EncryptedPassword = encryptedPassword,
            FromAddress = settings.FromAddress,
            FromName = settings.FromName
        };

        await _configStore.SaveSmtpConfigAsync(config);
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        return settings.Enabled && settings.IsComplete();
    }
}

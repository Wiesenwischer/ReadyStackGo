using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Issues and validates short-lived, signed email-verification tokens. Tokens are
/// stateless (no database storage): they carry the user id and a purpose marker and are
/// validated by signature and expiry.
/// </summary>
public interface IEmailVerificationTokenService
{
    /// <summary>Creates a signed verification token for the user, valid for <paramref name="lifetime"/>.</summary>
    string Create(UserId userId, TimeSpan lifetime);

    /// <summary>Validates a token and returns the user id, or null if invalid/expired/wrong purpose.</summary>
    UserId? Validate(string token);
}

using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Issues and validates short-lived, signed password-reset tokens. Stateless: the token
/// carries the user id and a purpose marker and is validated by signature and expiry.
/// A reset token cannot be used as a session or email-verification token (distinct purpose).
/// </summary>
public interface IPasswordResetTokenService
{
    string Create(UserId userId, TimeSpan lifetime);
    UserId? Validate(string token);
}

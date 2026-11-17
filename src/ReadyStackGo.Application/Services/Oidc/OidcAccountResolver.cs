using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>
/// Maps a sign-in at an OIDC provider to a ReadyStackGo account:
/// <list type="number">
/// <item>An existing link (provider, subject) wins.</item>
/// <item>Otherwise only by email, and only if the provider confirmed it or the provider is
/// set to trust unverified addresses. This applies to existing users and to invitations.</item>
/// <item>An existing user linked to this provider with a different subject is rejected.</item>
/// <item>An existing user without a link gets linked; the email is marked verified only if the
/// provider confirmed it.</item>
/// <item>A pending invitation creates the user.</item>
/// <item>Otherwise the sign-in is rejected (no account).</item>
/// </list>
/// Disabled accounts are rejected like the sign-in with a password.
/// </summary>
public class OidcAccountResolver
{
    private readonly IUserRepository _users;
    private readonly IInvitationRepository _invitations;
    private readonly UsernameGenerator _usernames;

    public OidcAccountResolver(IUserRepository users, IInvitationRepository invitations, UsernameGenerator usernames)
    {
        _users = users;
        _invitations = invitations;
        _usernames = usernames;
    }

    public OidcResolution Resolve(OidcProviderSettings provider, OidcUserInfo userInfo, DateTime now)
    {
        // (1) Existing link wins, even if the email changed at the provider.
        var linked = _users.FindByExternalIdentity(provider.Name, userInfo.Subject);
        if (linked != null)
        {
            return linked.Enablement.IsEnabled
                ? OidcResolution.Success(linked)
                : OidcResolution.Failure(OidcSignInErrors.AccountDisabled);
        }

        if (string.IsNullOrWhiteSpace(userInfo.Email))
        {
            return OidcResolution.Failure(OidcSignInErrors.EmailMissing);
        }

        EmailAddress email;
        try
        {
            email = new EmailAddress(userInfo.Email);
        }
        catch (ArgumentException)
        {
            return OidcResolution.Failure(OidcSignInErrors.EmailInvalid);
        }

        // (2) Matching by email needs a confirmed address (or explicit trust).
        if (!userInfo.EmailVerified && !provider.TrustUnverifiedEmail)
        {
            return OidcResolution.Failure(OidcSignInErrors.EmailUnverified);
        }

        var existing = _users.FindByEmail(email);
        if (existing != null)
        {
            // (3) Linked to this provider, but to another identity: never overwrite silently.
            if (existing.FindExternalIdentity(provider.Name) != null)
            {
                return OidcResolution.Failure(OidcSignInErrors.SubjectMismatch);
            }

            if (!existing.Enablement.IsEnabled)
            {
                return OidcResolution.Failure(OidcSignInErrors.AccountDisabled);
            }

            // (4) Link, and verify the email only on a real confirmation.
            existing.LinkExternalIdentity(provider.Name, userInfo.Subject);
            if (userInfo.EmailVerified && !existing.IsEmailVerified)
            {
                existing.VerifyEmail(now);
            }
            _users.Update(existing);
            return OidcResolution.Success(existing);
        }

        // (5) Pending invitation: just-in-time provisioning.
        var invitation = _invitations.FindPendingByEmail(email);
        if (invitation == null)
        {
            return OidcResolution.Failure(OidcSignInErrors.NoAccount);
        }

        var username = _usernames.Generate(userInfo.Username, email.Value);
        var user = User.RegisterExternal(
            _users.NextIdentity(), username, email, provider.Name, userInfo.Subject, emailVerified: userInfo.EmailVerified);
        user.AssignRole(invitation.ToRoleAssignment());

        try
        {
            invitation.Accept(now);
        }
        catch (InvalidOperationException)
        {
            return OidcResolution.Failure(OidcSignInErrors.NoAccount);
        }

        _users.Add(user);
        _invitations.Update(invitation);
        return OidcResolution.Success(user);
    }
}

public sealed record OidcResolution(User? User, string? Error)
{
    public static OidcResolution Success(User user) => new(user, null);

    public static OidcResolution Failure(string error) => new(null, error);
}

/// <summary>Error codes of a failed sign-in, passed to the login page as ?error=.</summary>
public static class OidcSignInErrors
{
    public const string Failed = "oidc_failed";
    public const string State = "oidc_state";
    public const string Provider = "oidc_provider";
    public const string Token = "oidc_token";
    public const string NoAccount = "oidc_no_account";
    public const string EmailUnverified = "oidc_email_unverified";
    public const string EmailMissing = "oidc_email_missing";
    public const string EmailInvalid = "oidc_email_invalid";
    public const string SubjectMismatch = "oidc_subject_mismatch";
    public const string AccountDisabled = "oidc_account_disabled";
    public const string ProviderRejected = "oidc_provider_rejected";
    public const string Unreachable = "oidc_unreachable";
}

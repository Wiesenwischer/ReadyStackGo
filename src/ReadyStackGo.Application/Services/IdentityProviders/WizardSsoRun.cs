namespace ReadyStackGo.Application.Services.IdentityProviders;

public enum WizardSsoRunState
{
    /// <summary>Started; the browser is on its way to the provider to pair.</summary>
    Started,

    /// <summary>Paired: the provider is saved (disabled); the sign-in is next.</summary>
    Registered,

    /// <summary>The first system administrator exists. Final.</summary>
    SignedIn,

    /// <summary>Failed for <see cref="WizardSsoRun.FailureReason"/>; "Try again" may continue.</summary>
    Failed
}

/// <summary>Reasons a wizard run with an identity provider fails.</summary>
public static class WizardSsoFailure
{
    public const string Unreachable = "unreachable";
    public const string Cancelled = "cancelled";
    public const string LimitReached = "limit_reached";
    public const string RegistrationFailed = "registration_failed";
    public const string SignInFailed = "sign_in_failed";
    public const string EmailUnverified = "email_unverified";
    public const string EmailInvalid = "email_invalid";
    public const string Expired = "expired";
    public const string CompletedElsewhere = "completed_elsewhere";

    /// <summary>Failures after which "Try again" signs in again instead of pairing again.</summary>
    public static bool RetriesSignIn(string reason) =>
        reason is EmailUnverified or EmailInvalid or SignInFailed;

    /// <summary>Failures that end the run for good.</summary>
    public static bool IsFinal(string reason) => reason is Expired or CompletedElsewhere;
}

/// <summary>
/// A run of the setup wizard that makes an identity provider account the first system
/// administrator: Started → Registered → SignedIn, Failed from any non-final state. A run
/// started inside the setup window may last <see cref="Duration"/> (15 minutes by default),
/// so pairing and signing in (maybe with account creation) do not fail on the window.
/// Invalid transitions throw <see cref="InvalidOperationException"/>.
/// </summary>
public class WizardSsoRun
{
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(15);

    public WizardSsoRun(string id, string templateId, string flowSecret, string baseUrl, DateTime startedAt, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("Template is required.", nameof(templateId));
        if (string.IsNullOrWhiteSpace(flowSecret)) throw new ArgumentException("Flow secret is required.", nameof(flowSecret));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));

        Id = id;
        TemplateId = templateId;
        FlowSecret = flowSecret;
        BaseUrl = baseUrl;
        StartedAt = startedAt;
        ExpiresAt = startedAt + duration;
        State = WizardSsoRunState.Started;
    }

    public string Id { get; }
    public string TemplateId { get; }
    public string FlowSecret { get; }
    public string BaseUrl { get; }
    public DateTime StartedAt { get; }
    public DateTime ExpiresAt { get; }
    public TimeSpan Duration => ExpiresAt - StartedAt;

    public WizardSsoRunState State { get; private set; }
    public string? FailureReason { get; private set; }
    public string? FailureDetail { get; private set; }

    /// <summary>Name of the provider saved after pairing.</summary>
    public string? ProviderName { get; private set; }

    /// <summary>State of the pairing in flight and the code it brought back.</summary>
    public string? PendingRegistrationState { get; set; }
    public string? PendingRegistrationCode { get; set; }

    public string? SignedInUsername { get; private set; }
    public string? SignedInEmail { get; private set; }
    public string? SignedInDisplayName { get; private set; }

    public bool IsExpired(DateTime now) => State != WizardSsoRunState.SignedIn && now >= ExpiresAt;

    /// <summary>Started → Registered: the provider is saved under <paramref name="providerName"/>.</summary>
    public void MarkRegistered(string providerName, DateTime now)
    {
        EnsureNotExpired(now);
        if (State != WizardSsoRunState.Started)
        {
            throw new InvalidOperationException($"Cannot register in state {State}.");
        }
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.", nameof(providerName));

        ProviderName = providerName;
        PendingRegistrationCode = null;
        PendingRegistrationState = null;
        State = WizardSsoRunState.Registered;
    }

    /// <summary>Registered → SignedIn: the first system administrator was created.</summary>
    public void MarkSignedIn(string username, string email, string? displayName, DateTime now)
    {
        EnsureNotExpired(now);
        if (State != WizardSsoRunState.Registered)
        {
            throw new InvalidOperationException($"Cannot sign in in state {State}.");
        }

        SignedInUsername = username;
        SignedInEmail = email;
        SignedInDisplayName = displayName;
        State = WizardSsoRunState.SignedIn;
    }

    /// <summary>Any non-final state → Failed.</summary>
    public void Fail(string reason, string? detail = null)
    {
        if (State == WizardSsoRunState.SignedIn)
        {
            throw new InvalidOperationException("A run that signed in cannot fail.");
        }
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
        if (State == WizardSsoRunState.Failed && FailureReason != null && WizardSsoFailure.IsFinal(FailureReason))
        {
            // A final failure (expired, completed elsewhere) stays; later errors do not hide it.
            return;
        }

        State = WizardSsoRunState.Failed;
        FailureReason = reason;
        FailureDetail = detail;
    }

    /// <summary>
    /// "Try again" from Failed before the run expires: back to Registered (sign in again) when
    /// the provider is saved and the failure was about the sign-in, otherwise back to Started
    /// (pair again).
    /// </summary>
    public void Retry(DateTime now)
    {
        if (State != WizardSsoRunState.Failed)
        {
            throw new InvalidOperationException($"Cannot retry in state {State}.");
        }
        if (FailureReason != null && WizardSsoFailure.IsFinal(FailureReason))
        {
            throw new InvalidOperationException($"A run that failed with {FailureReason} cannot be retried.");
        }
        EnsureNotExpired(now);

        State = ProviderName != null && FailureReason != null && WizardSsoFailure.RetriesSignIn(FailureReason)
            ? WizardSsoRunState.Registered
            : WizardSsoRunState.Started;
        FailureReason = null;
        FailureDetail = null;
        PendingRegistrationCode = null;
        PendingRegistrationState = null;
    }

    private void EnsureNotExpired(DateTime now)
    {
        if (now >= ExpiresAt)
        {
            throw new InvalidOperationException("The run has expired.");
        }
    }
}

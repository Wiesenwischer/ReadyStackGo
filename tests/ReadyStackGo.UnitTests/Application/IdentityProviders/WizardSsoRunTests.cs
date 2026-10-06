using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using ReadyStackGo.Application.Services.IdentityProviders;

namespace ReadyStackGo.UnitTests.Application.IdentityProviders;

public class WizardSsoRunTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private WizardSsoRun NewRun(TimeSpan? duration = null) =>
        new("run-1", "wysch", "secret", "https://rsgo.example.com", Now, duration ?? WizardSsoRun.DefaultDuration);

    private WizardSsoRun RegisteredRun()
    {
        var run = NewRun();
        run.MarkRegistered("wysch", Now);
        return run;
    }

    #region Construction

    [Fact]
    public void Constructor_StartsInStartedWithExpiryAfterDuration()
    {
        var run = NewRun();

        run.State.Should().Be(WizardSsoRunState.Started);
        run.StartedAt.Should().Be(Now);
        run.ExpiresAt.Should().Be(Now.AddSeconds(900));
        run.Duration.Should().Be(TimeSpan.FromMinutes(15));
        run.FailureReason.Should().BeNull();
        run.ProviderName.Should().BeNull();
    }

    [Theory]
    [InlineData("", "wysch", "secret")]
    [InlineData(" ", "wysch", "secret")]
    [InlineData("id", "", "secret")]
    [InlineData("id", "wysch", "")]
    [InlineData("id", "wysch", "  ")]
    public void Constructor_MissingValues_Throw(string id, string template, string secret)
    {
        var act = () => new WizardSsoRun(id, template, secret, "https://x", Now, WizardSsoRun.DefaultDuration);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveDuration_Throws(int seconds)
    {
        var act = () => NewRun(TimeSpan.FromSeconds(seconds));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Valid transitions

    [Fact]
    public void FullRun_StartedRegisteredSignedIn()
    {
        var run = NewRun();
        run.PendingRegistrationState = "state";
        run.PendingRegistrationCode = "code";

        _time.Advance(TimeSpan.FromMinutes(5));
        run.MarkRegistered("wysch", Now);

        run.State.Should().Be(WizardSsoRunState.Registered);
        run.ProviderName.Should().Be("wysch");
        run.PendingRegistrationCode.Should().BeNull();
        run.PendingRegistrationState.Should().BeNull();

        _time.Advance(TimeSpan.FromMinutes(5));
        run.MarkSignedIn("admin", "admin@example.com", "Admin");

        run.State.Should().Be(WizardSsoRunState.SignedIn);
        run.SignedInUsername.Should().Be("admin");
        run.SignedInEmail.Should().Be("admin@example.com");
        run.SignedInDisplayName.Should().Be("Admin");
    }

    [Fact]
    public void Fail_FromStarted_SetsReasonAndDetail()
    {
        var run = NewRun();

        run.Fail(WizardSsoFailure.Unreachable, "timeout");

        run.State.Should().Be(WizardSsoRunState.Failed);
        run.FailureReason.Should().Be(WizardSsoFailure.Unreachable);
        run.FailureDetail.Should().Be("timeout");
    }

    [Fact]
    public void Fail_FromRegistered_KeepsProviderName()
    {
        var run = RegisteredRun();

        run.Fail(WizardSsoFailure.SignInFailed);

        run.State.Should().Be(WizardSsoRunState.Failed);
        run.ProviderName.Should().Be("wysch");
    }

    [Fact]
    public void Fail_FromFailed_ReplacesReason()
    {
        var run = NewRun();
        run.Fail(WizardSsoFailure.Unreachable);

        run.Fail(WizardSsoFailure.Expired);

        run.FailureReason.Should().Be(WizardSsoFailure.Expired);
    }

    [Theory]
    [InlineData(WizardSsoFailure.Expired)]
    [InlineData(WizardSsoFailure.CompletedElsewhere)]
    public void Fail_AfterFinalFailure_KeepsFinalReason(string finalReason)
    {
        var run = NewRun();
        run.Fail(finalReason);

        run.Fail(WizardSsoFailure.Unreachable, "later error");

        run.FailureReason.Should().Be(finalReason);
        run.FailureDetail.Should().BeNull();
    }

    #endregion

    #region Invalid transitions

    [Fact]
    public void MarkSignedIn_FromStarted_Throws()
    {
        var run = NewRun();

        var act = () => run.MarkSignedIn("admin", "admin@example.com", null);

        act.Should().Throw<InvalidOperationException>();
        run.State.Should().Be(WizardSsoRunState.Started);
    }

    [Fact]
    public void MarkRegistered_Twice_Throws()
    {
        var run = RegisteredRun();

        var act = () => run.MarkRegistered("other", Now);

        act.Should().Throw<InvalidOperationException>();
        run.ProviderName.Should().Be("wysch");
    }

    [Fact]
    public void MarkRegistered_AfterSignedIn_Throws()
    {
        var run = RegisteredRun();
        run.MarkSignedIn("admin", "admin@example.com", null);

        var act = () => run.MarkRegistered("wysch", Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSignedIn_Twice_Throws()
    {
        var run = RegisteredRun();
        run.MarkSignedIn("admin", "admin@example.com", null);

        var act = () => run.MarkSignedIn("other", "other@example.com", null);

        act.Should().Throw<InvalidOperationException>();
        run.SignedInUsername.Should().Be("admin");
    }

    [Fact]
    public void Fail_AfterSignedIn_Throws()
    {
        var run = RegisteredRun();
        run.MarkSignedIn("admin", "admin@example.com", null);

        var act = () => run.Fail(WizardSsoFailure.SignInFailed);

        act.Should().Throw<InvalidOperationException>();
        run.State.Should().Be(WizardSsoRunState.SignedIn);
    }

    [Fact]
    public void MarkRegistered_FromFailed_Throws()
    {
        var run = NewRun();
        run.Fail(WizardSsoFailure.Unreachable);

        var act = () => run.MarkRegistered("wysch", Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkSignedIn_FromFailed_Throws()
    {
        var run = RegisteredRun();
        run.Fail(WizardSsoFailure.SignInFailed);

        var act = () => run.MarkSignedIn("admin", "admin@example.com", null);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MarkRegistered_EmptyProviderName_Throws(string? name)
    {
        var run = NewRun();

        var act = () => run.MarkRegistered(name!, Now);

        act.Should().Throw<ArgumentException>();
        run.State.Should().Be(WizardSsoRunState.Started);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Fail_EmptyReason_Throws(string? reason)
    {
        var run = NewRun();

        var act = () => run.Fail(reason!);

        act.Should().Throw<ArgumentException>();
        run.State.Should().Be(WizardSsoRunState.Started);
    }

    [Fact]
    public void Retry_FromStarted_Throws()
    {
        var act = () => NewRun().Retry(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retry_FromRegistered_Throws()
    {
        var act = () => RegisteredRun().Retry(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Retry_FromSignedIn_Throws()
    {
        var run = RegisteredRun();
        run.MarkSignedIn("admin", "admin@example.com", null);

        var act = () => run.Retry(Now);

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Expiry

    [Fact]
    public void IsExpired_JustBefore900Seconds_False_At900Seconds_True()
    {
        var run = NewRun();

        _time.Advance(TimeSpan.FromSeconds(899));
        run.IsExpired(Now).Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(1));
        run.IsExpired(Now).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_SignedInRun_NeverExpires()
    {
        var run = RegisteredRun();
        run.MarkSignedIn("admin", "admin@example.com", null);

        _time.Advance(TimeSpan.FromHours(1));

        run.IsExpired(Now).Should().BeFalse();
    }

    [Fact]
    public void MarkRegistered_AfterExpiry_Throws()
    {
        var run = NewRun();
        _time.Advance(WizardSsoRun.DefaultDuration);

        var act = () => run.MarkRegistered("wysch", Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*expired*");
        run.State.Should().Be(WizardSsoRunState.Started);
    }

    [Fact]
    public void MarkSignedIn_AfterExpiry_StillRecordsTheAdministrator()
    {
        // The administrator was created before the run expired; the run must record it so the
        // provider gets enabled instead of leaving an administrator nobody can sign in as.
        var run = RegisteredRun();
        _time.Advance(WizardSsoRun.DefaultDuration + TimeSpan.FromSeconds(1));

        run.MarkSignedIn("admin", "admin@example.com", null);

        run.State.Should().Be(WizardSsoRunState.SignedIn);
        run.IsExpired(Now).Should().BeFalse();
    }

    [Fact]
    public void CustomDuration_IsUsedForExpiry()
    {
        var run = NewRun(TimeSpan.FromMinutes(2));

        _time.Advance(TimeSpan.FromMinutes(2));

        run.IsExpired(Now).Should().BeTrue();
    }

    #endregion

    #region Retry

    [Theory]
    [InlineData(WizardSsoFailure.Unreachable)]
    [InlineData(WizardSsoFailure.Cancelled)]
    [InlineData(WizardSsoFailure.LimitReached)]
    [InlineData(WizardSsoFailure.RegistrationFailed)]
    public void Retry_AfterPairingFailure_GoesBackToStarted(string reason)
    {
        var run = NewRun();
        run.PendingRegistrationState = "state";
        run.PendingRegistrationCode = "code";
        run.Fail(reason, "detail");

        run.Retry(Now);

        run.State.Should().Be(WizardSsoRunState.Started);
        run.FailureReason.Should().BeNull();
        run.FailureDetail.Should().BeNull();
        run.PendingRegistrationState.Should().BeNull();
        run.PendingRegistrationCode.Should().BeNull();
    }

    [Theory]
    [InlineData(WizardSsoFailure.SignInFailed)]
    [InlineData(WizardSsoFailure.EmailUnverified)]
    [InlineData(WizardSsoFailure.EmailInvalid)]
    public void Retry_AfterSignInFailureWithSavedProvider_GoesBackToRegistered(string reason)
    {
        var run = RegisteredRun();
        run.Fail(reason);

        run.Retry(Now);

        run.State.Should().Be(WizardSsoRunState.Registered);
        run.ProviderName.Should().Be("wysch");
        run.FailureReason.Should().BeNull();
    }

    [Theory]
    [InlineData(WizardSsoFailure.Unreachable)]
    [InlineData(WizardSsoFailure.Cancelled)]
    public void Retry_AfterNonSignInFailureWithSavedProvider_GoesBackToStarted(string reason)
    {
        var run = RegisteredRun();
        run.Fail(reason);

        run.Retry(Now);

        run.State.Should().Be(WizardSsoRunState.Started);
    }

    [Fact]
    public void Retry_AfterSignInFailureWithoutSavedProvider_GoesBackToStarted()
    {
        var run = NewRun();
        run.Fail(WizardSsoFailure.SignInFailed);

        run.Retry(Now);

        run.State.Should().Be(WizardSsoRunState.Started);
    }

    [Theory]
    [InlineData(WizardSsoFailure.Expired)]
    [InlineData(WizardSsoFailure.CompletedElsewhere)]
    public void Retry_AfterFinalFailure_Throws(string reason)
    {
        var run = RegisteredRun();
        run.Fail(reason);

        var act = () => run.Retry(Now);

        act.Should().Throw<InvalidOperationException>();
        run.State.Should().Be(WizardSsoRunState.Failed);
        run.FailureReason.Should().Be(reason);
    }

    [Fact]
    public void Retry_AfterExpiry_Throws()
    {
        var run = NewRun();
        run.Fail(WizardSsoFailure.Unreachable);
        _time.Advance(WizardSsoRun.DefaultDuration);

        var act = () => run.Retry(Now);

        act.Should().Throw<InvalidOperationException>();
        run.State.Should().Be(WizardSsoRunState.Failed);
        run.FailureReason.Should().Be(WizardSsoFailure.Unreachable);
    }

    [Fact]
    public void Retry_ThenCompleteRun_Succeeds()
    {
        var run = RegisteredRun();
        run.Fail(WizardSsoFailure.EmailUnverified);
        run.Retry(Now);

        run.MarkSignedIn("admin", "admin@example.com", null);

        run.State.Should().Be(WizardSsoRunState.SignedIn);
    }

    #endregion

    #region Failure classification

    [Theory]
    [InlineData(WizardSsoFailure.EmailUnverified, true)]
    [InlineData(WizardSsoFailure.EmailInvalid, true)]
    [InlineData(WizardSsoFailure.SignInFailed, true)]
    [InlineData(WizardSsoFailure.Unreachable, false)]
    [InlineData(WizardSsoFailure.Cancelled, false)]
    [InlineData(WizardSsoFailure.Expired, false)]
    [InlineData("unknown", false)]
    public void RetriesSignIn_OnlyForSignInFailures(string reason, bool expected)
    {
        WizardSsoFailure.RetriesSignIn(reason).Should().Be(expected);
    }

    [Theory]
    [InlineData(WizardSsoFailure.Expired, true)]
    [InlineData(WizardSsoFailure.CompletedElsewhere, true)]
    [InlineData(WizardSsoFailure.Unreachable, false)]
    [InlineData(WizardSsoFailure.SignInFailed, false)]
    [InlineData(WizardSsoFailure.LimitReached, false)]
    public void IsFinal_OnlyForExpiredAndCompletedElsewhere(string reason, bool expected)
    {
        WizardSsoFailure.IsFinal(reason).Should().Be(expected);
    }

    #endregion
}

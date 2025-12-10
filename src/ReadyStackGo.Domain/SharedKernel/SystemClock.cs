namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Provides a testable abstraction for system time.
/// Based on the .NET 8+ TimeProvider pattern, but simplified for Domain layer.
///
/// Usage in production: SystemClock.UtcNow (uses TimeProvider.System)
/// Usage in tests: SystemClock.SetProvider(fakeTimeProvider) in test setup
/// </summary>
public static class SystemClock
{
    private static TimeProvider _provider = TimeProvider.System;

    /// <summary>
    /// Gets the current UTC time.
    /// </summary>
    public static DateTime UtcNow => _provider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Gets the current UTC time as DateTimeOffset.
    /// </summary>
    public static DateTimeOffset UtcNowOffset => _provider.GetUtcNow();

    /// <summary>
    /// Sets the time provider. Use FakeTimeProvider from
    /// Microsoft.Extensions.TimeProvider.Testing for tests.
    /// </summary>
    /// <param name="provider">The time provider to use.</param>
    public static void SetProvider(TimeProvider provider)
    {
        _provider = provider ?? TimeProvider.System;
    }

    /// <summary>
    /// Resets to the default system time provider.
    /// Call this in test cleanup to avoid test pollution.
    /// </summary>
    public static void Reset()
    {
        _provider = TimeProvider.System;
    }

    /// <summary>
    /// Gets the underlying TimeProvider for advanced scenarios.
    /// </summary>
    public static TimeProvider Provider => _provider;
}

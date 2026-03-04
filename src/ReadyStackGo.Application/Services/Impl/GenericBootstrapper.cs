namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Default bootstrapper for the generic RSGO distribution.
/// No-op: the generic distribution requires no pre-seeded data.
/// All configuration is done through the wizard/onboarding flow.
/// </summary>
public sealed class GenericBootstrapper : IBootstrapper
{
    public Task BootstrapAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

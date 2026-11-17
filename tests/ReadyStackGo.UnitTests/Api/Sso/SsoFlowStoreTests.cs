using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ReadyStackGo.API.Endpoints.Sso;

namespace ReadyStackGo.UnitTests.Api.Sso;

public class SsoFlowStoreTests
{
    private static SsoFlowStore Create(string? seconds)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SsoFlowStore.LifetimeKey] = seconds })
            .Build();
        return new SsoFlowStore(new MemoryCache(new MemoryCacheOptions()), configuration);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("600")]
    [InlineData("6000")]
    public void Lifetime_IsTenMinutes_UnlessShortened(string? seconds)
    {
        Create(seconds).StateLifetime.Should().Be(TimeSpan.FromMinutes(10), "the specification allows at most 10 minutes");
    }

    [Fact]
    public void Lifetime_CanBeShortenedForTests()
    {
        Create("2").StateLifetime.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TakeRegistration_IsOneTime()
    {
        var store = Create(null);
        store.PutRegistration("s1", new RegistrationFlowState(RegistrationFlowOwner.WizardRun, "run-1", "secret"));

        store.TakeRegistration("s1").Should().NotBeNull();
        store.TakeRegistration("s1").Should().BeNull();
    }
}

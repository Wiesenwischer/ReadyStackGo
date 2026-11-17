using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Application.Maintenance;

/// <summary>
/// Unit tests for MaintenanceObserverStateStore — the state that has to outlive a single check cycle.
/// Before it existed, the state lived on the scoped observer service and was discarded every cycle,
/// which silently defeated the configured pollingInterval.
/// </summary>
public class MaintenanceObserverStateStoreTests
{
    private readonly MaintenanceObserverStateStore _store = new();
    private readonly ProductDeploymentId _id = new(Guid.NewGuid());

    private static MaintenanceObserverConfig Config(
        string propertyName = "app.MaintenanceMode",
        int intervalSeconds = 30)
        => MaintenanceObserverConfig.Create(
            ObserverType.SqlExtendedProperty,
            TimeSpan.FromSeconds(intervalSeconds),
            maintenanceValue: "1",
            normalValue: "0",
            SqlObserverSettings.ForExtendedProperty(propertyName, "Server=sql01;Database=AppDb"));

    private static IMaintenanceObserver Observer() => new Mock<IMaintenanceObserver>().Object;

    #region Observer caching

    [Fact]
    public void GetOrCreateObserver_SameConfig_CreatesOnlyOnce()
    {
        var created = 0;

        var first = _store.GetOrCreateObserver(_id, Config(), _ => { created++; return Observer(); });
        var second = _store.GetOrCreateObserver(_id, Config(), _ => { created++; return Observer(); });

        created.Should().Be(1);
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOrCreateObserver_ChangedConfig_CreatesAReplacement()
    {
        var first = _store.GetOrCreateObserver(_id, Config(), _ => Observer());

        var second = _store.GetOrCreateObserver(_id, Config(propertyName: "other.Flag"), _ => Observer());

        second.Should().NotBeSameAs(first,
            "a manifest change picked up by a redeploy must take effect without restarting RSGO");
        _store.GetConfig(_id)!.Settings.Should().Be(
            SqlObserverSettings.ForExtendedProperty("other.Flag", "Server=sql01;Database=AppDb"));
    }

    [Fact]
    public void GetOrCreateObserver_ChangedConfig_DiscardsTheResultOfThePreviousConfig()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        _store.RecordResult(_id, ObserverResult.MaintenanceRequired("1"));

        _store.GetOrCreateObserver(_id, Config(propertyName: "other.Flag"), _ => Observer());

        _store.GetLastResult(_id).Should().BeNull();
    }

    [Fact]
    public void GetOrCreateObserver_ChangedConfig_KeepsTheCheckTimestamp()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        _store.RecordResult(_id, ObserverResult.NormalOperation("0"));

        _store.GetOrCreateObserver(_id, Config(propertyName: "other.Flag"), _ => Observer());

        _store.ShouldCheck(_id, TimeSpan.FromHours(1)).Should().BeFalse(
            "a config change must not become a way to read the product database more often than its interval");
    }

    [Fact]
    public void GetOrCreateObserver_DifferentProducts_AreIndependent()
    {
        var other = new ProductDeploymentId(Guid.NewGuid());

        var first = _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        var second = _store.GetOrCreateObserver(other, Config(), _ => Observer());

        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void GetOrCreateObserver_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _store.GetOrCreateObserver(_id, null!, _ => Observer()));
        Assert.Throws<ArgumentNullException>(() => _store.GetOrCreateObserver(_id, Config(), null!));
    }

    #endregion

    #region Polling interval

    [Fact]
    public void ShouldCheck_NeverChecked_ReturnsTrue()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());

        _store.ShouldCheck(_id, TimeSpan.FromHours(1)).Should().BeTrue();
    }

    [Fact]
    public void ShouldCheck_UnknownProduct_ReturnsTrue()
    {
        _store.ShouldCheck(new ProductDeploymentId(Guid.NewGuid()), TimeSpan.FromHours(1))
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldCheck_WithinTheInterval_ReturnsFalse()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        _store.RecordResult(_id, ObserverResult.NormalOperation("0"));

        _store.ShouldCheck(_id, TimeSpan.FromHours(1)).Should().BeFalse();
    }

    [Fact]
    public void ShouldCheck_ZeroInterval_AlwaysReturnsTrue()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        _store.RecordResult(_id, ObserverResult.NormalOperation("0"));

        _store.ShouldCheck(_id, TimeSpan.Zero).Should().BeTrue();
    }

    #endregion

    #region Results

    [Fact]
    public void RecordResult_IsReadableBack()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        var result = ObserverResult.MaintenanceRequired("database-exclusive (ONLINE/SINGLE_USER)");

        _store.RecordResult(_id, result);

        _store.GetLastResult(_id).Should().Be(result);
    }

    [Fact]
    public void RecordResult_WithoutAnObserver_IsIgnored()
    {
        _store.RecordResult(_id, ObserverResult.MaintenanceRequired("1"));

        _store.GetLastResult(_id).Should().BeNull("a forgotten product must not be resurrected");
        _store.GetConfig(_id).Should().BeNull();
    }

    [Fact]
    public void RecordResult_Null_Throws()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());

        Assert.Throws<ArgumentNullException>(() => _store.RecordResult(_id, null!));
    }

    [Fact]
    public void GetLastResult_UnknownProduct_ReturnsNull()
    {
        _store.GetLastResult(new ProductDeploymentId(Guid.NewGuid())).Should().BeNull();
    }

    #endregion

    #region Forget

    [Fact]
    public void Forget_DropsEverythingForTheProduct()
    {
        _store.GetOrCreateObserver(_id, Config(), _ => Observer());
        _store.RecordResult(_id, ObserverResult.NormalOperation("0"));

        _store.Forget(_id);

        _store.GetLastResult(_id).Should().BeNull();
        _store.GetConfig(_id).Should().BeNull();
        _store.ShouldCheck(_id, TimeSpan.FromHours(1)).Should().BeTrue();
    }

    [Fact]
    public void Forget_UnknownProduct_IsANoOp()
    {
        var act = () => _store.Forget(new ProductDeploymentId(Guid.NewGuid()));

        act.Should().NotThrow();
    }

    #endregion
}

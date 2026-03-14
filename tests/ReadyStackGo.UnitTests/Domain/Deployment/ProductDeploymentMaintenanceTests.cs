using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ProductDeployment maintenance mode behavior.
/// Tests trigger-based ownership rules at the product level.
/// </summary>
public class ProductDeploymentMaintenanceTests
{
    private readonly ProductDeploymentId _productDeploymentId = new(Guid.NewGuid());
    private readonly EnvironmentId _environmentId = new(Guid.NewGuid());
    private readonly UserId _userId = UserId.NewId();

    private ProductDeployment CreateRunningProductDeployment()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("db", "Database", "source:product:db:1.0", 1, new Dictionary<string, string>()),
            new("api", "API Server", "source:product:api:1.0", 2, new Dictionary<string, string>())
        };

        var pd = ProductDeployment.InitiateDeployment(
            _productDeploymentId,
            _environmentId,
            "product-group",
            "product-id",
            "test-product",
            "Test Product",
            "1.0.0",
            _userId,
            "test-deploy",
            stackConfigs,
            new Dictionary<string, string>());

        // Complete all stacks to reach Running status
        pd.StartStack("db", new DeploymentId(Guid.NewGuid()));
        pd.CompleteStack("db");
        pd.StartStack("api", new DeploymentId(Guid.NewGuid()));
        pd.CompleteStack("api");

        return pd;
    }

    #region Initial State

    [Fact]
    public void NewProductDeployment_HasNormalOperationMode()
    {
        var pd = CreateRunningProductDeployment();

        pd.OperationMode.Should().Be(OperationMode.Normal);
        pd.MaintenanceTrigger.Should().BeNull();
        pd.MaintenanceObserverConfig.Should().BeNull();
    }

    #endregion

    #region Enter Maintenance

    [Fact]
    public void EnterMaintenance_Manual_FromRunning_Succeeds()
    {
        var pd = CreateRunningProductDeployment();

        pd.EnterMaintenance(MaintenanceTrigger.Manual("Scheduled maintenance", "admin"));

        pd.OperationMode.Should().Be(OperationMode.Maintenance);
        pd.MaintenanceTrigger.Should().NotBeNull();
        pd.MaintenanceTrigger!.IsManual.Should().BeTrue();
        pd.MaintenanceTrigger.Reason.Should().Be("Scheduled maintenance");
        pd.MaintenanceTrigger.TriggeredBy.Should().Be("admin");
        pd.Status.Should().Be(ProductDeploymentStatus.Running);
    }

    [Fact]
    public void EnterMaintenance_Observer_FromRunning_Succeeds()
    {
        var pd = CreateRunningProductDeployment();

        pd.EnterMaintenance(MaintenanceTrigger.Observer("DB maintenance detected", "SqlObserver"));

        pd.OperationMode.Should().Be(OperationMode.Maintenance);
        pd.MaintenanceTrigger!.IsObserver.Should().BeTrue();
        pd.MaintenanceTrigger.TriggeredBy.Should().Be("SqlObserver");
    }

    [Fact]
    public void EnterMaintenance_FromDeploying_Throws()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("db", "Database", "source:product:db:1.0", 1, new Dictionary<string, string>())
        };

        var pd = ProductDeployment.InitiateDeployment(
            _productDeploymentId, _environmentId, "group", "id", "name", "Name", "1.0.0",
            _userId, "deploy", stackConfigs, new Dictionary<string, string>());

        var act = () => pd.EnterMaintenance(MaintenanceTrigger.Manual("test"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*operational*");
    }

    [Fact]
    public void EnterMaintenance_WhenAlreadyInMaintenance_Throws()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Manual("First"));

        var act = () => pd.EnterMaintenance(MaintenanceTrigger.Manual("Second"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*already*maintenance*");
    }

    [Fact]
    public void EnterMaintenance_NullTrigger_Throws()
    {
        var pd = CreateRunningProductDeployment();

        var act = () => pd.EnterMaintenance(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EnterMaintenance_FromStopped_Throws()
    {
        var pd = CreateRunningProductDeployment();
        pd.MarkAsStopped("User stopped");

        var act = () => pd.EnterMaintenance(MaintenanceTrigger.Manual("test"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*operational*");
    }

    [Fact]
    public void EnterMaintenance_RaisesProductMaintenanceModeChangedEvent()
    {
        var pd = CreateRunningProductDeployment();
        pd.ClearDomainEvents();

        pd.EnterMaintenance(MaintenanceTrigger.Manual("Test reason"));

        pd.DomainEvents.Should().ContainSingle(e => e is ProductMaintenanceModeChanged);
        var evt = pd.DomainEvents.OfType<ProductMaintenanceModeChanged>().First();
        evt.NewMode.Should().Be(OperationMode.Maintenance);
        evt.Trigger.Should().NotBeNull();
        evt.Trigger!.IsManual.Should().BeTrue();
    }

    #endregion

    #region Exit Maintenance — Ownership Rules

    [Fact]
    public void ExitMaintenance_Manual_FromManualMaintenance_Succeeds()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Manual("Test"));

        pd.ExitMaintenance(MaintenanceTriggerSource.Manual);

        pd.OperationMode.Should().Be(OperationMode.Normal);
        pd.MaintenanceTrigger.Should().BeNull();
    }

    [Fact]
    public void ExitMaintenance_Observer_FromObserverMaintenance_Succeeds()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Observer("External"));

        pd.ExitMaintenance(MaintenanceTriggerSource.Observer);

        pd.OperationMode.Should().Be(OperationMode.Normal);
        pd.MaintenanceTrigger.Should().BeNull();
    }

    [Fact]
    public void ExitMaintenance_Manual_FromObserverMaintenance_Throws()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Observer("External maintenance"));

        var act = () => pd.ExitMaintenance(MaintenanceTriggerSource.Manual);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*observer*");
    }

    [Fact]
    public void ExitMaintenance_Observer_FromManualMaintenance_Throws()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Manual("User maintenance"));

        var act = () => pd.ExitMaintenance(MaintenanceTriggerSource.Observer);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*manually activated*");
    }

    [Fact]
    public void ExitMaintenance_WhenNotInMaintenance_Throws()
    {
        var pd = CreateRunningProductDeployment();

        var act = () => pd.ExitMaintenance(MaintenanceTriggerSource.Manual);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not in maintenance*");
    }

    [Fact]
    public void ExitMaintenance_RaisesProductMaintenanceModeChangedEvent()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Manual("Test"));
        pd.ClearDomainEvents();

        pd.ExitMaintenance(MaintenanceTriggerSource.Manual);

        pd.DomainEvents.Should().ContainSingle(e => e is ProductMaintenanceModeChanged);
        var evt = pd.DomainEvents.OfType<ProductMaintenanceModeChanged>().First();
        evt.NewMode.Should().Be(OperationMode.Normal);
    }

    #endregion

    #region Query Properties

    [Fact]
    public void CanEnterMaintenance_WhenRunningAndNormal_ReturnsTrue()
    {
        var pd = CreateRunningProductDeployment();

        pd.CanEnterMaintenance.Should().BeTrue();
    }

    [Fact]
    public void CanEnterMaintenance_WhenInMaintenance_ReturnsFalse()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Manual());

        pd.CanEnterMaintenance.Should().BeFalse();
    }

    [Fact]
    public void CanExitMaintenance_WhenInMaintenance_ReturnsTrue()
    {
        var pd = CreateRunningProductDeployment();
        pd.EnterMaintenance(MaintenanceTrigger.Manual());

        pd.CanExitMaintenance.Should().BeTrue();
    }

    [Fact]
    public void CanExitMaintenance_WhenNormal_ReturnsFalse()
    {
        var pd = CreateRunningProductDeployment();

        pd.CanExitMaintenance.Should().BeFalse();
    }

    #endregion

    #region Observer Config

    [Fact]
    public void SetMaintenanceObserverConfig_StoresConfig()
    {
        var pd = CreateRunningProductDeployment();
        var config = MaintenanceObserverConfig.Create(
            ObserverType.Http,
            TimeSpan.FromSeconds(30),
            "maintenance",
            "normal",
            HttpObserverSettings.Create("https://example.com/status"));

        pd.SetMaintenanceObserverConfig(config);

        pd.MaintenanceObserverConfig.Should().Be(config);
    }

    [Fact]
    public void SetMaintenanceObserverConfig_Null_ClearsConfig()
    {
        var pd = CreateRunningProductDeployment();
        pd.SetMaintenanceObserverConfig(MaintenanceObserverConfig.Create(
            ObserverType.Http, TimeSpan.FromSeconds(30), "m", null,
            HttpObserverSettings.Create("https://example.com")));

        pd.SetMaintenanceObserverConfig(null);

        pd.MaintenanceObserverConfig.Should().BeNull();
    }

    #endregion

    #region Interaction with Lifecycle

    [Fact]
    public void MaintenanceMode_DoesNotAffectProductStatus()
    {
        var pd = CreateRunningProductDeployment();

        pd.EnterMaintenance(MaintenanceTrigger.Manual("Window"));

        pd.Status.Should().Be(ProductDeploymentStatus.Running);
        pd.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void EnterMaintenance_FromPartiallyRunning_Succeeds()
    {
        var stackConfigs = new List<StackDeploymentConfig>
        {
            new("db", "Database", "source:product:db:1.0", 1, new Dictionary<string, string>()),
            new("api", "API", "source:product:api:1.0", 2, new Dictionary<string, string>())
        };

        var pd = ProductDeployment.InitiateDeployment(
            _productDeploymentId, _environmentId, "group", "id", "name", "Name", "1.0.0",
            _userId, "deploy", stackConfigs, new Dictionary<string, string>());

        pd.StartStack("db", new DeploymentId(Guid.NewGuid()));
        pd.CompleteStack("db");
        pd.StartStack("api", new DeploymentId(Guid.NewGuid()));
        pd.FailStack("api", "Connection failed");
        pd.MarkAsPartiallyRunning("1 of 2 stacks failed");

        pd.EnterMaintenance(MaintenanceTrigger.Manual("Fix API"));

        pd.OperationMode.Should().Be(OperationMode.Maintenance);
        pd.Status.Should().Be(ProductDeploymentStatus.PartiallyRunning);
    }

    #endregion
}

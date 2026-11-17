using FluentAssertions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class MaintenanceSetterConfigMapperTests
{
    private static readonly Dictionary<string, string> NoVars = new();

    [Fact]
    public void Map_Null_ReturnsNull()
    {
        MaintenanceSetterConfigMapper.Map(null, NoVars).Should().BeNull();
    }

    [Fact]
    public void Map_UnknownType_ReturnsNull()
    {
        var src = new RsgoMaintenanceSetter { Type = "carrierPigeon", PropertyName = "x", ConnectionString = "y" };
        MaintenanceSetterConfigMapper.Map(src, NoVars).Should().BeNull();
    }

    [Fact]
    public void Map_Sql_MapsConnectionPropertyValuesAndGrace()
    {
        var src = new RsgoMaintenanceSetter
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "Server=db;Database=ams;",
            PropertyName = "ams-MaintenanceMode",
            MaintenanceValue = "1",
            NormalValue = "0",
            GracePeriod = "15s"
        };

        var config = MaintenanceSetterConfigMapper.Map(src, NoVars);

        config.Should().NotBeNull();
        config!.Type.Should().Be(SetterType.SqlExtendedProperty);
        config.MaintenanceValue.Should().Be("1");
        config.NormalValue.Should().Be("0");
        config.GracePeriod.Should().Be(TimeSpan.FromSeconds(15));
        var sql = config.Settings.Should().BeOfType<SqlSetterSettings>().Subject;
        sql.ConnectionString.Should().Be("Server=db;Database=ams;");
        sql.PropertyName.Should().Be("ams-MaintenanceMode");
    }

    [Fact]
    public void Map_Sql_ResolvesConnectionNameFromVariables()
    {
        var src = new RsgoMaintenanceSetter
        {
            Type = "sqlExtendedProperty",
            ConnectionName = "AMS_DB",
            PropertyName = "ams-MaintenanceMode"
        };
        var vars = new Dictionary<string, string> { ["AMS_DB"] = "Server=db;Database=ams;" };

        var config = MaintenanceSetterConfigMapper.Map(src, vars);

        config.Should().NotBeNull();
        ((SqlSetterSettings)config!.Settings).ConnectionString.Should().Be("Server=db;Database=ams;");
    }

    [Fact]
    public void Map_Sql_UnresolvedVariable_ReturnsNull()
    {
        var src = new RsgoMaintenanceSetter
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "Server=${MISSING};",
            PropertyName = "p"
        };

        MaintenanceSetterConfigMapper.Map(src, NoVars).Should().BeNull();
    }

    [Fact]
    public void Map_Sql_MissingPropertyName_ReturnsNull()
    {
        var src = new RsgoMaintenanceSetter { Type = "sqlExtendedProperty", ConnectionString = "Server=db;" };
        MaintenanceSetterConfigMapper.Map(src, NoVars).Should().BeNull();
    }

    [Fact]
    public void Map_Webhook_MapsUrlSecretTimeoutRetries()
    {
        var src = new RsgoMaintenanceSetter
        {
            Type = "webhook",
            Url = "https://product.example.com/maintenance",
            Secret = "s3cr3t",
            Timeout = "5s",
            Retries = 3
        };

        var config = MaintenanceSetterConfigMapper.Map(src, NoVars);

        config.Should().NotBeNull();
        config!.Type.Should().Be(SetterType.Webhook);
        var wh = config.Settings.Should().BeOfType<WebhookSetterSettings>().Subject;
        wh.Url.Should().Be("https://product.example.com/maintenance");
        wh.Secret.Should().Be("s3cr3t");
        wh.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        wh.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void Map_Webhook_DefaultsTimeoutAndRetries()
    {
        var src = new RsgoMaintenanceSetter { Type = "webhook", Url = "https://x/y" };

        var config = MaintenanceSetterConfigMapper.Map(src, NoVars);

        var wh = (WebhookSetterSettings)config!.Settings;
        wh.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        wh.MaxRetries.Should().Be(2);
        config.GracePeriod.Should().Be(TimeSpan.Zero);
    }
}

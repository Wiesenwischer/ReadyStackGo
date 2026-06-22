using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Locks the stable, versioned machine-readable status contract (Phase 5): shape, schema
/// version, the running/maintenance/deploying states, and the planned-maintenance (flag) vs
/// redeploy (deploy-state) distinction. Exercises the full resolver → status-JSON pipeline.
/// </summary>
public class EdgeStatusContractTests
{
    private static JsonElement Status(ProductDeploymentStatus status, OperationMode mode,
        MaintenanceTrigger? trigger = null, string? version = "1.2.3")
    {
        var desired = EdgeStateResolver.Resolve(status, mode, trigger, version);
        return JsonDocument.Parse(CaddyConfigBuilder.BuildStatusJson(desired)).RootElement;
    }

    [Fact]
    public void Shape_HasExactlyTheContractKeys_AndVersionedSchema()
    {
        var status = Status(ProductDeploymentStatus.Running, OperationMode.Normal);

        status.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo(new[] { "schema", "state", "reason", "until", "productVersion" });
        status.GetProperty("schema").GetInt32().Should().Be(EdgeStatusContract.SchemaVersion);
    }

    [Fact]
    public void Running_State()
    {
        var status = Status(ProductDeploymentStatus.Running, OperationMode.Normal);

        status.GetProperty("state").GetString().Should().Be("running");
        status.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);
        status.GetProperty("until").ValueKind.Should().Be(JsonValueKind.Null);
        status.GetProperty("productVersion").GetString().Should().Be("1.2.3");
    }

    [Fact]
    public void PlannedMaintenance_FromFlag_CarriesReason()
    {
        var status = Status(ProductDeploymentStatus.Running, OperationMode.Maintenance,
            MaintenanceTrigger.Manual("Scheduled DB upgrade", "admin"));

        status.GetProperty("state").GetString().Should().Be("maintenance");
        status.GetProperty("reason").GetString().Should().Be("Scheduled DB upgrade");
    }

    [Fact]
    public void Redeploy_IsDeploying_NotPlannedMaintenance()
    {
        var status = Status(ProductDeploymentStatus.Redeploying, OperationMode.Normal);

        status.GetProperty("state").GetString().Should().Be("deploying",
            "a redeploy is temporary unavailability, distinct from planned maintenance");
        status.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void AllStates_AreRobustlyParseableJson()
    {
        var cases = new[]
        {
            Status(ProductDeploymentStatus.Running, OperationMode.Normal),
            Status(ProductDeploymentStatus.Redeploying, OperationMode.Normal),
            Status(ProductDeploymentStatus.Failed, OperationMode.Normal),
            Status(ProductDeploymentStatus.Running, OperationMode.Maintenance, MaintenanceTrigger.Observer("ext"))
        };

        foreach (var s in cases)
            s.GetProperty("state").GetString().Should().BeOneOf("running", "maintenance", "deploying");
    }
}

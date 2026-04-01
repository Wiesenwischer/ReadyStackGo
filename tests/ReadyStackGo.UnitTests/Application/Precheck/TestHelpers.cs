using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Precheck;

internal static class TestHelpers
{
    public static StackDefinition CreateStackDefinition(
        IReadOnlyList<ServiceTemplate>? services = null,
        IEnumerable<Variable>? variables = null,
        IEnumerable<VolumeDefinition>? volumes = null,
        IEnumerable<NetworkDefinition>? networks = null)
    {
        return new StackDefinition(
            sourceId: "test-source",
            name: "test-stack",
            productId: new ProductId("test:test-stack"),
            services: services ?? [new ServiceTemplate { Name = "web", Image = "nginx:latest" }],
            variables: variables,
            volumes: volumes,
            networks: networks);
    }

    public static Deployment CreateDeployment(DeploymentStatus targetStatus)
    {
        var envId = EnvironmentId.FromGuid(Guid.NewGuid());
        var userId = new global::ReadyStackGo.Domain.Deployment.UserId(Guid.NewGuid());

        // Start as Installing, then transition to target status
        var deployment = Deployment.StartInstallation(
            DeploymentId.NewId(),
            envId,
            "test-source:test:test-stack:1.0:test-stack",
            "test-stack",
            "test-stack",
            userId);

        switch (targetStatus)
        {
            case DeploymentStatus.Installing:
                // Already in Installing
                break;
            case DeploymentStatus.Running:
                deployment.MarkAsRunning();
                break;
            case DeploymentStatus.Failed:
                deployment.MarkAsFailed("Test failure");
                break;
            case DeploymentStatus.Upgrading:
                deployment.MarkAsRunning();
                deployment.StartUpgradeProcess("2.0");
                break;
            case DeploymentStatus.Removed:
                deployment.MarkAsRunning();
                deployment.MarkAsRemoved();
                break;
        }

        return deployment;
    }
}

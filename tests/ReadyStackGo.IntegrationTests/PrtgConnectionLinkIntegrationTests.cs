using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Infrastructure.DataAccess;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Reproduces the "Save link" 500 — clicking the saved-PRTG-connection picker
/// on the deployment detail page returned Internal Server Error.
/// </summary>
public class PrtgConnectionLinkIntegrationTests : AuthenticatedTestBase
{
    [Fact]
    public async Task LinkPrtgConnection_OnExistingDeployment_DoesNotReturn500()
    {
        var connectionId = await CreatePrtgConnectionAsync("repro-prtg");
        var productDeploymentId = SeedProductDeployment();

        var response = await Client.PutAsJsonAsync(
            $"/api/deployments/{productDeploymentId}/prtg-connection",
            new { id = productDeploymentId, prtgConnectionId = connectionId });

        var body = await response.Content.ReadAsStringAsync();
        // Print whatever we got so a failing test surfaces the real reason.
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            $"server returned 500 — body: {body}");
        response.IsSuccessStatusCode.Should().BeTrue($"unexpected status {response.StatusCode} — body: {body}");
    }

    private async Task<Guid> CreatePrtgConnectionAsync(string name)
    {
        var req = new
        {
            name,
            url = "https://prtg.example.local",
            apiToken = "fake-api-token-for-test",
            templateDeviceId = (int?)null,
            verifyTls = true,
        };
        var response = await Client.PostAsJsonAsync("/api/prtg-connections", req);
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"connection create failed: {response.StatusCode} {body}");
        var dto = System.Text.Json.JsonDocument.Parse(body);
        var connectionElement = dto.RootElement.GetProperty("connection");
        return connectionElement.GetProperty("id").GetGuid();
    }

    private Guid SeedProductDeployment()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();

        var id = ProductDeploymentId.Create();
        var deployment = ProductDeployment.InitiateDeployment(
            id,
            new EnvironmentId(Guid.Parse(EnvironmentId)),
            productGroupId: "repro:product",
            productId: "repro:product:1.0.0",
            productName: "Repro Product",
            productDisplayName: "Repro Product",
            productVersion: "1.0.0",
            deployedBy: new UserId(Guid.NewGuid()),
            deploymentName: "repro-deployment",
            stackConfigs: new[]
            {
                new StackDeploymentConfig(
                    StackName: "repro-stack",
                    StackDisplayName: "Repro Stack",
                    StackId: "repro:stack:1.0.0",
                    ServiceCount: 1,
                    Variables: new Dictionary<string, string>()),
            },
            sharedVariables: new Dictionary<string, string>(),
            continueOnError: true);

        db.ProductDeployments.Add(deployment);
        db.SaveChanges();

        return id.Value;
    }
}

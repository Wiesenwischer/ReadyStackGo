using FluentAssertions;
using ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class GetProductDeploymentHandlerTests
{
    private static readonly string TestEnvironmentId = Guid.NewGuid().ToString();

    private static ProductDeployment CreateDeploymentWithStackVariables(
        params (string StackName, string StackId, Dictionary<string, string> Variables)[] stacks)
    {
        var stackConfigs = stacks
            .Select((s, i) => new StackDeploymentConfig(
                s.StackName, s.StackName, s.StackId, 1, s.Variables))
            .ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.Parse(TestEnvironmentId)),
            "group", "source:product", "product", "Product",
            "1.0.0",
            UserId.Create(),
            "test-deployment",
            stackConfigs,
            new Dictionary<string, string>());

        // Bring the aggregate to Running so it is a realistic "existing" deployment.
        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId());
            deployment.CompleteStack(stack.StackName);
        }

        return deployment;
    }

    [Fact]
    public void MapToResponse_CarriesPerStackVariablesIntoDto()
    {
        // Regression for #452: the Upgrade form pre-fills per-stack variables from
        // this response. If Variables are dropped, required-variable validation
        // wrongly blocks the upgrade even though the values were set at deploy time.
        var deployment = CreateDeploymentWithStackVariables(
            ("IdentityAccess", "source:product:IdentityAccess", new Dictionary<string, string>
            {
                ["DB_SERVER"] = "db.example.local",
                ["DB_NAME"] = "identity",
                ["DB_ADMIN_PASSWORD"] = "s3cret",
            }));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var stack = response.Stacks.Should().ContainSingle().Subject;
        stack.Variables.Should().Contain("DB_SERVER", "db.example.local");
        stack.Variables.Should().Contain("DB_NAME", "identity");
        stack.Variables.Should().Contain("DB_ADMIN_PASSWORD", "s3cret");
    }

    [Fact]
    public void MapToResponse_MapsVariablesPerStackIndependently()
    {
        var deployment = CreateDeploymentWithStackVariables(
            ("stack-a", "source:product:stack-a", new Dictionary<string, string> { ["A_VAR"] = "a" }),
            ("stack-b", "source:product:stack-b", new Dictionary<string, string> { ["B_VAR"] = "b" }));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var stackA = response.Stacks.Single(s => s.StackName == "stack-a");
        var stackB = response.Stacks.Single(s => s.StackName == "stack-b");

        stackA.Variables.Should().ContainKey("A_VAR").WhoseValue.Should().Be("a");
        stackA.Variables.Should().NotContainKey("B_VAR");
        stackB.Variables.Should().ContainKey("B_VAR").WhoseValue.Should().Be("b");
        stackB.Variables.Should().NotContainKey("A_VAR");
    }

    [Fact]
    public void MapToResponse_StackWithoutVariables_ReturnsEmptyDictionary()
    {
        var deployment = CreateDeploymentWithStackVariables(
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>()));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        response.Stacks.Should().ContainSingle()
            .Which.Variables.Should().NotBeNull().And.BeEmpty();
    }
}

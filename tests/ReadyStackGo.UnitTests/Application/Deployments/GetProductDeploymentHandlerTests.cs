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
        => CreateDeployment(new Dictionary<string, string>(), null, stacks);

    private static ProductDeployment CreateDeployment(
        Dictionary<string, string> sharedVariables,
        IEnumerable<string>? secretVariableNames,
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
            sharedVariables);

        if (secretVariableNames != null)
        {
            deployment.SetSecretVariableNames(secretVariableNames);
        }

        // Bring the aggregate to Running so it is a realistic "existing" deployment.
        foreach (var stack in deployment.GetStacksInDeployOrder())
        {
            deployment.StartStack(stack.StackName, DeploymentId.NewId());
            deployment.CompleteStack(stack.StackName);
        }

        return deployment;
    }

    private static DeploymentVariableDto Variable(
        IEnumerable<DeploymentVariableDto> variables, string name)
        => variables.Single(v => v.Name == name);

    #region Secret redaction

    [Fact]
    public void MapToResponse_SecretStackVariable_WithholdsTheValue()
    {
        // Regression for #465: a password must never appear in the response, no matter what the UI
        // renders — the value would otherwise be readable in the browser's network tab.
        var deployment = CreateDeployment(
            new Dictionary<string, string>(),
            secretVariableNames: new[] { "DB_ADMIN_PASSWORD" },
            ("IdentityAccess", "source:product:IdentityAccess", new Dictionary<string, string>
            {
                ["DB_SERVER"] = "db.example.local",
                ["DB_ADMIN_PASSWORD"] = "s3cret",
            }));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var secret = Variable(response.Stacks.Single().Variables, "DB_ADMIN_PASSWORD");
        secret.IsSecret.Should().BeTrue();
        secret.Value.Should().BeNull();
        secret.HasValue.Should().BeTrue("the upgrade form needs to know a value is stored");
    }

    [Fact]
    public void MapToResponse_SecretSharedVariable_WithholdsTheValue()
    {
        var deployment = CreateDeployment(
            new Dictionary<string, string>
            {
                ["DB_SERVER"] = "db.example.local",
                ["DB_RUNTIME_PASSWORD"] = "s3cret",
            },
            secretVariableNames: new[] { "DB_RUNTIME_PASSWORD" },
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>()));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        Variable(response.SharedVariables, "DB_RUNTIME_PASSWORD").Value.Should().BeNull();
        Variable(response.SharedVariables, "DB_SERVER").Value.Should().Be("db.example.local");
    }

    [Fact]
    public void MapToResponse_SecretWithoutStoredValue_ReportsNoValue()
    {
        // The user opted out of saving this password, so there is nothing to pre-fill and the
        // upgrade form must ask for it again.
        var deployment = CreateDeployment(
            new Dictionary<string, string> { ["DB_ADMIN_PASSWORD"] = string.Empty },
            secretVariableNames: new[] { "DB_ADMIN_PASSWORD" },
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>()));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var secret = Variable(response.SharedVariables, "DB_ADMIN_PASSWORD");
        secret.IsSecret.Should().BeTrue();
        secret.HasValue.Should().BeFalse();
    }

    [Theory]
    [InlineData("DB_ADMIN_PASSWORD")]
    [InlineData("dbPassword")]
    [InlineData("API_TOKEN")]
    [InlineData("CLIENT_SECRET")]
    [InlineData("DB_CONNECTION_STRING")]
    [InlineData("REGISTRY_PWD")]
    public void MapToResponse_NoRecordedSecrets_FallsBackToTheName(string variableName)
    {
        // Deployments created before the secret names were recorded must not leak either.
        var deployment = CreateDeployment(
            new Dictionary<string, string> { [variableName] = "s3cret" },
            secretVariableNames: null,
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>()));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var variable = Variable(response.SharedVariables, variableName);
        variable.IsSecret.Should().BeTrue();
        variable.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("DB_SERVER")]
    [InlineData("API_PORT")]
    [InlineData("SEED_HANDBOOK_EXAMPLES")]
    public void MapToResponse_HarmlessVariables_StayVisible(string variableName)
    {
        var deployment = CreateDeployment(
            new Dictionary<string, string> { [variableName] = "value" },
            secretVariableNames: null,
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>()));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var variable = Variable(response.SharedVariables, variableName);
        variable.IsSecret.Should().BeFalse();
        variable.Value.Should().Be("value");
    }

    [Fact]
    public void MapToResponse_NoSecretValueAppearsAnywhereInTheResponse()
    {
        // Belt and braces: whatever else the DTO grows, the secret must not be reachable.
        const string secretValue = "uniqueS3cretValue";
        var deployment = CreateDeployment(
            new Dictionary<string, string> { ["DB_RUNTIME_PASSWORD"] = secretValue },
            secretVariableNames: new[] { "DB_RUNTIME_PASSWORD" },
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>
            {
                ["DB_RUNTIME_PASSWORD"] = secretValue
            }));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        // global:: — the test tree has an Application.System namespace that shadows System here.
        var serialized = global::System.Text.Json.JsonSerializer.Serialize(response);
        serialized.Should().NotContain(secretValue);
    }

    #endregion

    #region Non-secret variables

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
            }));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        var stack = response.Stacks.Should().ContainSingle().Subject;
        Variable(stack.Variables, "DB_SERVER").Value.Should().Be("db.example.local");
        Variable(stack.Variables, "DB_NAME").Value.Should().Be("identity");
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

        Variable(stackA.Variables, "A_VAR").Value.Should().Be("a");
        stackA.Variables.Should().NotContain(v => v.Name == "B_VAR");
        Variable(stackB.Variables, "B_VAR").Value.Should().Be("b");
        stackB.Variables.Should().NotContain(v => v.Name == "A_VAR");
    }

    [Fact]
    public void MapToResponse_StackWithoutVariables_ReturnsEmptyList()
    {
        var deployment = CreateDeploymentWithStackVariables(
            ("stack-a", "source:product:stack-a", new Dictionary<string, string>()));

        var response = GetProductDeploymentHandler.MapToResponse(deployment);

        response.Stacks.Should().ContainSingle()
            .Which.Variables.Should().NotBeNull().And.BeEmpty();
    }

    #endregion
}

using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for the secret-variable classification on ProductDeployment (#465).
/// </summary>
public class ProductDeploymentSecretVariablesTests
{
    private static ProductDeployment CreateDeployment()
        => ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(),
            new EnvironmentId(Guid.NewGuid()),
            "group", "source:product", "product", "Product",
            "1.0.0",
            ReadyStackGo.Domain.Deployment.UserId.Create(),
            "test-deployment",
            new List<StackDeploymentConfig>
            {
                new("api", "API", "source:product:api", 1, new Dictionary<string, string>())
            },
            new Dictionary<string, string>());

    #region Recorded names

    [Fact]
    public void SetSecretVariableNames_RecordsTheNames()
    {
        var pd = CreateDeployment();

        pd.SetSecretVariableNames(new[] { "DB_ADMIN_PASSWORD", "DB_CONNECTION" });

        pd.SecretVariableNames.Should().BeEquivalentTo(new[] { "DB_ADMIN_PASSWORD", "DB_CONNECTION" });
        pd.IsSecretVariable("DB_ADMIN_PASSWORD").Should().BeTrue();
    }

    [Fact]
    public void SetSecretVariableNames_IsCaseInsensitive()
    {
        var pd = CreateDeployment();

        pd.SetSecretVariableNames(new[] { "DB_ADMIN_PASSWORD" });

        pd.IsSecretVariable("db_admin_password").Should().BeTrue();
    }

    [Fact]
    public void SetSecretVariableNames_ReplacesThePreviousSet()
    {
        // An upgrade re-records from the target version: a variable that is no longer a secret there
        // must stop being withheld.
        var pd = CreateDeployment();
        pd.SetSecretVariableNames(new[] { "OLD_TOKEN_VALUE" });

        pd.SetSecretVariableNames(new[] { "NEW_CREDENTIAL_VALUE" });

        pd.SecretVariableNames.Should().BeEquivalentTo(new[] { "NEW_CREDENTIAL_VALUE" });
    }

    [Fact]
    public void SetSecretVariableNames_Null_ClearsTheSet()
    {
        var pd = CreateDeployment();
        pd.SetSecretVariableNames(new[] { "SOME_VALUE" });

        pd.SetSecretVariableNames(null);

        pd.SecretVariableNames.Should().BeEmpty();
    }

    [Fact]
    public void SetSecretVariableNames_IgnoresBlankEntries()
    {
        var pd = CreateDeployment();

        pd.SetSecretVariableNames(new[] { "DB_ADMIN_PASSWORD", "", "   ", null! });

        pd.SecretVariableNames.Should().BeEquivalentTo(new[] { "DB_ADMIN_PASSWORD" });
    }

    #endregion

    #region Fallback for deployments without a recorded set

    [Theory]
    [InlineData("DB_ADMIN_PASSWORD")]
    [InlineData("SMTP_PASSWD")]
    [InlineData("REGISTRY_PWD")]
    [InlineData("CLIENT_SECRET")]
    [InlineData("API_TOKEN")]
    [InlineData("SOME_APIKEY")]
    [InlineData("AWS_ACCESS_KEY")]
    [InlineData("SIGNING_PRIVATE_KEY")]
    [InlineData("GIT_CREDENTIALS")]
    [InlineData("DB_CONNECTIONSTRING")]
    [InlineData("DB_CONNECTION_STRING")]
    [InlineData("PG_CONNSTR")]
    public void IsSecretVariable_WithoutRecordedSet_JudgesByName(string name)
    {
        var pd = CreateDeployment();

        pd.IsSecretVariable(name).Should().BeTrue(
            "deployments created before the names were recorded must not leak either");
    }

    [Theory]
    [InlineData("DB_SERVER")]
    [InlineData("API_PORT")]
    [InlineData("DB_NAME_PERSISTENCE")]
    [InlineData("SEED_HANDBOOK_EXAMPLES")]
    [InlineData("REDIS_PORT")]
    public void IsSecretVariable_HarmlessNames_AreNotWithheld(string name)
    {
        var pd = CreateDeployment();

        pd.IsSecretVariable(name).Should().BeFalse();
    }

    [Fact]
    public void IsSecretVariable_RecordedSetDoesNotDisableTheNameFallback()
    {
        // A variable missing from the recorded set (added later, or a definition that could not be
        // resolved) still gets the benefit of the doubt.
        var pd = CreateDeployment();
        pd.SetSecretVariableNames(new[] { "DB_CONNECTION" });

        pd.IsSecretVariable("UNRECORDED_PASSWORD").Should().BeTrue();
    }

    #endregion

    #region Type classification

    [Theory]
    [InlineData(VariableType.Password)]
    [InlineData(VariableType.ConnectionString)]
    [InlineData(VariableType.SqlServerConnectionString)]
    [InlineData(VariableType.PostgresConnectionString)]
    [InlineData(VariableType.MySqlConnectionString)]
    [InlineData(VariableType.EventStoreConnectionString)]
    [InlineData(VariableType.MongoConnectionString)]
    [InlineData(VariableType.RedisConnectionString)]
    public void IsSecret_CredentialBearingTypes_AreSecret(VariableType type)
    {
        type.IsSecret().Should().BeTrue();
    }

    [Theory]
    [InlineData(VariableType.String)]
    [InlineData(VariableType.Number)]
    [InlineData(VariableType.Boolean)]
    [InlineData(VariableType.Select)]
    [InlineData(VariableType.Port)]
    [InlineData(VariableType.Url)]
    [InlineData(VariableType.Email)]
    [InlineData(VariableType.Path)]
    [InlineData(VariableType.MultiLine)]
    public void IsSecret_OtherTypes_AreNotSecret(VariableType type)
    {
        type.IsSecret().Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NameSuggestsSecret_BlankName_IsFalse(string name)
    {
        VariableTypeSecrecy.NameSuggestsSecret(name).Should().BeFalse();
    }

    #endregion
}

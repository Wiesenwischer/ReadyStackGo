using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Infrastructure.Observers;

/// <summary>
/// Unit tests for SqlObserverConnection — the connection strings RSGO opens itself.
///
/// The load-bearing assertion is that pooling ends up disabled no matter what the manifest asked
/// for: a pooled observer session survives between polls, and a product waiting for all sessions to
/// close before taking its database exclusively then waits on RSGO forever.
/// </summary>
public class SqlObserverConnectionTests
{
    private const string Base = "Server=sql01;Database=AppDb;User Id=svc;Password=secret";

    #region Pooling

    [Fact]
    public void Normalize_DisablesPooling()
    {
        var result = new SqlConnectionStringBuilder(SqlObserverConnection.Normalize(Base));

        result.Pooling.Should().BeFalse();
    }

    [Theory]
    [InlineData("Pooling=true")]
    [InlineData("Pooling=True")]
    [InlineData("Min Pool Size=5;Max Pool Size=50")]
    public void Normalize_OverridesPoolingRequestedByTheManifest(string poolingOptions)
    {
        var result = new SqlConnectionStringBuilder(
            SqlObserverConnection.Normalize($"{Base};{poolingOptions}"));

        result.Pooling.Should().BeFalse(
            "a manifest must not be able to re-enable pooling for connections RSGO owns");
    }

    [Fact]
    public void ToMaster_DisablesPoolingAsWell()
    {
        var result = new SqlConnectionStringBuilder(
            SqlObserverConnection.ToMaster($"{Base};Pooling=true"));

        result.Pooling.Should().BeFalse();
    }

    #endregion

    #region Preserved settings

    [Fact]
    public void Normalize_PreservesServerCredentialsAndDatabase()
    {
        var result = new SqlConnectionStringBuilder(SqlObserverConnection.Normalize(Base));

        result.DataSource.Should().Be("sql01");
        result.InitialCatalog.Should().Be("AppDb");
        result.UserID.Should().Be("svc");
        result.Password.Should().Be("secret");
    }

    [Fact]
    public void Normalize_PreservesUnrelatedOptions()
    {
        var result = new SqlConnectionStringBuilder(
            SqlObserverConnection.Normalize($"{Base};TrustServerCertificate=true;Connect Timeout=7"));

        result.TrustServerCertificate.Should().BeTrue();
        result.ConnectTimeout.Should().Be(7);
    }

    #endregion

    #region Application name

    [Fact]
    public void Normalize_TagsTheSessionSoOperatorsCanIdentifyIt()
    {
        var result = new SqlConnectionStringBuilder(SqlObserverConnection.Normalize(Base));

        result.ApplicationName.Should().Be(SqlObserverConnection.ApplicationName);
    }

    [Fact]
    public void Normalize_UsesTheCallersTagWhenGiven()
    {
        var result = new SqlConnectionStringBuilder(
            SqlObserverConnection.Normalize(Base, "ReadyStackGo-ConnectionTest"));

        result.ApplicationName.Should().Be("ReadyStackGo-ConnectionTest");
        result.Pooling.Should().BeFalse();
    }

    [Fact]
    public void Normalize_KeepsAnApplicationNameChosenInTheManifest()
    {
        var result = new SqlConnectionStringBuilder(
            SqlObserverConnection.Normalize($"{Base};Application Name=CustomerTag"));

        result.ApplicationName.Should().Be("CustomerTag");
    }

    #endregion

    #region master rewrite

    [Fact]
    public void ToMaster_RedirectsToMasterButKeepsServerAndCredentials()
    {
        var result = new SqlConnectionStringBuilder(SqlObserverConnection.ToMaster(Base));

        result.InitialCatalog.Should().Be("master");
        result.DataSource.Should().Be("sql01");
        result.UserID.Should().Be("svc");
        result.Password.Should().Be("secret");
    }

    [Fact]
    public void ToMaster_WithoutInitialCatalog_StillTargetsMaster()
    {
        var result = new SqlConnectionStringBuilder(
            SqlObserverConnection.ToMaster("Server=sql01;Integrated Security=true"));

        result.InitialCatalog.Should().Be("master");
    }

    #endregion

    #region DatabaseName

    [Fact]
    public void DatabaseName_ReturnsInitialCatalog()
    {
        SqlObserverConnection.DatabaseName(Base).Should().Be("AppDb");
    }

    [Fact]
    public void DatabaseName_AcceptsInitialCatalogSpelling()
    {
        SqlObserverConnection.DatabaseName("Server=sql01;Initial Catalog=Other").Should().Be("Other");
    }

    [Fact]
    public void DatabaseName_WithoutDatabase_ReturnsNull()
    {
        SqlObserverConnection.DatabaseName("Server=sql01;Integrated Security=true").Should().BeNull();
    }

    #endregion

    #region Unparsable input

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not a connection string at all")]
    public void Normalize_UnusableInput_IsReturnedUnchanged(string connectionString)
    {
        // Failing here would hide the real problem; the connect attempt reports it instead.
        SqlObserverConnection.Normalize(connectionString).Should().Be(connectionString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("this is not a connection string at all")]
    public void DatabaseName_UnusableInput_ReturnsNull(string connectionString)
    {
        SqlObserverConnection.DatabaseName(connectionString).Should().BeNull();
    }

    [Fact]
    public void ToMaster_UnusableInput_IsReturnedUnchanged()
    {
        const string garbage = "not a connection string";

        SqlObserverConnection.ToMaster(garbage).Should().Be(garbage);
    }

    #endregion
}

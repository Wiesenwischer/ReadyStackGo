namespace ReadyStackGo.IntegrationTests.Infrastructure;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Infrastructure.DataAccess;

/// <summary>
/// Test fixture providing an isolated SQLite in-memory database for integration tests.
/// </summary>
public class SqliteTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public ReadyStackGoDbContext Context { get; }

    public SqliteTestFixture()
    {
        // Use a shared in-memory database that persists for the connection lifetime
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ReadyStackGoDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ReadyStackGoDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a new DbContext instance sharing the same database connection.
    /// Useful for testing repository behavior with fresh context instances.
    /// </summary>
    public ReadyStackGoDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<ReadyStackGoDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new ReadyStackGoDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}

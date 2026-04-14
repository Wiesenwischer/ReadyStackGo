using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReadyStackGo.Infrastructure.DataAccess;

/// <summary>
/// Design-time factory for EF Core tooling (dotnet-ef migrations).
/// Uses an in-memory SQLite connection so no real database file is required
/// when generating migrations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReadyStackGoDbContext>
{
    public ReadyStackGoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReadyStackGoDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new ReadyStackGoDbContext(options);
    }
}

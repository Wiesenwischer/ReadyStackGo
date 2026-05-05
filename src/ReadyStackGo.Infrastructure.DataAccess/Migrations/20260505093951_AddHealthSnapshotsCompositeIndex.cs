using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthSnapshotsCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF EXISTS / IF NOT EXISTS so the migration is safe against
            // legacy databases where the baseline schema may not have produced the
            // exact same set of indexes that the InitialCreate migration declares.
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_HealthSnapshots_EnvironmentId\";");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS " +
                "\"IX_HealthSnapshots_EnvironmentId_DeploymentId_CapturedAtUtc\" " +
                "ON \"HealthSnapshots\" (\"EnvironmentId\", \"DeploymentId\", \"CapturedAtUtc\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS " +
                "\"IX_HealthSnapshots_EnvironmentId_DeploymentId_CapturedAtUtc\";");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS " +
                "\"IX_HealthSnapshots_EnvironmentId\" " +
                "ON \"HealthSnapshots\" (\"EnvironmentId\");");
        }
    }
}

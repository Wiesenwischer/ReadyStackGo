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
            migrationBuilder.DropIndex(
                name: "IX_HealthSnapshots_EnvironmentId",
                table: "HealthSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_HealthSnapshots_EnvironmentId_DeploymentId_CapturedAtUtc",
                table: "HealthSnapshots",
                columns: new[] { "EnvironmentId", "DeploymentId", "CapturedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HealthSnapshots_EnvironmentId_DeploymentId_CapturedAtUtc",
                table: "HealthSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_HealthSnapshots_EnvironmentId",
                table: "HealthSnapshots",
                column: "EnvironmentId");
        }
    }
}

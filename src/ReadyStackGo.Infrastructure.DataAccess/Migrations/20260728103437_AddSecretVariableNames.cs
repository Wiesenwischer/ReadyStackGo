using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSecretVariableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default is an empty JSON array, not an empty string: the value converter deserializes
            // this column, and "" is not valid JSON. Existing deployments start with no recorded
            // secret names and fall back to name-based detection until their next deploy/upgrade.
            migrationBuilder.AddColumn<string>(
                name: "SecretVariableNamesJson",
                table: "ProductDeployments",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretVariableNamesJson",
                table: "ProductDeployments");
        }
    }
}

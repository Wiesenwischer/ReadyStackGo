using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOciRegistrySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistryPassword",
                table: "StackSources",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistryUrl",
                table: "StackSources",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistryUsername",
                table: "StackSources",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Repository",
                table: "StackSources",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagPattern",
                table: "StackSources",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistryPassword",
                table: "StackSources");

            migrationBuilder.DropColumn(
                name: "RegistryUrl",
                table: "StackSources");

            migrationBuilder.DropColumn(
                name: "RegistryUsername",
                table: "StackSources");

            migrationBuilder.DropColumn(
                name: "Repository",
                table: "StackSources");

            migrationBuilder.DropColumn(
                name: "TagPattern",
                table: "StackSources");
        }
    }
}

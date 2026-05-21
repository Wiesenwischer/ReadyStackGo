using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddInlinePrtgRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InlinePrtgEncryptedToken",
                table: "ProductDeployments",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InlinePrtgTemplateDeviceId",
                table: "ProductDeployments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InlinePrtgUrl",
                table: "ProductDeployments",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InlinePrtgVerifyTls",
                table: "ProductDeployments",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InlinePrtgEncryptedToken",
                table: "ProductDeployments");

            migrationBuilder.DropColumn(
                name: "InlinePrtgTemplateDeviceId",
                table: "ProductDeployments");

            migrationBuilder.DropColumn(
                name: "InlinePrtgUrl",
                table: "ProductDeployments");

            migrationBuilder.DropColumn(
                name: "InlinePrtgVerifyTls",
                table: "ProductDeployments");
        }
    }
}

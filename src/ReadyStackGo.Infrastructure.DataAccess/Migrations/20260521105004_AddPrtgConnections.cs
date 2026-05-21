using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddPrtgConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrtgConnectionId",
                table: "ProductDeployments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrtgDeviceId",
                table: "ProductDeployments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrtgLastSyncedAt",
                table: "ProductDeployments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrtgConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EncryptedApiToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TemplateDeviceId = table.Column<int>(type: "INTEGER", nullable: true),
                    VerifyTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrtgConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeployments_PrtgConnectionId",
                table: "ProductDeployments",
                column: "PrtgConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrtgConnections_OrganizationId",
                table: "PrtgConnections",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PrtgConnections_OrganizationId_Name",
                table: "PrtgConnections",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrtgConnections");

            migrationBuilder.DropIndex(
                name: "IX_ProductDeployments_PrtgConnectionId",
                table: "ProductDeployments");

            migrationBuilder.DropColumn(
                name: "PrtgConnectionId",
                table: "ProductDeployments");

            migrationBuilder.DropColumn(
                name: "PrtgDeviceId",
                table: "ProductDeployments");

            migrationBuilder.DropColumn(
                name: "PrtgLastSyncedAt",
                table: "ProductDeployments");
        }
    }
}

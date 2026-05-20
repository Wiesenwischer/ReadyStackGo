using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSnmpSettingsAndV3Users : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SnmpSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    ListenAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    RootOid = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Community = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TrapReceivers = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SnmpV3Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AuthProtocol = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthPassphraseEncrypted = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    PrivProtocol = table.Column<int>(type: "INTEGER", nullable: false),
                    PrivPassphraseEncrypted = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnmpV3Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SnmpV3Users_Name",
                table: "SnmpV3Users",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SnmpSettings");

            migrationBuilder.DropTable(
                name: "SnmpV3Users");
        }
    }
}

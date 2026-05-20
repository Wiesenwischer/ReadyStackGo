using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSnmpEngineIdAndBoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EngineBoots",
                table: "SnmpSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EngineIdHex",
                table: "SnmpSettings",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EngineBoots",
                table: "SnmpSettings");

            migrationBuilder.DropColumn(
                name: "EngineIdHex",
                table: "SnmpSettings");
        }
    }
}

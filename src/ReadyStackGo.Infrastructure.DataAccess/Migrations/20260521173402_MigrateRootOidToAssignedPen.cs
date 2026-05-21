using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <summary>
    /// IANA assigned ReadyStackGo Private Enterprise Number 65846 on 2026-05-21.
    /// Auto-migrate any RootOid still pointing at the previous placeholder
    /// PEN 99999 to the real assignment. Customers who explicitly set a custom
    /// RootOid (e.g. their own corporate PEN) are not touched.
    /// </summary>
    public partial class MigrateRootOidToAssignedPen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"SnmpSettings\" " +
                "SET \"RootOid\" = '1.3.6.1.4.1.65846.1' " +
                "WHERE \"RootOid\" = '1.3.6.1.4.1.99999.1';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"SnmpSettings\" " +
                "SET \"RootOid\" = '1.3.6.1.4.1.99999.1' " +
                "WHERE \"RootOid\" = '1.3.6.1.4.1.65846.1';");
        }
    }
}

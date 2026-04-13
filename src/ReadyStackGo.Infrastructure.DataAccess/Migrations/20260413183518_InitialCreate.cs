using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyStackGo.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StackId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StackName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StackVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeployedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentPhase = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    ProgressMessage = table.Column<string>(type: "TEXT", nullable: true),
                    IsCancellationRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    CancellationReason = table.Column<string>(type: "TEXT", nullable: true),
                    VariablesJson = table.Column<string>(type: "TEXT", nullable: false),
                    MaintenanceObserverConfigJson = table.Column<string>(type: "TEXT", nullable: true),
                    MaintenanceTriggerJson = table.Column<string>(type: "TEXT", nullable: true),
                    HealthCheckConfigsJson = table.Column<string>(type: "TEXT", nullable: false),
                    InitContainerResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpgradedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UpgradeCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Environments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectionConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    SshCredentialJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentVariables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentVariables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HealthSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StackName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OverallStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CurrentVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BusHealthJson = table.Column<string>(type: "TEXT", nullable: true),
                    InfraHealthJson = table.Column<string>(type: "TEXT", nullable: true),
                    SelfHealthJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductDeployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductGroupId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProductDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProductVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeploymentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DeployedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ContinueOnError = table.Column<bool>(type: "INTEGER", nullable: false),
                    SharedVariablesJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastUpgradedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpgradeCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OperationMode = table.Column<int>(type: "INTEGER", nullable: false),
                    MaintenanceTriggerJson = table.Column<string>(type: "TEXT", nullable: true),
                    MaintenanceObserverConfigJson = table.Column<string>(type: "TEXT", nullable: true),
                    PhaseHistoryJson = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDeployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Registries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ImagePatterns = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StackSources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FilePattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GitUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    GitBranch = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    GitUsername = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    GitPassword = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    GitSslVerify = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StackSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablementStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EnablementEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockReason = table.Column<string>(type: "TEXT", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    PasswordChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeployedServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContainerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Image = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployedServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployedServices_Deployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "Deployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentPhaseHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPhaseHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPhaseHistory_Deployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "Deployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductStackDeployments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StackName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StackDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StackId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DeploymentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeploymentStackName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsNewInUpgrade = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    VariablesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProductDeploymentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStackDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductStackDeployments_ProductDeployments_ProductDeploymentId",
                        column: x => x.ProductDeploymentId,
                        principalTable: "ProductDeployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScopeType = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OrganizationId",
                table: "ApiKeys",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_OrganizationId_Name",
                table: "ApiKeys",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeployedServices_DeploymentId",
                table: "DeployedServices",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPhaseHistory_DeploymentId",
                table: "DeploymentPhaseHistory",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_EnvironmentId",
                table: "Deployments",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_Status",
                table: "Deployments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_OrganizationId",
                table: "Environments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_OrganizationId_Name",
                table: "Environments",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariables_EnvironmentId",
                table: "EnvironmentVariables",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentVariables_EnvironmentId_Key",
                table: "EnvironmentVariables",
                columns: new[] { "EnvironmentId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthSnapshots_CapturedAtUtc",
                table: "HealthSnapshots",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HealthSnapshots_DeploymentId",
                table: "HealthSnapshots",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthSnapshots_DeploymentId_CapturedAtUtc",
                table: "HealthSnapshots",
                columns: new[] { "DeploymentId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthSnapshots_EnvironmentId",
                table: "HealthSnapshots",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Name",
                table: "Organizations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeployments_EnvironmentId",
                table: "ProductDeployments",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeployments_ProductGroupId",
                table: "ProductDeployments",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDeployments_Status",
                table: "ProductDeployments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStackDeployments_DeploymentId",
                table: "ProductStackDeployments",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStackDeployments_ProductDeploymentId",
                table: "ProductStackDeployments",
                column: "ProductDeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Registries_OrganizationId",
                table: "Registries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Registries_OrganizationId_IsDefault",
                table: "Registries",
                columns: new[] { "OrganizationId", "IsDefault" },
                filter: "[IsDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Registries_OrganizationId_Name",
                table: "Registries",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StackSources_Enabled",
                table: "StackSources",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_StackSources_Name",
                table: "StackSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StackSources_Type",
                table: "StackSources",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId_ScopeType_ScopeId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId", "ScopeType", "ScopeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "DeployedServices");

            migrationBuilder.DropTable(
                name: "DeploymentPhaseHistory");

            migrationBuilder.DropTable(
                name: "Environments");

            migrationBuilder.DropTable(
                name: "EnvironmentVariables");

            migrationBuilder.DropTable(
                name: "HealthSnapshots");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "ProductStackDeployments");

            migrationBuilder.DropTable(
                name: "Registries");

            migrationBuilder.DropTable(
                name: "StackSources");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Deployments");

            migrationBuilder.DropTable(
                name: "ProductDeployments");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

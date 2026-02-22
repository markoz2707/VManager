using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HyperV.CentralManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddContentLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedUtc",
                table: "UserAccounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserAccounts",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UserAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginUtc",
                table: "UserAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "AgentHosts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HypervisorVersion",
                table: "AgentHosts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "AgentHosts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AlertDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MetricName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    ThresholdValue = table.Column<double>(type: "double precision", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    EvaluationPeriods = table.Column<int>(type: "integer", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentLibraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLibraryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentLibraryItems_UserAccounts_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DrsConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutomationLevel = table.Column<int>(type: "integer", nullable: false),
                    CpuImbalanceThreshold = table.Column<int>(type: "integer", nullable: false),
                    MemoryImbalanceThreshold = table.Column<int>(type: "integer", nullable: false),
                    EvaluationIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    MinBenefitPercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrsConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrsConfigurations_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DrsRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DrsConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmInventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinationAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EstimatedBenefitPercent = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppliedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrsRecommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HaConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HeartbeatIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    FailureThreshold = table.Column<int>(type: "integer", nullable: false),
                    AdmissionControl = table.Column<bool>(type: "boolean", nullable: false),
                    ReservedCpuPercent = table.Column<int>(type: "integer", nullable: false),
                    ReservedMemoryPercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HaConfigurations_Clusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "Clusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HaEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentHostName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VmInventoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    VmName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricDataPoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmInventoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetricName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricDataPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MigrationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VmInventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinationAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    LiveMigration = table.Column<bool>(type: "boolean", nullable: false),
                    IncludeStorage = table.Column<bool>(type: "boolean", nullable: false),
                    InitiatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreCheckResults = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Configuration = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Resource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VmFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VmFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VmFolders_VmFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "VmFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentValue = table.Column<double>(type: "double precision", nullable: false),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentHostName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VmInventoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    VmName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FiredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcknowledgedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertInstances_AlertDefinitions_AlertDefinitionId",
                        column: x => x.AlertDefinitionId,
                        principalTable: "AlertDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentLibrarySubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncStatus = table.Column<int>(type: "integer", nullable: false),
                    LastSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SyncError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLibrarySubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentLibrarySubscriptions_AgentHosts_AgentHostId",
                        column: x => x.AgentHostId,
                        principalTable: "AgentHosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLibrarySubscriptions_ContentLibraryItems_LibraryItem~",
                        column: x => x.LibraryItemId,
                        principalTable: "ContentLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HaVmOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HaConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmInventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestartPriority = table.Column<int>(type: "integer", nullable: false),
                    RestartOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaVmOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HaVmOverrides_HaConfigurations_HaConfigurationId",
                        column: x => x.HaConfigurationId,
                        principalTable: "HaConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertNotificationChannels",
                columns: table => new
                {
                    AlertDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationChannelId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertNotificationChannels", x => new { x.AlertDefinitionId, x.NotificationChannelId });
                    table.ForeignKey(
                        name: "FK_AlertNotificationChannels_AlertDefinitions_AlertDefinitionId",
                        column: x => x.AlertDefinitionId,
                        principalTable: "AlertDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertNotificationChannels_NotificationChannels_Notification~",
                        column: x => x.NotificationChannelId,
                        principalTable: "NotificationChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_UserAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VmInventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentHostId = table.Column<Guid>(type: "uuid", nullable: false),
                    VmId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CpuCount = table.Column<int>(type: "integer", nullable: false),
                    MemoryMB = table.Column<long>(type: "bigint", nullable: false),
                    Environment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LastSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VmInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VmInventory_AgentHosts_AgentHostId",
                        column: x => x.AgentHostId,
                        principalTable: "AgentHosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VmInventory_VmFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "VmFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertInstances_AlertDefinitionId",
                table: "AlertInstances",
                column: "AlertDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertInstances_Status",
                table: "AlertInstances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotificationChannels_NotificationChannelId",
                table: "AlertNotificationChannels",
                column: "NotificationChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLibraryItems_Name",
                table: "ContentLibraryItems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLibraryItems_OwnerId",
                table: "ContentLibraryItems",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLibrarySubscriptions_AgentHostId_LibraryItemId",
                table: "ContentLibrarySubscriptions",
                columns: new[] { "AgentHostId", "LibraryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentLibrarySubscriptions_LibraryItemId",
                table: "ContentLibrarySubscriptions",
                column: "LibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DrsConfigurations_ClusterId",
                table: "DrsConfigurations",
                column: "ClusterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrsRecommendations_Status",
                table: "DrsRecommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HaConfigurations_ClusterId",
                table: "HaConfigurations",
                column: "ClusterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HaEvents_TimestampUtc",
                table: "HaEvents",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HaVmOverrides_HaConfigurationId_VmInventoryId",
                table: "HaVmOverrides",
                columns: new[] { "HaConfigurationId", "VmInventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetricDataPoints_AgentHostId_MetricName_TimestampUtc",
                table: "MetricDataPoints",
                columns: new[] { "AgentHostId", "MetricName", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricDataPoints_TimestampUtc",
                table: "MetricDataPoints",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationTasks_Status",
                table: "MigrationTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Resource_Action",
                table: "Permissions",
                columns: new[] { "Resource", "Action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VmFolders_ParentId",
                table: "VmFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_VmInventory_AgentHostId_VmId",
                table: "VmInventory",
                columns: new[] { "AgentHostId", "VmId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VmInventory_FolderId",
                table: "VmInventory",
                column: "FolderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertInstances");

            migrationBuilder.DropTable(
                name: "AlertNotificationChannels");

            migrationBuilder.DropTable(
                name: "ContentLibrarySubscriptions");

            migrationBuilder.DropTable(
                name: "DrsConfigurations");

            migrationBuilder.DropTable(
                name: "DrsRecommendations");

            migrationBuilder.DropTable(
                name: "HaEvents");

            migrationBuilder.DropTable(
                name: "HaVmOverrides");

            migrationBuilder.DropTable(
                name: "MetricDataPoints");

            migrationBuilder.DropTable(
                name: "MigrationTasks");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "VmInventory");

            migrationBuilder.DropTable(
                name: "AlertDefinitions");

            migrationBuilder.DropTable(
                name: "NotificationChannels");

            migrationBuilder.DropTable(
                name: "ContentLibraryItems");

            migrationBuilder.DropTable(
                name: "HaConfigurations");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "VmFolders");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "LastLoginUtc",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "AgentVersion",
                table: "AgentHosts");

            migrationBuilder.DropColumn(
                name: "HypervisorVersion",
                table: "AgentHosts");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "AgentHosts");
        }
    }
}

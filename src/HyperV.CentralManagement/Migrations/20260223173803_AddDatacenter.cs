using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HyperV.CentralManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddDatacenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DatacenterId",
                table: "Clusters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DatacenterId",
                table: "AgentHosts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Datacenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datacenters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clusters_DatacenterId",
                table: "Clusters",
                column: "DatacenterId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentHosts_DatacenterId",
                table: "AgentHosts",
                column: "DatacenterId");

            migrationBuilder.CreateIndex(
                name: "IX_Datacenters_Name",
                table: "Datacenters",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentHosts_Datacenters_DatacenterId",
                table: "AgentHosts",
                column: "DatacenterId",
                principalTable: "Datacenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Clusters_Datacenters_DatacenterId",
                table: "Clusters",
                column: "DatacenterId",
                principalTable: "Datacenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentHosts_Datacenters_DatacenterId",
                table: "AgentHosts");

            migrationBuilder.DropForeignKey(
                name: "FK_Clusters_Datacenters_DatacenterId",
                table: "Clusters");

            migrationBuilder.DropTable(
                name: "Datacenters");

            migrationBuilder.DropIndex(
                name: "IX_Clusters_DatacenterId",
                table: "Clusters");

            migrationBuilder.DropIndex(
                name: "IX_AgentHosts_DatacenterId",
                table: "AgentHosts");

            migrationBuilder.DropColumn(
                name: "DatacenterId",
                table: "Clusters");

            migrationBuilder.DropColumn(
                name: "DatacenterId",
                table: "AgentHosts");
        }
    }
}

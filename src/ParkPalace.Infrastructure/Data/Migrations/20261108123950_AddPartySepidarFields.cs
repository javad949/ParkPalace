using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkPalace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartySepidarFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Parties",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SepidarCode",
                table: "Parties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SepidarMappingVerified",
                table: "Parties",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SepidarPartyRef",
                table: "Parties",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SepidarTitle",
                table: "Parties",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TelephonyGroups",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SupervisorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupervisorExtension = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupervisorDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonyGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelephonyGroups_Users_SupervisorUserId",
                        column: x => x.SupervisorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TelephonyGroupMembers",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Extension = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSupervisor = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephonyGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelephonyGroupMembers_TelephonyGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "crm",
                        principalTable: "TelephonyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TelephonyGroupMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelephonyGroupMembers_GroupId_Extension",
                schema: "crm",
                table: "TelephonyGroupMembers",
                columns: new[] { "GroupId", "Extension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelephonyGroupMembers_UserId",
                schema: "crm",
                table: "TelephonyGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TelephonyGroups_SupervisorUserId",
                schema: "crm",
                table: "TelephonyGroups",
                column: "SupervisorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelephonyGroupMembers",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "TelephonyGroups",
                schema: "crm");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SepidarCode",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SepidarMappingVerified",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SepidarPartyRef",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SepidarTitle",
                table: "Parties");
        }
    }
}

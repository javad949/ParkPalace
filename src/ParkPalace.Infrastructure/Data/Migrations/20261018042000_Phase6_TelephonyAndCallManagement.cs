using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkPalace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_TelephonyAndCallManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallRecords",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UniqueId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LinkedId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CallerNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NormalizedCallerNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CallerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DestinationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DestinationChannel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnswerTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    BillableSeconds = table.Column<int>(type: "int", nullable: true),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsUnknownCaller = table.Column<bool>(type: "bit", nullable: false),
                    MatchConfidence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordingFileName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    RecordingFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordingDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    RecordingFileSize = table.Column<long>(type: "bigint", nullable: true),
                    HasRecording = table.Column<bool>(type: "bit", nullable: false),
                    CdrId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReconciliationStatus = table.Column<int>(type: "int", nullable: false),
                    DisconnectionCause = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallRecords_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallRecords_PartyContacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "PartyContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserExtensions",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserExtensions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CallAudits",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallAudits_CallRecords_CallRecordId",
                        column: x => x.CallRecordId,
                        principalSchema: "crm",
                        principalTable: "CallRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CallNotes",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NoteText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FollowUpRequired = table.Column<bool>(type: "bit", nullable: false),
                    FollowUpDueDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FollowUpAssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FollowUpStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallNotes_CallRecords_CallRecordId",
                        column: x => x.CallRecordId,
                        principalSchema: "crm",
                        principalTable: "CallRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallAudits_CallRecordId",
                schema: "crm",
                table: "CallAudits",
                column: "CallRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CallAudits_TimestampUtc",
                schema: "crm",
                table: "CallAudits",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CallNotes_CallRecordId",
                schema: "crm",
                table: "CallNotes",
                column: "CallRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CallNotes_FollowUpStatus",
                schema: "crm",
                table: "CallNotes",
                column: "FollowUpStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_ContactId",
                schema: "crm",
                table: "CallRecords",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_Extension",
                schema: "crm",
                table: "CallRecords",
                column: "Extension");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_LinkedId",
                schema: "crm",
                table: "CallRecords",
                column: "LinkedId");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_NormalizedCallerNumber",
                schema: "crm",
                table: "CallRecords",
                column: "NormalizedCallerNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_PartyId",
                schema: "crm",
                table: "CallRecords",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_StartTimeUtc",
                schema: "crm",
                table: "CallRecords",
                column: "StartTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_Status",
                schema: "crm",
                table: "CallRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CallRecords_UniqueId",
                schema: "crm",
                table: "CallRecords",
                column: "UniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExtensions_Extension",
                schema: "crm",
                table: "UserExtensions",
                column: "Extension",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExtensions_UserId",
                schema: "crm",
                table: "UserExtensions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallAudits",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "CallNotes",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "UserExtensions",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "CallRecords",
                schema: "crm");
        }
    }
}

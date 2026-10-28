using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkPalace.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase7_CatalogStandardizationAndDecoupledUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmed",
                table: "MaterialUnitConversions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "MaterialUnitConversions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategorizationStatus",
                table: "Materials",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NeedsReview");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsumptionUnitId",
                table: "Materials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustomizedByUser",
                table: "Materials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageSize",
                table: "Materials",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PackageSizeUnitId",
                table: "Materials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Packaging",
                table: "Materials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductFamily",
                table: "Materials",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseUnitId",
                table: "Materials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandardizationNotes",
                table: "Materials",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ConsumptionUnitId",
                table: "Materials",
                column: "ConsumptionUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_PackageSizeUnitId",
                table: "Materials",
                column: "PackageSizeUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_PurchaseUnitId",
                table: "Materials",
                column: "PurchaseUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_UnitsOfMeasure_ConsumptionUnitId",
                table: "Materials",
                column: "ConsumptionUnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_UnitsOfMeasure_PackageSizeUnitId",
                table: "Materials",
                column: "PackageSizeUnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_UnitsOfMeasure_PurchaseUnitId",
                table: "Materials",
                column: "PurchaseUnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Requirement 3: Guarantee that any legacy or unverified unit conversions remain unconfirmed unless explicit witness exists in Notes
            migrationBuilder.Sql(@"
                UPDATE [MaterialUnitConversions]
                SET [IsConfirmed] = 0
                WHERE [Notes] IS NULL OR ([Notes] NOT LIKE N'%تأیید%' AND [Notes] NOT LIKE N'%شاهد%' AND [Notes] NOT LIKE N'%verified%');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_UnitsOfMeasure_ConsumptionUnitId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_UnitsOfMeasure_PackageSizeUnitId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_UnitsOfMeasure_PurchaseUnitId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_ConsumptionUnitId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_PackageSizeUnitId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_PurchaseUnitId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "IsConfirmed",
                table: "MaterialUnitConversions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "MaterialUnitConversions");

            migrationBuilder.DropColumn(
                name: "CategorizationStatus",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ConsumptionUnitId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "IsCustomizedByUser",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "PackageSize",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "PackageSizeUnitId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "Packaging",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ProductFamily",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "StandardizationNotes",
                table: "Materials");
        }
    }
}

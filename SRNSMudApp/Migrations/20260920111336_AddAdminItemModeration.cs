using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20260920111336_AddAdminItemModeration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AdminHiddenAt",
            table: "Items",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminHiddenReason",
            table: "Items",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsAdminHidden",
            table: "Items",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_Items_IsAdminHidden",
            table: "Items",
            column: "IsAdminHidden");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Items_IsAdminHidden",
            table: "Items");

        migrationBuilder.DropColumn(
            name: "AdminHiddenAt",
            table: "Items");

        migrationBuilder.DropColumn(
            name: "AdminHiddenReason",
            table: "Items");

        migrationBuilder.DropColumn(
            name: "IsAdminHidden",
            table: "Items");
    }
}

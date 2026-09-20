using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20260920100603_AddUserBanStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BanReason",
            table: "AspNetUsers",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "BannedAt",
            table: "AspNetUsers",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsBanned",
            table: "AspNetUsers",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BanReason",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "BannedAt",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "IsBanned",
            table: "AspNetUsers");
    }
}

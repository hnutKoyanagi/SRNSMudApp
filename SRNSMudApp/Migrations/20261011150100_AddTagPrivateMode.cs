using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20261011150100_AddTagPrivateMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPrivate",
            table: "Tags",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "TargetUserGroupId",
            table: "Tags",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsAdminHidden",
            table: "Tags",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AdminHiddenAt",
            table: "Tags",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminHiddenReason",
            table: "Tags",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Tags_IsPrivate",
            table: "Tags",
            column: "IsPrivate");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_IsAdminHidden",
            table: "Tags",
            column: "IsAdminHidden");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_TargetUserGroupId",
            table: "Tags",
            column: "TargetUserGroupId");

        migrationBuilder.AddForeignKey(
            name: "FK_Tags_UserGroups_TargetUserGroupId",
            table: "Tags",
            column: "TargetUserGroupId",
            principalTable: "UserGroups",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Tags_UserGroups_TargetUserGroupId",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Tags_TargetUserGroupId",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Tags_IsAdminHidden",
            table: "Tags");

        migrationBuilder.DropIndex(
            name: "IX_Tags_IsPrivate",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "AdminHiddenReason",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "AdminHiddenAt",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "IsAdminHidden",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "TargetUserGroupId",
            table: "Tags");

        migrationBuilder.DropColumn(
            name: "IsPrivate",
            table: "Tags");
    }
}


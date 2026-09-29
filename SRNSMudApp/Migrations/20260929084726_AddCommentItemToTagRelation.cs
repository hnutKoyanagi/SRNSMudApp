using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20260929084726_AddCommentItemToTagRelation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Comment",
            table: "TagRelations");

        migrationBuilder.AddColumn<int>(
            name: "CommentItemId",
            table: "TagRelations",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TagRelations_CommentItemId",
            table: "TagRelations",
            column: "CommentItemId");

        migrationBuilder.AddForeignKey(
            name: "FK_TagRelations_Items_CommentItemId",
            table: "TagRelations",
            column: "CommentItemId",
            principalTable: "Items",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TagRelations_Items_CommentItemId",
            table: "TagRelations");

        migrationBuilder.DropIndex(
            name: "IX_TagRelations_CommentItemId",
            table: "TagRelations");

        migrationBuilder.DropColumn(
            name: "CommentItemId",
            table: "TagRelations");

        migrationBuilder.AddColumn<string>(
            name: "Comment",
            table: "TagRelations",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);
    }
}

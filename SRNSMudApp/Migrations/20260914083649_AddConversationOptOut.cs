using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20260914083649_AddConversationOptOut : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RootItemId",
            table: "Items",
            type: "int",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ConversationOptOuts",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                RootItemId = table.Column<int>(type: "int", nullable: false),
                CreatedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ConversationOptOuts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ConversationOptOuts_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ConversationOptOuts_Items_RootItemId",
                    column: x => x.RootItemId,
                    principalTable: "Items",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Items_RootItemId",
            table: "Items",
            column: "RootItemId");

        migrationBuilder.CreateIndex(
            name: "IX_ConversationOptOuts_RootItemId",
            table: "ConversationOptOuts",
            column: "RootItemId");

        migrationBuilder.CreateIndex(
            name: "IX_ConversationOptOuts_UserId_RootItemId",
            table: "ConversationOptOuts",
            columns: new[] { "UserId", "RootItemId" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Items_Items_RootItemId",
            table: "Items",
            column: "RootItemId",
            principalTable: "Items",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Items_Items_RootItemId",
            table: "Items");

        migrationBuilder.DropTable(
            name: "ConversationOptOuts");

        migrationBuilder.DropIndex(
            name: "IX_Items_RootItemId",
            table: "Items");

        migrationBuilder.DropColumn(
            name: "RootItemId",
            table: "Items");
    }
}

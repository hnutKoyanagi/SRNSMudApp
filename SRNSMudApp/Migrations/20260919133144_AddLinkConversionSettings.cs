using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20260919133144_AddLinkConversionSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsLinkConversionEnabled",
            table: "AspNetUsers",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<float>(
            name: "LinkConversionThreshold",
            table: "AspNetUsers",
            type: "real",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsLinkConversionEnabled",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "LinkConversionThreshold",
            table: "AspNetUsers");
    }
}

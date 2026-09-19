using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRNSMudApp.Migrations;

/// <inheritdoc />
public partial class _20260919133000_AddTagSuggestionThresholdsToUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<float>(
            name: "TagSuggestionStrongThreshold",
            table: "AspNetUsers",
            type: "real",
            nullable: true);

        migrationBuilder.AddColumn<float>(
            name: "TagSuggestionCandidateThreshold",
            table: "AspNetUsers",
            type: "real",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TagSuggestionStrongThreshold",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "TagSuggestionCandidateThreshold",
            table: "AspNetUsers");
    }
}


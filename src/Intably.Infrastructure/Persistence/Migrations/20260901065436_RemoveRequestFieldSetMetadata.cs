using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intably.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRequestFieldSetMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceFieldSetId",
                table: "TemplateRequestFields");

            migrationBuilder.DropColumn(
                name: "SourceFieldSetName",
                table: "TemplateRequestFields");

            migrationBuilder.DropColumn(
                name: "SourceFieldSetVersion",
                table: "TemplateRequestFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceFieldSetId",
                table: "TemplateRequestFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFieldSetName",
                table: "TemplateRequestFields",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceFieldSetVersion",
                table: "TemplateRequestFields",
                type: "int",
                nullable: true);
        }
    }
}

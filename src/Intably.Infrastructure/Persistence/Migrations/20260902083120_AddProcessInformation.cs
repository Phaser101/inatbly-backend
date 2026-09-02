using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intably.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSteps_ProcessStepGroups_ProcessStepGroupId",
                table: "ProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateSteps_TemplateStepGroups_TemplateStepGroupId",
                table: "TemplateSteps");

            migrationBuilder.DropIndex(
                name: "IX_TemplateSteps_TemplateVersionId",
                table: "TemplateSteps");

            migrationBuilder.DropIndex(
                name: "IX_TemplateStepGroups_TemplateVersionId_Name",
                table: "TemplateStepGroups");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TemplateRequestFields",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "LaunchInput");

            migrationBuilder.AddColumn<bool>(
                name: "Pinned",
                table: "TemplateRequestFields",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProducingTemplateStepId",
                table: "TemplateRequestFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ProcessRequestValues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "LaunchInput");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAtUtc",
                table: "ProcessRequestValues",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByDisplayName",
                table: "ProcessRequestValues",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModifiedByUserId",
                table: "ProcessRequestValues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionsJson",
                table: "ProcessRequestValues",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "Pinned",
                table: "ProcessRequestValues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProducingProcessStepId",
                table: "ProcessRequestValues",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProcessRequestValues",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(
                """
                WITH RankedTemplateFields AS
                (
                    SELECT
                        rfrg,
                        ROW_NUMBER() OVER (
                            PARTITION BY TemplateVersionId
                            ORDER BY [Order], rfrg) AS Position
                    FROM TemplateRequestFields
                )
                UPDATE fields
                SET Pinned = CASE WHEN ranked.Position <= 2 THEN 1 ELSE 0 END
                FROM TemplateRequestFields AS fields
                INNER JOIN RankedTemplateFields AS ranked
                    ON ranked.rfrg = fields.rfrg;

                UPDATE processValues
                SET
                    Kind = 'LaunchInput',
                    Pinned = templateFields.Pinned,
                    OptionsJson = COALESCE(
                        (
                            SELECT
                                '['
                                + STRING_AGG(
                                    CAST(
                                        '"' + STRING_ESCAPE(options.Value, 'json') + '"'
                                        AS nvarchar(max)),
                                    ',')
                                    WITHIN GROUP (ORDER BY options.[Order])
                                + ']'
                            FROM TemplateRequestFieldOptions AS options
                            WHERE options.RequestFieldId = templateFields.rfrg
                        ),
                        '[]')
                FROM ProcessRequestValues AS processValues
                INNER JOIN TemplateRequestFields AS templateFields
                    ON templateFields.rfrg = processValues.SourceRequestFieldId;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BeforeValue",
                table: "ProcessAuditEvents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AfterValue",
                table: "ProcessAuditEvents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TemplateSteps_TemplateVersionId_ptsrg",
                table: "TemplateSteps",
                columns: new[] { "TemplateVersionId", "ptsrg" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TemplateStepGroups_TemplateVersionId_ptsgrg",
                table: "TemplateStepGroups",
                columns: new[] { "TemplateVersionId", "ptsgrg" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProcessSteps_ProcessId_psrg",
                table: "ProcessSteps",
                columns: new[] { "ProcessId", "psrg" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProcessStepGroups_ProcessId_psgrg",
                table: "ProcessStepGroups",
                columns: new[] { "ProcessId", "psgrg" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_TemplateVersionId_TemplateStepGroupId",
                table: "TemplateSteps",
                columns: new[] { "TemplateVersionId", "TemplateStepGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateRequestFields_TemplateVersionId_ProducingTemplateStepId",
                table: "TemplateRequestFields",
                columns: new[] { "TemplateVersionId", "ProducingTemplateStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessId_ProcessStepGroupId",
                table: "ProcessSteps",
                columns: new[] { "ProcessId", "ProcessStepGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRequestValues_ModifiedByUserId",
                table: "ProcessRequestValues",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRequestValues_ProcessId_ProducingProcessStepId",
                table: "ProcessRequestValues",
                columns: new[] { "ProcessId", "ProducingProcessStepId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessRequestValues_ProcessSteps_ProcessId_ProducingProcessStepId",
                table: "ProcessRequestValues",
                columns: new[] { "ProcessId", "ProducingProcessStepId" },
                principalTable: "ProcessSteps",
                principalColumns: new[] { "ProcessId", "psrg" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessRequestValues_Users_ModifiedByUserId",
                table: "ProcessRequestValues",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "grg");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSteps_ProcessStepGroups_ProcessId_ProcessStepGroupId",
                table: "ProcessSteps",
                columns: new[] { "ProcessId", "ProcessStepGroupId" },
                principalTable: "ProcessStepGroups",
                principalColumns: new[] { "ProcessId", "psgrg" });

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateRequestFields_TemplateSteps_TemplateVersionId_ProducingTemplateStepId",
                table: "TemplateRequestFields",
                columns: new[] { "TemplateVersionId", "ProducingTemplateStepId" },
                principalTable: "TemplateSteps",
                principalColumns: new[] { "TemplateVersionId", "ptsrg" });

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateSteps_TemplateStepGroups_TemplateVersionId_TemplateStepGroupId",
                table: "TemplateSteps",
                columns: new[] { "TemplateVersionId", "TemplateStepGroupId" },
                principalTable: "TemplateStepGroups",
                principalColumns: new[] { "TemplateVersionId", "ptsgrg" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessRequestValues_ProcessSteps_ProcessId_ProducingProcessStepId",
                table: "ProcessRequestValues");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessRequestValues_Users_ModifiedByUserId",
                table: "ProcessRequestValues");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSteps_ProcessStepGroups_ProcessId_ProcessStepGroupId",
                table: "ProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateRequestFields_TemplateSteps_TemplateVersionId_ProducingTemplateStepId",
                table: "TemplateRequestFields");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateSteps_TemplateStepGroups_TemplateVersionId_TemplateStepGroupId",
                table: "TemplateSteps");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TemplateSteps_TemplateVersionId_ptsrg",
                table: "TemplateSteps");

            migrationBuilder.DropIndex(
                name: "IX_TemplateSteps_TemplateVersionId_TemplateStepGroupId",
                table: "TemplateSteps");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TemplateStepGroups_TemplateVersionId_ptsgrg",
                table: "TemplateStepGroups");

            migrationBuilder.DropIndex(
                name: "IX_TemplateRequestFields_TemplateVersionId_ProducingTemplateStepId",
                table: "TemplateRequestFields");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProcessSteps_ProcessId_psrg",
                table: "ProcessSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_ProcessId_ProcessStepGroupId",
                table: "ProcessSteps");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProcessStepGroups_ProcessId_psgrg",
                table: "ProcessStepGroups");

            migrationBuilder.DropIndex(
                name: "IX_ProcessRequestValues_ModifiedByUserId",
                table: "ProcessRequestValues");

            migrationBuilder.DropIndex(
                name: "IX_ProcessRequestValues_ProcessId_ProducingProcessStepId",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TemplateRequestFields");

            migrationBuilder.DropColumn(
                name: "Pinned",
                table: "TemplateRequestFields");

            migrationBuilder.DropColumn(
                name: "ProducingTemplateStepId",
                table: "TemplateRequestFields");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "ModifiedAtUtc",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "ModifiedByDisplayName",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "OptionsJson",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "Pinned",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "ProducingProcessStepId",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProcessRequestValues");

            migrationBuilder.AlterColumn<string>(
                name: "BeforeValue",
                table: "ProcessAuditEvents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AfterValue",
                table: "ProcessAuditEvents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_TemplateVersionId",
                table: "TemplateSteps",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateStepGroups_TemplateVersionId_Name",
                table: "TemplateStepGroups",
                columns: new[] { "TemplateVersionId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessSteps_ProcessStepGroups_ProcessStepGroupId",
                table: "ProcessSteps",
                column: "ProcessStepGroupId",
                principalTable: "ProcessStepGroups",
                principalColumn: "psgrg");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateSteps_TemplateStepGroups_TemplateStepGroupId",
                table: "TemplateSteps",
                column: "TemplateStepGroupId",
                principalTable: "TemplateStepGroups",
                principalColumn: "ptsgrg");
        }
    }
}

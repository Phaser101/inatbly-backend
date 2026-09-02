using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intably.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStepGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TemplateSteps_TemplateVersionId_Order",
                table: "TemplateSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_ProcessId_Order",
                table: "ProcessSteps");

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateStepGroupId",
                table: "TemplateSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessStepGroupId",
                table: "ProcessSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessStepGroups",
                columns: table => new
                {
                    psgrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTemplateStepGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ExecutionMode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepGroups", x => x.psgrg);
                    table.ForeignKey(
                        name: "FK_ProcessStepGroups_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "pirg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateStepGroups",
                columns: table => new
                {
                    ptsgrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    ExecutionMode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateStepGroups", x => x.ptsgrg);
                    table.ForeignKey(
                        name: "FK_TemplateStepGroups_TemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "TemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStepGroupPrerequisites",
                columns: table => new
                {
                    ProcessStepGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrerequisiteProcessStepGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepGroupPrerequisites", x => new { x.ProcessStepGroupId, x.PrerequisiteProcessStepGroupId });
                    table.ForeignKey(
                        name: "FK_ProcessStepGroupPrerequisites_ProcessStepGroups_PrerequisiteProcessStepGroupId",
                        column: x => x.PrerequisiteProcessStepGroupId,
                        principalTable: "ProcessStepGroups",
                        principalColumn: "psgrg");
                    table.ForeignKey(
                        name: "FK_ProcessStepGroupPrerequisites_ProcessStepGroups_ProcessStepGroupId",
                        column: x => x.ProcessStepGroupId,
                        principalTable: "ProcessStepGroups",
                        principalColumn: "psgrg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateStepGroupPrerequisites",
                columns: table => new
                {
                    TemplateStepGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrerequisiteTemplateStepGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateStepGroupPrerequisites", x => new { x.TemplateStepGroupId, x.PrerequisiteTemplateStepGroupId });
                    table.ForeignKey(
                        name: "FK_TemplateStepGroupPrerequisites_TemplateStepGroups_PrerequisiteTemplateStepGroupId",
                        column: x => x.PrerequisiteTemplateStepGroupId,
                        principalTable: "TemplateStepGroups",
                        principalColumn: "ptsgrg");
                    table.ForeignKey(
                        name: "FK_TemplateStepGroupPrerequisites_TemplateStepGroups_TemplateStepGroupId",
                        column: x => x.TemplateStepGroupId,
                        principalTable: "TemplateStepGroups",
                        principalColumn: "ptsgrg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO TemplateStepGroups
                    (ptsgrg, TemplateVersionId, Name, Description, [Order], ExecutionMode)
                SELECT
                    NEWID(),
                    Id,
                    'Default',
                    '',
                    1,
                    CASE WHEN RequireSequentialSteps = 1
                        THEN 'Sequential'
                        ELSE 'Parallel'
                    END
                FROM TemplateVersions;

                UPDATE steps
                SET TemplateStepGroupId = groups.ptsgrg
                FROM TemplateSteps AS steps
                INNER JOIN TemplateStepGroups AS groups
                    ON groups.TemplateVersionId = steps.TemplateVersionId;

                INSERT INTO ProcessStepGroups
                    (psgrg, ProcessId, SourceTemplateStepGroupId, Name, Description, [Order], ExecutionMode)
                SELECT
                    NEWID(),
                    processes.pirg,
                    COALESCE(templateGroups.ptsgrg, NEWID()),
                    'Default',
                    '',
                    1,
                    CASE WHEN processes.RequireSequentialSteps = 1
                        THEN 'Sequential'
                        ELSE 'Parallel'
                    END
                FROM Processes AS processes
                OUTER APPLY
                (
                    SELECT TOP (1) groups.ptsgrg
                    FROM TemplateVersions AS versions
                    INNER JOIN TemplateStepGroups AS groups
                        ON groups.TemplateVersionId = versions.Id
                    WHERE versions.TemplateId = processes.TemplateId
                        AND versions.Version = processes.TemplateVersion
                    ORDER BY groups.[Order], groups.ptsgrg
                ) AS templateGroups;

                UPDATE steps
                SET ProcessStepGroupId = groups.psgrg
                FROM ProcessSteps AS steps
                INNER JOIN ProcessStepGroups AS groups
                    ON groups.ProcessId = steps.ProcessId;
                """);

            migrationBuilder.DropColumn(
                name: "RequireSequentialSteps",
                table: "TemplateVersions");

            migrationBuilder.DropColumn(
                name: "RequireSequentialSteps",
                table: "Processes");

            migrationBuilder.AlterColumn<Guid>(
                name: "TemplateStepGroupId",
                table: "TemplateSteps",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProcessStepGroupId",
                table: "ProcessSteps",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_TemplateStepGroupId_Order",
                table: "TemplateSteps",
                columns: new[] { "TemplateStepGroupId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_TemplateVersionId",
                table: "TemplateSteps",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessStepGroupId_Order",
                table: "ProcessSteps",
                columns: new[] { "ProcessStepGroupId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepGroupPrerequisites_PrerequisiteProcessStepGroupId",
                table: "ProcessStepGroupPrerequisites",
                column: "PrerequisiteProcessStepGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepGroups_ProcessId_Order",
                table: "ProcessStepGroups",
                columns: new[] { "ProcessId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepGroups_ProcessId_SourceTemplateStepGroupId",
                table: "ProcessStepGroups",
                columns: new[] { "ProcessId", "SourceTemplateStepGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateStepGroupPrerequisites_PrerequisiteTemplateStepGroupId",
                table: "TemplateStepGroupPrerequisites",
                column: "PrerequisiteTemplateStepGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateStepGroups_TemplateVersionId_Name",
                table: "TemplateStepGroups",
                columns: new[] { "TemplateVersionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateStepGroups_TemplateVersionId_Order",
                table: "TemplateStepGroups",
                columns: new[] { "TemplateVersionId", "Order" },
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessSteps_ProcessStepGroups_ProcessStepGroupId",
                table: "ProcessSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_TemplateSteps_TemplateStepGroups_TemplateStepGroupId",
                table: "TemplateSteps");

            migrationBuilder.DropTable(
                name: "ProcessStepGroupPrerequisites");

            migrationBuilder.DropTable(
                name: "TemplateStepGroupPrerequisites");

            migrationBuilder.DropTable(
                name: "ProcessStepGroups");

            migrationBuilder.DropTable(
                name: "TemplateStepGroups");

            migrationBuilder.DropIndex(
                name: "IX_TemplateSteps_TemplateStepGroupId_Order",
                table: "TemplateSteps");

            migrationBuilder.DropIndex(
                name: "IX_TemplateSteps_TemplateVersionId",
                table: "TemplateSteps");

            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_ProcessStepGroupId_Order",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "TemplateStepGroupId",
                table: "TemplateSteps");

            migrationBuilder.DropColumn(
                name: "ProcessStepGroupId",
                table: "ProcessSteps");

            migrationBuilder.AddColumn<bool>(
                name: "RequireSequentialSteps",
                table: "TemplateVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireSequentialSteps",
                table: "Processes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_TemplateVersionId_Order",
                table: "TemplateSteps",
                columns: new[] { "TemplateVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessId_Order",
                table: "ProcessSteps",
                columns: new[] { "ProcessId", "Order" },
                unique: true);
        }
    }
}

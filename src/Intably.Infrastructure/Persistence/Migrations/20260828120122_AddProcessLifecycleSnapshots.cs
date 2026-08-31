using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intably.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessLifecycleSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeDisplayName",
                table: "ProcessSteps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutorDisplayName",
                table: "ProcessSteps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredRoleName",
                table: "ProcessSteps",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateStepId",
                table: "ProcessSteps",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FieldType",
                table: "ProcessRequestValues",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "ProcessRequestValues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByDisplayName",
                table: "Processes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerDisplayName",
                table: "Processes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActorDisplayName",
                table: "ProcessAuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE ps
                SET ps.SourceTemplateStepId = COALESCE(ts.ptsrg, ps.psrg),
                    ps.RequiredRoleName = COALESCE(fr.Name, ''),
                    ps.AssigneeDisplayName = assignee.DisplayName,
                    ps.ExecutorDisplayName = executor.DisplayName
                FROM ProcessSteps ps
                INNER JOIN Processes p ON p.pirg = ps.ProcessId
                LEFT JOIN TemplateVersions tv
                    ON tv.TemplateId = p.TemplateId
                    AND tv.Version = p.TemplateVersion
                LEFT JOIN TemplateSteps ts
                    ON ts.TemplateVersionId = tv.Id
                    AND ts.[Order] = ps.[Order]
                LEFT JOIN FunctionalRoles fr ON fr.frrg = ps.RequiredRoleId
                LEFT JOIN Users assignee ON assignee.grg = ps.AssigneeUserId
                LEFT JOIN Users executor ON executor.grg = ps.ExecutorUserId;

                UPDATE p
                SET p.OwnerDisplayName = owner.DisplayName,
                    p.ClosedByDisplayName = closer.DisplayName
                FROM Processes p
                INNER JOIN Users owner ON owner.grg = p.OwnerUserId
                LEFT JOIN Users closer ON closer.grg = p.ClosedByUserId;

                UPDATE ae
                SET ae.ActorDisplayName = actor.DisplayName
                FROM ProcessAuditEvents ae
                INNER JOIN Users actor ON actor.grg = ae.ActorUserId;

                UPDATE prv
                SET prv.FieldType = LOWER(trf.Type),
                    prv.IsRequired = trf.IsRequired
                FROM ProcessRequestValues prv
                INNER JOIN TemplateRequestFields trf
                    ON trf.rfrg = prv.SourceRequestFieldId;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SourceTemplateStepId",
                table: "ProcessSteps",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessId_SourceTemplateStepId",
                table: "ProcessSteps",
                columns: new[] { "ProcessId", "SourceTemplateStepId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessSteps_ProcessId_SourceTemplateStepId",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "AssigneeDisplayName",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "ExecutorDisplayName",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "RequiredRoleName",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "SourceTemplateStepId",
                table: "ProcessSteps");

            migrationBuilder.DropColumn(
                name: "FieldType",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "ProcessRequestValues");

            migrationBuilder.DropColumn(
                name: "ClosedByDisplayName",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "OwnerDisplayName",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "ActorDisplayName",
                table: "ProcessAuditEvents");
        }
    }
}

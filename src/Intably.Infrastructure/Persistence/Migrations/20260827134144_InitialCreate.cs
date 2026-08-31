using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intably.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FunctionalRoles",
                columns: table => new
                {
                    frrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FunctionalRoles", x => x.frrg);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    grg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntraObjectId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.grg);
                });

            migrationBuilder.CreateTable(
                name: "ProcessTemplates",
                columns: table => new
                {
                    ptrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedVersion = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTemplates", x => x.ptrg);
                    table.ForeignKey(
                        name: "FK_ProcessTemplates_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFunctionalRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFunctionalRoles", x => new { x.UserId, x.FunctionalRoleId });
                    table.ForeignKey(
                        name: "FK_UserFunctionalRoles_FunctionalRoles_FunctionalRoleId",
                        column: x => x.FunctionalRoleId,
                        principalTable: "FunctionalRoles",
                        principalColumn: "frrg",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFunctionalRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes",
                columns: table => new
                {
                    pirg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Context = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes", x => x.pirg);
                    table.ForeignKey(
                        name: "FK_Processes_ProcessTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ProcessTemplates",
                        principalColumn: "ptrg",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateVersions_ProcessTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ProcessTemplates",
                        principalColumn: "ptrg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessRequestValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRequestFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessRequestValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessRequestValues_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "pirg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessSteps",
                columns: table => new
                {
                    psrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequiredRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SupportingUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NoteRequired = table.Column<bool>(type: "bit", nullable: false),
                    ExecutorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExecutionNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    BlockedReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSteps", x => x.psrg);
                    table.ForeignKey(
                        name: "FK_ProcessSteps_FunctionalRoles_RequiredRoleId",
                        column: x => x.RequiredRoleId,
                        principalTable: "FunctionalRoles",
                        principalColumn: "frrg",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessSteps_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "pirg",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessSteps_Users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessSteps_Users_ExecutorUserId",
                        column: x => x.ExecutorUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateRequestFields",
                columns: table => new
                {
                    rfrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Placeholder = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFieldSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceFieldSetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceFieldSetVersion = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateRequestFields", x => x.rfrg);
                    table.ForeignKey(
                        name: "FK_TemplateRequestFields_TemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "TemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateSteps",
                columns: table => new
                {
                    ptsrg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequiredRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SupportingUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    DefaultAssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueOffsetDays = table.Column<int>(type: "int", nullable: true),
                    NoteRequired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateSteps", x => x.ptsrg);
                    table.ForeignKey(
                        name: "FK_TemplateSteps_FunctionalRoles_RequiredRoleId",
                        column: x => x.RequiredRoleId,
                        principalTable: "FunctionalRoles",
                        principalColumn: "frrg",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemplateSteps_TemplateVersions_TemplateVersionId",
                        column: x => x.TemplateVersionId,
                        principalTable: "TemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateSteps_Users_DefaultAssigneeUserId",
                        column: x => x.DefaultAssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessAuditEvents",
                columns: table => new
                {
                    aerg = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AffectedItem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BeforeValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AfterValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessAuditEvents", x => x.aerg);
                    table.ForeignKey(
                        name: "FK_ProcessAuditEvents_ProcessSteps_ProcessStepId",
                        column: x => x.ProcessStepId,
                        principalTable: "ProcessSteps",
                        principalColumn: "psrg");
                    table.ForeignKey(
                        name: "FK_ProcessAuditEvents_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "pirg",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "grg",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplateRequestFieldOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateRequestFieldOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateRequestFieldOptions_TemplateRequestFields_RequestFieldId",
                        column: x => x.RequestFieldId,
                        principalTable: "TemplateRequestFields",
                        principalColumn: "rfrg",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FunctionalRoles_Name",
                table: "FunctionalRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessAuditEvents_ActorUserId",
                table: "ProcessAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessAuditEvents_ProcessId",
                table: "ProcessAuditEvents",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessAuditEvents_ProcessStepId",
                table: "ProcessAuditEvents",
                column: "ProcessStepId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ClosedByUserId",
                table: "Processes",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_OwnerUserId",
                table: "Processes",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_TemplateId",
                table: "Processes",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRequestValues_ProcessId_Order",
                table: "ProcessRequestValues",
                columns: new[] { "ProcessId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_AssigneeUserId",
                table: "ProcessSteps",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ExecutorUserId",
                table: "ProcessSteps",
                column: "ExecutorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_ProcessId_Order",
                table: "ProcessSteps",
                columns: new[] { "ProcessId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSteps_RequiredRoleId",
                table: "ProcessSteps",
                column: "RequiredRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTemplates_OwnerUserId",
                table: "ProcessTemplates",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateRequestFieldOptions_RequestFieldId_Order",
                table: "TemplateRequestFieldOptions",
                columns: new[] { "RequestFieldId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateRequestFields_TemplateVersionId_Order",
                table: "TemplateRequestFields",
                columns: new[] { "TemplateVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_DefaultAssigneeUserId",
                table: "TemplateSteps",
                column: "DefaultAssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_RequiredRoleId",
                table: "TemplateSteps",
                column: "RequiredRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSteps_TemplateVersionId_Order",
                table: "TemplateSteps",
                columns: new[] { "TemplateVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateVersions_TemplateId_Version",
                table: "TemplateVersions",
                columns: new[] { "TemplateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFunctionalRoles_FunctionalRoleId",
                table: "UserFunctionalRoles",
                column: "FunctionalRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntraObjectId",
                table: "Users",
                column: "EntraObjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessAuditEvents");

            migrationBuilder.DropTable(
                name: "ProcessRequestValues");

            migrationBuilder.DropTable(
                name: "TemplateRequestFieldOptions");

            migrationBuilder.DropTable(
                name: "TemplateSteps");

            migrationBuilder.DropTable(
                name: "UserFunctionalRoles");

            migrationBuilder.DropTable(
                name: "ProcessSteps");

            migrationBuilder.DropTable(
                name: "TemplateRequestFields");

            migrationBuilder.DropTable(
                name: "FunctionalRoles");

            migrationBuilder.DropTable(
                name: "Processes");

            migrationBuilder.DropTable(
                name: "TemplateVersions");

            migrationBuilder.DropTable(
                name: "ProcessTemplates");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

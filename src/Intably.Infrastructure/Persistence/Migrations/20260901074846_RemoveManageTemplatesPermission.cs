using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intably.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveManageTemplatesPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO [PermissionGrants]
                    ([pgrg], [UserId], [Permission], [GrantedByUserId], [GrantedAtUtc])
                SELECT
                    NEWID(),
                    legacyGrant.[UserId],
                    replacement.[Permission],
                    legacyGrant.[GrantedByUserId],
                    legacyGrant.[GrantedAtUtc]
                FROM [PermissionGrants] AS legacyGrant
                CROSS JOIN (
                    VALUES
                        (N'CreateTemplates'),
                        (N'EditTemplates'),
                        (N'PublishTemplates'),
                        (N'ArchiveTemplates')
                ) AS replacement([Permission])
                WHERE legacyGrant.[Permission] = N'ManageTemplates'
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [PermissionGrants] AS existingGrant
                        WHERE existingGrant.[UserId] = legacyGrant.[UserId]
                            AND existingGrant.[Permission] = replacement.[Permission]
                    );

                DELETE FROM [PermissionGrants]
                WHERE [Permission] = N'ManageTemplates';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH RankedGrants AS (
                    SELECT
                        detailedGrant.[UserId],
                        detailedGrant.[GrantedByUserId],
                        detailedGrant.[GrantedAtUtc],
                        ROW_NUMBER() OVER (
                            PARTITION BY detailedGrant.[UserId]
                            ORDER BY detailedGrant.[GrantedAtUtc]
                        ) AS [GrantRank]
                    FROM [PermissionGrants] AS detailedGrant
                    WHERE detailedGrant.[Permission] IN (
                        N'CreateTemplates',
                        N'EditTemplates',
                        N'PublishTemplates',
                        N'ArchiveTemplates')
                )
                INSERT INTO [PermissionGrants]
                    ([pgrg], [UserId], [Permission], [GrantedByUserId], [GrantedAtUtc])
                SELECT
                    NEWID(),
                    rankedGrant.[UserId],
                    N'ManageTemplates',
                    rankedGrant.[GrantedByUserId],
                    rankedGrant.[GrantedAtUtc]
                FROM RankedGrants AS rankedGrant
                WHERE rankedGrant.[GrantRank] = 1
                    AND NOT EXISTS (
                        SELECT 1
                        FROM [PermissionGrants] AS existingGrant
                        WHERE existingGrant.[UserId] = rankedGrant.[UserId]
                            AND existingGrant.[Permission] = N'ManageTemplates'
                    );

                DELETE FROM [PermissionGrants]
                WHERE [Permission] IN (
                    N'CreateTemplates',
                    N'EditTemplates',
                    N'PublishTemplates',
                    N'ArchiveTemplates');
                """);
        }
    }
}

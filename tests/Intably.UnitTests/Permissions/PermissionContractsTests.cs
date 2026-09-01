using Intably.Application.Permissions;
using Intably.Domain.Permissions;

namespace Intably.UnitTests.Permissions;

public sealed class PermissionContractsTests
{
    [Theory]
    [InlineData(
        ApplicationPermission.ViewMyWork,
        PermissionContracts.ViewMyWork)]
    [InlineData(
        ApplicationPermission.ViewProcesses,
        PermissionContracts.ViewProcesses)]
    [InlineData(
        ApplicationPermission.StartProcesses,
        PermissionContracts.StartProcesses)]
    [InlineData(
        ApplicationPermission.UpdateProcessSteps,
        PermissionContracts.UpdateProcessSteps)]
    [InlineData(
        ApplicationPermission.AssignProcessSteps,
        PermissionContracts.AssignProcessSteps)]
    [InlineData(
        ApplicationPermission.CloseProcesses,
        PermissionContracts.CloseProcesses)]
    [InlineData(
        ApplicationPermission.ViewTemplates,
        PermissionContracts.ViewTemplates)]
    [InlineData(
        ApplicationPermission.CreateTemplates,
        PermissionContracts.CreateTemplates)]
    [InlineData(
        ApplicationPermission.EditTemplates,
        PermissionContracts.EditTemplates)]
    [InlineData(
        ApplicationPermission.PublishTemplates,
        PermissionContracts.PublishTemplates)]
    [InlineData(
        ApplicationPermission.ArchiveTemplates,
        PermissionContracts.ArchiveTemplates)]
    [InlineData(
        ApplicationPermission.ManagePermissions,
        PermissionContracts.ManagePermissions)]
    [InlineData(ApplicationPermission.ManageRoles, PermissionContracts.ManageRoles)]
    [InlineData(
        ApplicationPermission.ManageMembership,
        PermissionContracts.ManageMembership)]
    [InlineData(
        ApplicationPermission.ManageUserStatus,
        PermissionContracts.ManageUserStatus)]
    public void ToContractName_MapsPermission(
        ApplicationPermission permission,
        string expected)
    {
        Assert.Equal(expected, permission.ToContractName());
    }

    [Theory]
    [InlineData(
        PermissionContracts.ViewMyWork,
        ApplicationPermission.ViewMyWork)]
    [InlineData(
        PermissionContracts.ViewProcesses,
        ApplicationPermission.ViewProcesses)]
    [InlineData(
        PermissionContracts.StartProcesses,
        ApplicationPermission.StartProcesses)]
    [InlineData(
        PermissionContracts.UpdateProcessSteps,
        ApplicationPermission.UpdateProcessSteps)]
    [InlineData(
        PermissionContracts.AssignProcessSteps,
        ApplicationPermission.AssignProcessSteps)]
    [InlineData(
        PermissionContracts.CloseProcesses,
        ApplicationPermission.CloseProcesses)]
    [InlineData(
        PermissionContracts.ViewTemplates,
        ApplicationPermission.ViewTemplates)]
    [InlineData(
        PermissionContracts.CreateTemplates,
        ApplicationPermission.CreateTemplates)]
    [InlineData(
        PermissionContracts.EditTemplates,
        ApplicationPermission.EditTemplates)]
    [InlineData(
        PermissionContracts.PublishTemplates,
        ApplicationPermission.PublishTemplates)]
    [InlineData(
        PermissionContracts.ArchiveTemplates,
        ApplicationPermission.ArchiveTemplates)]
    [InlineData(
        PermissionContracts.ManagePermissions,
        ApplicationPermission.ManagePermissions)]
    [InlineData(
        PermissionContracts.ManageRoles,
        ApplicationPermission.ManageRoles)]
    [InlineData(
        PermissionContracts.ManageMembership,
        ApplicationPermission.ManageMembership)]
    [InlineData(
        PermissionContracts.ManageUserStatus,
        ApplicationPermission.ManageUserStatus)]
    public void TryParse_MapsContractName(
        string value,
        ApplicationPermission expected)
    {
        Assert.True(PermissionContracts.TryParse(value, out var permission));
        Assert.Equal(expected, permission);
    }

    [Theory]
    [MemberData(nameof(EffectivePermissionCases))]
    public void GetEffectivePermissions_ExpandsOnlyDefinedImplications(
        ApplicationPermission directPermission,
        ApplicationPermission[] expected)
    {
        var effective = new[] { directPermission }.GetEffectivePermissions();

        Assert.Equal(expected.Order(), effective.Order());
    }

    public static TheoryData<ApplicationPermission, ApplicationPermission[]>
        EffectivePermissionCases =>
        new()
        {
            {
                ApplicationPermission.StartProcesses,
                [
                    ApplicationPermission.StartProcesses,
                    ApplicationPermission.ViewProcesses,
                    ApplicationPermission.ViewTemplates,
                ]
            },
            {
                ApplicationPermission.AssignProcessSteps,
                [
                    ApplicationPermission.AssignProcessSteps,
                    ApplicationPermission.ViewProcesses,
                ]
            },
            {
                ApplicationPermission.ViewMyWork,
                [ApplicationPermission.ViewMyWork]
            },
        };
}

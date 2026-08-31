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
        ApplicationPermission.ViewTemplates,
        PermissionContracts.ViewTemplates)]
    [InlineData(
        ApplicationPermission.ManagePermissions,
        PermissionContracts.ManagePermissions)]
    [InlineData(ApplicationPermission.ManageRoles, PermissionContracts.ManageRoles)]
    [InlineData(
        ApplicationPermission.ManageTemplates,
        PermissionContracts.ManageTemplates)]
    [InlineData(
        ApplicationPermission.ManageProcesses,
        PermissionContracts.ManageProcesses)]
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
        PermissionContracts.ViewTemplates,
        ApplicationPermission.ViewTemplates)]
    [InlineData(
        PermissionContracts.ManagePermissions,
        ApplicationPermission.ManagePermissions)]
    [InlineData(
        PermissionContracts.ManageRoles,
        ApplicationPermission.ManageRoles)]
    [InlineData(
        PermissionContracts.ManageTemplates,
        ApplicationPermission.ManageTemplates)]
    [InlineData(
        PermissionContracts.ManageProcesses,
        ApplicationPermission.ManageProcesses)]
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
                ApplicationPermission.ManageTemplates,
                [
                    ApplicationPermission.ManageTemplates,
                    ApplicationPermission.ViewTemplates,
                ]
            },
            {
                ApplicationPermission.StartProcesses,
                [
                    ApplicationPermission.StartProcesses,
                    ApplicationPermission.ViewProcesses,
                    ApplicationPermission.ViewTemplates,
                ]
            },
            {
                ApplicationPermission.ManageProcesses,
                [
                    ApplicationPermission.ManageProcesses,
                    ApplicationPermission.StartProcesses,
                    ApplicationPermission.ViewProcesses,
                    ApplicationPermission.ViewTemplates,
                ]
            },
            {
                ApplicationPermission.ViewMyWork,
                [ApplicationPermission.ViewMyWork]
            },
        };
}

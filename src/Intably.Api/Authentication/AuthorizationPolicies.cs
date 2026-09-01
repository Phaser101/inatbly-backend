using Intably.Application.Permissions;
using Intably.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Intably.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string ApiAccess = nameof(ApiAccess);
    public const string ViewMyWork = PermissionContracts.ViewMyWork;
    public const string ViewProcesses = PermissionContracts.ViewProcesses;
    public const string StartProcesses = PermissionContracts.StartProcesses;
    public const string UpdateProcessSteps = PermissionContracts.UpdateProcessSteps;
    public const string AssignProcessSteps = PermissionContracts.AssignProcessSteps;
    public const string CloseProcesses = PermissionContracts.CloseProcesses;
    public const string ViewTemplates = PermissionContracts.ViewTemplates;
    public const string CreateTemplates = PermissionContracts.CreateTemplates;
    public const string EditTemplates = PermissionContracts.EditTemplates;
    public const string PublishTemplates = PermissionContracts.PublishTemplates;
    public const string ArchiveTemplates = PermissionContracts.ArchiveTemplates;
    public const string ManagePermissions = PermissionContracts.ManagePermissions;
    public const string ManageRoles = PermissionContracts.ManageRoles;
    public const string ManageMembership = PermissionContracts.ManageMembership;
    public const string ManageUserStatus = PermissionContracts.ManageUserStatus;

    public static void AddTo(AuthorizationOptions options)
    {
        options.AddPolicy(
            ApiAccess,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ActiveUserRequirement());
            });
        AddPermissionPolicy(options, ViewMyWork, ApplicationPermission.ViewMyWork);
        AddPermissionPolicy(
            options,
            ViewProcesses,
            ApplicationPermission.ViewProcesses);
        AddPermissionPolicy(
            options,
            StartProcesses,
            ApplicationPermission.StartProcesses);
        AddPermissionPolicy(
            options,
            UpdateProcessSteps,
            ApplicationPermission.UpdateProcessSteps);
        AddPermissionPolicy(
            options,
            AssignProcessSteps,
            ApplicationPermission.AssignProcessSteps);
        AddPermissionPolicy(
            options,
            CloseProcesses,
            ApplicationPermission.CloseProcesses);
        AddPermissionPolicy(
            options,
            ViewTemplates,
            ApplicationPermission.ViewTemplates);
        AddPermissionPolicy(
            options,
            CreateTemplates,
            ApplicationPermission.CreateTemplates);
        AddPermissionPolicy(
            options,
            EditTemplates,
            ApplicationPermission.EditTemplates);
        AddPermissionPolicy(
            options,
            PublishTemplates,
            ApplicationPermission.PublishTemplates);
        AddPermissionPolicy(
            options,
            ArchiveTemplates,
            ApplicationPermission.ArchiveTemplates);
        AddPermissionPolicy(
            options,
            ManagePermissions,
            ApplicationPermission.ManagePermissions);
        AddPermissionPolicy(options, ManageRoles, ApplicationPermission.ManageRoles);
        AddPermissionPolicy(
            options,
            ManageMembership,
            ApplicationPermission.ManageMembership);
        AddPermissionPolicy(
            options,
            ManageUserStatus,
            ApplicationPermission.ManageUserStatus);
    }

    private static void AddPermissionPolicy(
        AuthorizationOptions options,
        string name,
        ApplicationPermission permission)
    {
        options.AddPolicy(
            name,
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ActiveUserRequirement());
                policy.AddRequirements(new PermissionRequirement(permission));
            });
    }
}

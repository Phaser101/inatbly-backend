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
    public const string ViewTemplates = PermissionContracts.ViewTemplates;
    public const string ManagePermissions = PermissionContracts.ManagePermissions;
    public const string ManageRoles = PermissionContracts.ManageRoles;
    public const string ManageTemplates = PermissionContracts.ManageTemplates;
    public const string ManageProcesses = PermissionContracts.ManageProcesses;

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
            ViewTemplates,
            ApplicationPermission.ViewTemplates);
        AddPermissionPolicy(
            options,
            ManagePermissions,
            ApplicationPermission.ManagePermissions);
        AddPermissionPolicy(options, ManageRoles, ApplicationPermission.ManageRoles);
        AddPermissionPolicy(
            options,
            ManageTemplates,
            ApplicationPermission.ManageTemplates);
        AddPermissionPolicy(
            options,
            ManageProcesses,
            ApplicationPermission.ManageProcesses);
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

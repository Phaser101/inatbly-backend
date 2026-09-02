using Intably.Domain.Permissions;

namespace Intably.Application.Permissions;

public static class PermissionContracts
{
    public const string ViewMyWork = "VIEW_MY_WORK";
    public const string ViewProcesses = "VIEW_PROCESSES";
    public const string StartProcesses = "START_PROCESSES";
    public const string UpdateProcessSteps = "UPDATE_PROCESS_STEPS";
    public const string UpdateProcessInformation =
        "UPDATE_PROCESS_INFORMATION";
    public const string AssignProcessSteps = "ASSIGN_PROCESS_STEPS";
    public const string CloseProcesses = "CLOSE_PROCESSES";
    public const string ViewTemplates = "VIEW_TEMPLATES";
    public const string CreateTemplates = "CREATE_TEMPLATES";
    public const string EditTemplates = "EDIT_TEMPLATES";
    public const string PublishTemplates = "PUBLISH_TEMPLATES";
    public const string ArchiveTemplates = "ARCHIVE_TEMPLATES";
    public const string ManagePermissions = "MANAGE_PERMISSIONS";
    public const string ManageRoles = "MANAGE_ROLES";
    public const string ManageMembership = "MANAGE_MEMBERSHIP";
    public const string ManageUserStatus = "MANAGE_USER_STATUS";

    public static string ToContractName(this ApplicationPermission permission)
    {
        return permission switch
        {
            ApplicationPermission.ViewMyWork => ViewMyWork,
            ApplicationPermission.ViewProcesses => ViewProcesses,
            ApplicationPermission.StartProcesses => StartProcesses,
            ApplicationPermission.UpdateProcessSteps => UpdateProcessSteps,
            ApplicationPermission.UpdateProcessInformation =>
                UpdateProcessInformation,
            ApplicationPermission.AssignProcessSteps => AssignProcessSteps,
            ApplicationPermission.CloseProcesses => CloseProcesses,
            ApplicationPermission.ViewTemplates => ViewTemplates,
            ApplicationPermission.CreateTemplates => CreateTemplates,
            ApplicationPermission.EditTemplates => EditTemplates,
            ApplicationPermission.PublishTemplates => PublishTemplates,
            ApplicationPermission.ArchiveTemplates => ArchiveTemplates,
            ApplicationPermission.ManagePermissions => ManagePermissions,
            ApplicationPermission.ManageRoles => ManageRoles,
            ApplicationPermission.ManageMembership => ManageMembership,
            ApplicationPermission.ManageUserStatus => ManageUserStatus,
            _ => throw new ArgumentOutOfRangeException(nameof(permission)),
        };
    }

    public static IReadOnlyCollection<ApplicationPermission> GetEffectivePermissions(
        this IEnumerable<ApplicationPermission> directPermissions)
    {
        ArgumentNullException.ThrowIfNull(directPermissions);

        var effective = directPermissions.ToHashSet();
        if (effective.Contains(ApplicationPermission.StartProcesses))
        {
            effective.Add(ApplicationPermission.ViewTemplates);
            effective.Add(ApplicationPermission.ViewProcesses);
        }

        if (effective.Contains(ApplicationPermission.UpdateProcessSteps)
            || effective.Contains(ApplicationPermission.UpdateProcessInformation)
            || effective.Contains(ApplicationPermission.AssignProcessSteps)
            || effective.Contains(ApplicationPermission.CloseProcesses))
        {
            effective.Add(ApplicationPermission.ViewProcesses);
        }

        if (effective.Contains(ApplicationPermission.CreateTemplates)
            || effective.Contains(ApplicationPermission.EditTemplates)
            || effective.Contains(ApplicationPermission.PublishTemplates)
            || effective.Contains(ApplicationPermission.ArchiveTemplates))
        {
            effective.Add(ApplicationPermission.ViewTemplates);
        }

        return effective.Order().ToArray();
    }

    public static bool TryParse(
        string? value,
        out ApplicationPermission permission)
    {
        permission = value?.Trim().ToUpperInvariant() switch
        {
            ViewMyWork => ApplicationPermission.ViewMyWork,
            ViewProcesses => ApplicationPermission.ViewProcesses,
            StartProcesses => ApplicationPermission.StartProcesses,
            UpdateProcessSteps => ApplicationPermission.UpdateProcessSteps,
            UpdateProcessInformation =>
                ApplicationPermission.UpdateProcessInformation,
            AssignProcessSteps => ApplicationPermission.AssignProcessSteps,
            CloseProcesses => ApplicationPermission.CloseProcesses,
            ViewTemplates => ApplicationPermission.ViewTemplates,
            CreateTemplates => ApplicationPermission.CreateTemplates,
            EditTemplates => ApplicationPermission.EditTemplates,
            PublishTemplates => ApplicationPermission.PublishTemplates,
            ArchiveTemplates => ApplicationPermission.ArchiveTemplates,
            ManagePermissions => ApplicationPermission.ManagePermissions,
            ManageRoles => ApplicationPermission.ManageRoles,
            ManageMembership => ApplicationPermission.ManageMembership,
            ManageUserStatus => ApplicationPermission.ManageUserStatus,
            _ => default,
        };

        return value?.Trim().ToUpperInvariant() is
            ViewMyWork
            or ViewProcesses
            or StartProcesses
            or UpdateProcessSteps
            or UpdateProcessInformation
            or AssignProcessSteps
            or CloseProcesses
            or ViewTemplates
            or CreateTemplates
            or EditTemplates
            or PublishTemplates
            or ArchiveTemplates
            or ManagePermissions
            or ManageRoles
            or ManageMembership
            or ManageUserStatus;
    }
}

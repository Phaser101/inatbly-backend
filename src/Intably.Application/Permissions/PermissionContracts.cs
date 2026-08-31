using Intably.Domain.Permissions;

namespace Intably.Application.Permissions;

public static class PermissionContracts
{
    public const string ViewMyWork = "VIEW_MY_WORK";
    public const string ViewProcesses = "VIEW_PROCESSES";
    public const string StartProcesses = "START_PROCESSES";
    public const string ViewTemplates = "VIEW_TEMPLATES";
    public const string ManagePermissions = "MANAGE_PERMISSIONS";
    public const string ManageRoles = "MANAGE_ROLES";
    public const string ManageTemplates = "MANAGE_TEMPLATES";
    public const string ManageProcesses = "MANAGE_PROCESSES";

    public static string ToContractName(this ApplicationPermission permission)
    {
        return permission switch
        {
            ApplicationPermission.ViewMyWork => ViewMyWork,
            ApplicationPermission.ViewProcesses => ViewProcesses,
            ApplicationPermission.StartProcesses => StartProcesses,
            ApplicationPermission.ViewTemplates => ViewTemplates,
            ApplicationPermission.ManagePermissions => ManagePermissions,
            ApplicationPermission.ManageRoles => ManageRoles,
            ApplicationPermission.ManageTemplates => ManageTemplates,
            ApplicationPermission.ManageProcesses => ManageProcesses,
            _ => throw new ArgumentOutOfRangeException(nameof(permission)),
        };
    }

    public static IReadOnlyCollection<ApplicationPermission> GetEffectivePermissions(
        this IEnumerable<ApplicationPermission> directPermissions)
    {
        ArgumentNullException.ThrowIfNull(directPermissions);

        var effective = directPermissions.ToHashSet();
        if (effective.Contains(ApplicationPermission.ManageTemplates))
        {
            effective.Add(ApplicationPermission.ViewTemplates);
        }

        if (effective.Contains(ApplicationPermission.StartProcesses))
        {
            effective.Add(ApplicationPermission.ViewTemplates);
            effective.Add(ApplicationPermission.ViewProcesses);
        }

        if (effective.Contains(ApplicationPermission.ManageProcesses))
        {
            effective.Add(ApplicationPermission.StartProcesses);
            effective.Add(ApplicationPermission.ViewProcesses);
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
            ViewTemplates => ApplicationPermission.ViewTemplates,
            ManagePermissions => ApplicationPermission.ManagePermissions,
            ManageRoles => ApplicationPermission.ManageRoles,
            ManageTemplates => ApplicationPermission.ManageTemplates,
            ManageProcesses => ApplicationPermission.ManageProcesses,
            _ => default,
        };

        return value?.Trim().ToUpperInvariant() is
            ViewMyWork
            or ViewProcesses
            or StartProcesses
            or ViewTemplates
            or ManagePermissions
            or ManageRoles
            or ManageTemplates
            or ManageProcesses;
    }
}

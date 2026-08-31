using Intably.Domain.Common;

namespace Intably.Domain.Permissions;

public sealed class PermissionGrant : Entity
{
    private PermissionGrant()
    {
    }

    public PermissionGrant(
        Guid userId,
        ApplicationPermission permission,
        Guid grantedByUserId,
        DateTimeOffset grantedAtUtc)
    {
        UserId = userId;
        Permission = permission;
        GrantedByUserId = grantedByUserId;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid UserId { get; private set; }

    public ApplicationPermission Permission { get; private set; }

    public Guid GrantedByUserId { get; private set; }

    public DateTimeOffset GrantedAtUtc { get; private set; }
}

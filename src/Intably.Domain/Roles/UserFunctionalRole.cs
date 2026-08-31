namespace Intably.Domain.Roles;

public sealed class UserFunctionalRole
{
    private UserFunctionalRole()
    {
    }

    public UserFunctionalRole(
        Guid userId,
        Guid functionalRoleId,
        DateTimeOffset assignedAtUtc)
    {
        UserId = userId;
        FunctionalRoleId = functionalRoleId;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid FunctionalRoleId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }
}

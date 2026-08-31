using Intably.Application.Users;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Users;

internal sealed class UserLookupService(IntablyDbContext dbContext)
    : IUserLookupService
{
    public async Task<IReadOnlyCollection<UserLookup>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.Email)
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.IsActive,
            })
            .ToArrayAsync(cancellationToken);

        var memberships = await (
            from membership in dbContext.UserFunctionalRoles.AsNoTracking()
            join role in dbContext.FunctionalRoles.AsNoTracking()
                on membership.FunctionalRoleId equals role.Id
            orderby role.Name
            select new
            {
                membership.UserId,
                Role = new UserLookupRole(
                    role.Id,
                    role.Name,
                    role.IsArchived ? "Archived" : "Active"),
            }
        ).ToArrayAsync(cancellationToken);

        var rolesByUser = memberships
            .GroupBy(membership => membership.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<UserLookupRole>)
                    group.Select(membership => membership.Role).ToArray());

        return users
            .Select(user => new UserLookup(
                user.Id,
                user.DisplayName,
                user.Email,
                user.IsActive,
                rolesByUser.GetValueOrDefault(user.Id) ?? []))
            .ToArray();
    }
}

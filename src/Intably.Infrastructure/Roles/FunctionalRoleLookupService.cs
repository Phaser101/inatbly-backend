using Intably.Application.Roles;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Roles;

internal sealed class FunctionalRoleLookupService(IntablyDbContext dbContext)
    : IFunctionalRoleLookupService
{
    public async Task<IReadOnlyCollection<FunctionalRoleLookup>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.FunctionalRoles
            .AsNoTracking()
            .OrderBy(role => role.IsArchived)
            .ThenBy(role => role.Name)
            .Select(role => new FunctionalRoleLookup(
                role.Id,
                role.Name,
                role.Description,
                role.IsArchived ? "Archived" : "Active",
                role.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }
}

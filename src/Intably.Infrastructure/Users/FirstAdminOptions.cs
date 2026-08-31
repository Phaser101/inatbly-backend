namespace Intably.Infrastructure.Users;

internal sealed record FirstAdminOptions(
    string EntraTenantId,
    string EntraObjectId)
{
    public const string SectionName = "FirstAdmin";
}

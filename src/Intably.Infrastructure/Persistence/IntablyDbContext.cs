using Intably.Domain.Processes;
using Intably.Domain.Permissions;
using Intably.Domain.Roles;
using Intably.Domain.Templates;
using Intably.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Intably.Infrastructure.Persistence;

public sealed class IntablyDbContext(DbContextOptions<IntablyDbContext> options)
    : DbContext(options)
{
    public DbSet<FunctionalRole> FunctionalRoles => Set<FunctionalRole>();

    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();

    public DbSet<ProcessAuditEvent> ProcessAuditEvents => Set<ProcessAuditEvent>();

    public DbSet<ProcessInstance> Processes => Set<ProcessInstance>();

    public DbSet<ProcessRequestValue> ProcessRequestValues =>
        Set<ProcessRequestValue>();

    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();

    public DbSet<ProcessTemplate> ProcessTemplates => Set<ProcessTemplate>();

    public DbSet<TemplateRequestField> TemplateRequestFields =>
        Set<TemplateRequestField>();

    public DbSet<TemplateRequestFieldOption> TemplateRequestFieldOptions =>
        Set<TemplateRequestFieldOption>();

    public DbSet<TemplateStep> TemplateSteps => Set<TemplateStep>();

    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserFunctionalRole> UserFunctionalRoles =>
        Set<UserFunctionalRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntablyDbContext).Assembly);
    }
}

using Intably.Application.Permissions;
using Intably.Application.Processes;
using Intably.Application.MyWork;
using Intably.Application.Roles;
using Intably.Application.Templates;
using Intably.Application.Users;
using Intably.Infrastructure.Permissions;
using Intably.Infrastructure.Persistence;
using Intably.Infrastructure.Processes;
using Intably.Infrastructure.MyWork;
using Intably.Infrastructure.Roles;
using Intably.Infrastructure.Templates;
using Intably.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "The 'Database' connection string is required.");

        services.AddDbContext<IntablyDbContext>(
            options => options.UseSqlServer(connectionString));
        services.AddSingleton(provider =>
        {
            var currentConfiguration =
                provider.GetRequiredService<IConfiguration>();
            return new FirstAdminOptions(
                currentConfiguration[
                    $"{FirstAdminOptions.SectionName}:EntraTenantId"] ?? "",
                currentConfiguration[
                    $"{FirstAdminOptions.SectionName}:EntraObjectId"] ?? "");
        });
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<
            IFunctionalRoleAdministrationService,
            FunctionalRoleAdministrationService>();
        services.AddScoped<IFunctionalRoleLookupService, FunctionalRoleLookupService>();
        services.AddScoped<IPermissionGrantService, PermissionGrantService>();
        services.AddScoped<IMyWorkService, MyWorkService>();
        services.AddScoped<IProcessService, ProcessService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<UserProvisioningService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IUserLookupService, UserLookupService>();
        services
            .AddHealthChecks()
            .AddDbContextCheck<IntablyDbContext>("database");

        return services;
    }
}

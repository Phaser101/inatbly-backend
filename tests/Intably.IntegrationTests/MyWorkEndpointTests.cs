using System.Net;
using System.Net.Http.Json;
using Intably.Application.MyWork;
using Intably.Application.Processes;
using Intably.Application.Templates;
using Intably.Application.Users;
using Intably.Domain.Permissions;
using Intably.Domain.Roles;
using Intably.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.IntegrationTests;

public sealed class MyWorkEndpointTests
{
    [Fact]
    public async Task Get_RequiresViewMyWork()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("my-work-denied");

        var response = await client.GetAsync("/api/my-work");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsAssignedEligibleAndRecentlyCompletedWork()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        using var otherClient = factory.CreateAuthenticatedClient(
            "other-my-work-user",
            "Other My Work User",
            "other-my-work@example.com");

        var currentUser = await client.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        var otherUser = await otherClient.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        Assert.NotNull(currentUser);
        Assert.NotNull(otherUser);

        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.ViewMyWork);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.ViewProcesses);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.StartProcesses);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.CreateTemplates);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.PublishTemplates);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.ManageRoles);

        var role = await SeedRoleAsync(
            factory,
            currentUser.Grg,
            otherUser.Grg);
        var template = await CreateTemplateAsync(
            client,
            role,
            currentUser,
            otherUser);
        var publishResponse = await client.PostAsync(
            $"/api/templates/{template.Ptrg}/publish",
            null);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var startResponse = await client.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(template.Ptrg, "My work process", []));
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        var process =
            await startResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.NotNull(process);

        var initialItems = await client.GetFromJsonAsync<MyWorkItem[]>(
            "/api/my-work");
        Assert.NotNull(initialItems);
        Assert.Equal(3, initialItems.Length);
        Assert.DoesNotContain(
            initialItems,
            item => item.StepTitle == "Assigned elsewhere");

        var assigned = Assert.Single(
            initialItems,
            item => item.StepTitle == "Assigned to me");
        Assert.True(assigned.AssignedToCurrentUser);
        Assert.True(assigned.EligibleForCurrentUser);
        Assert.False(assigned.RecentlyCompleted);

        var roleEligible = Assert.Single(
            initialItems,
            item => item.StepTitle == "Eligible role work");
        Assert.False(roleEligible.AssignedToCurrentUser);
        Assert.True(roleEligible.EligibleForCurrentUser);

        var everyoneEligible = Assert.Single(
            initialItems,
            item => item.StepTitle == "Everyone work");
        Assert.False(everyoneEligible.AssignedToCurrentUser);
        Assert.True(everyoneEligible.EligibleForCurrentUser);

        var assignedStep = process.Steps.Single(
            step => step.Title == "Assigned to me");
        var completeResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{process.Pirg}/steps/{assignedStep.Psrg}/status",
            new SetProcessStepStatusRequest(
                "Complete",
                null,
                assignedStep.RowVersion));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var completedItems = await client.GetFromJsonAsync<MyWorkItem[]>(
            "/api/my-work");
        var recentlyCompleted = Assert.Single(
            completedItems!,
            item => item.StepTitle == "Assigned to me");
        Assert.True(recentlyCompleted.AssignedToCurrentUser);
        Assert.True(recentlyCompleted.EligibleForCurrentUser);
        Assert.True(recentlyCompleted.RecentlyCompleted);
        Assert.Equal("Complete", recentlyCompleted.Status);

        await SetCompletedAtAsync(
            factory,
            assignedStep.Psrg,
            DateTimeOffset.UtcNow.AddDays(-15));
        var afterCutoff = await client.GetFromJsonAsync<MyWorkItem[]>(
            "/api/my-work");
        Assert.DoesNotContain(
            afterCutoff!,
            item => item.StepTitle == "Assigned to me");

        var archiveResponse = await client.DeleteAsync(
            $"/api/functional-roles/{role.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var afterArchive = await client.GetFromJsonAsync<MyWorkItem[]>(
            "/api/my-work");
        Assert.DoesNotContain(
            afterArchive!,
            item => item.StepTitle == "Eligible role work");
        Assert.Contains(
            afterArchive!,
            item => item.StepTitle == "Everyone work");

        var roleStep = process.Steps.Single(
            step => step.Title == "Eligible role work");
        var forbiddenStatusResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{process.Pirg}/steps/{roleStep.Psrg}/status",
            new SetProcessStepStatusRequest(
                "InProgress",
                null,
                roleStep.RowVersion));
        var eligibleAssignees =
            await client.GetFromJsonAsync<EligibleAssignee[]>(
                $"/api/processes/{process.Pirg}/steps/{roleStep.Psrg}/eligible-assignees");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenStatusResponse.StatusCode);
        Assert.Empty(eligibleAssignees!);
    }

    private static async Task<FunctionalRole> SeedRoleAsync(
        IntablyApiFactory factory,
        Guid currentUserId,
        Guid otherUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        var now = DateTimeOffset.UtcNow;
        var role = FunctionalRole.Create(
            "My Work Approver",
            "Exercises role-based My Work selection.",
            now);
        dbContext.FunctionalRoles.Add(role);
        dbContext.UserFunctionalRoles.AddRange(
            new UserFunctionalRole(currentUserId, role.Id, now),
            new UserFunctionalRole(otherUserId, role.Id, now));
        await dbContext.SaveChangesAsync();
        return role;
    }

    private static async Task<TemplateDetails> CreateTemplateAsync(
        HttpClient client,
        FunctionalRole role,
        CurrentUserProfile currentUser,
        CurrentUserProfile otherUser)
    {
        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new SaveTemplateRequest(
                "My Work template",
                "Covers My Work selection behavior.",
                [],
                [
                    new SaveTemplateStep(
                        "Assigned to me",
                        role.Id,
                        role.Name,
                        "",
                        null,
                        currentUser.Grg,
                        currentUser.DisplayName,
                        2,
                        false),
                    new SaveTemplateStep(
                        "Eligible role work",
                        role.Id,
                        role.Name,
                        "",
                        null,
                        null,
                        null,
                        1,
                        false),
                    new SaveTemplateStep(
                        "Everyone work",
                        null,
                        "Everyone",
                        "",
                        null,
                        null,
                        null,
                        null,
                        false),
                    new SaveTemplateStep(
                        "Assigned elsewhere",
                        role.Id,
                        role.Name,
                        "",
                        null,
                        otherUser.Grg,
                        otherUser.DisplayName,
                        3,
                        false),
                ]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TemplateDetails>())!;
    }

    private static async Task SetCompletedAtAsync(
        IntablyApiFactory factory,
        Guid stepId,
        DateTimeOffset completedAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ProcessSteps SET CompletedAtUtc = {completedAtUtc} WHERE psrg = {stepId}");
    }
}

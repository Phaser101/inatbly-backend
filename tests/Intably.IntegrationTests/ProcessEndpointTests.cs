using System.Net;
using System.Net.Http.Json;
using Intably.Application.Permissions;
using Intably.Application.Processes;
using Intably.Application.Templates;
using Intably.Application.Users;
using Intably.Domain.Permissions;

namespace Intably.IntegrationTests;

public sealed class ProcessEndpointTests
{
    [Fact]
    public async Task Start_RequiresPublishedTemplateAndRequiredValues()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.ManageTemplates);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.StartProcesses);
        var template = await CreateTemplateAsync(client);
        var request = new StartProcessRequest(
            template.Ptrg,
            "Release 1.0",
            []);

        var draftResponse = await client.PostAsJsonAsync(
            "/api/processes",
            request);
        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);

        var publishResponse = await client.PostAsync(
            $"/api/templates/{template.Ptrg}/publish",
            null);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var missingValueResponse = await client.PostAsJsonAsync(
            "/api/processes",
            request);
        Assert.Equal(HttpStatusCode.BadRequest, missingValueResponse.StatusCode);
    }

    [Fact]
    public async Task ProcessLifecycle_SnapshotsMutatesAuditsAndExports()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.ManageTemplates);
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.StartProcesses);
        var actor = await client.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        Assert.NotNull(actor);
        var template = await CreateTemplateAsync(client);
        await client.PostAsync($"/api/templates/{template.Ptrg}/publish", null);

        var startResponse = await client.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(
                template.Ptrg,
                "Release 1.0",
                [
                    new StartProcessRequestValue(
                        template.RequestFields.Single().Rfrg,
                        "1.0"),
                ]));
        var started =
            await startResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.NotNull(started);
        Assert.Equal(template.Ptrg, started.Ptrg);
        Assert.Equal("Release name", started.RequestValues.Single().Label);
        Assert.Equal("text", started.RequestValues.Single().Type);
        Assert.True(started.RequestValues.Single().Required);
        Assert.Equal(
            template.Steps.Single().Ptsrg,
            started.Steps.Single().Ptsrg);
        Assert.Equal(1, started.Steps.Single().Order);

        var step = started.Steps.Single();
        var candidates = await client.GetFromJsonAsync<EligibleAssignee[]>(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/eligible-assignees");
        Assert.NotNull(candidates);
        Assert.Contains(candidates, candidate => candidate.Grg == actor.Grg);

        using var otherClient = factory.CreateAuthenticatedClient(
            "other-user",
            "Other User",
            "other@example.com");
        await otherClient.GetAsync("/api/users/me");
        await factory.GrantPermissionAsync(
            otherClient,
            "other-user",
            ApplicationPermission.ViewProcesses);
        var forbiddenResponse = await otherClient.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/assignment",
            new AssignProcessStepRequest(null, step.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        var assignmentResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/assignment",
            new AssignProcessStepRequest(actor.Grg, step.RowVersion));
        Assert.True(
            assignmentResponse.StatusCode == HttpStatusCode.OK,
            await assignmentResponse.Content.ReadAsStringAsync());
        var assigned =
            await assignmentResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.NotNull(assigned);
        Assert.Equal(actor.DisplayName, assigned.Steps.Single().Assignee);

        var inProgressResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/status",
            new SetProcessStepStatusRequest(
                "InProgress",
                null,
                assigned.Steps.Single().RowVersion));
        var inProgress =
            await inProgressResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.Equal(HttpStatusCode.OK, inProgressResponse.StatusCode);
        Assert.NotNull(inProgress);

        var staleResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/status",
            new SetProcessStepStatusRequest(
                "Blocked",
                "Waiting",
                assigned.Steps.Single().RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        var missingNoteResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/status",
            new SetProcessStepStatusRequest(
                "Complete",
                null,
                inProgress.Steps.Single().RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, missingNoteResponse.StatusCode);

        var completeResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/status",
            new SetProcessStepStatusRequest(
                "Complete",
                "Validated",
                inProgress.Steps.Single().RowVersion));
        var completed =
            await completeResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.NotNull(completed);

        var closeResponse = await client.PostAsJsonAsync(
            $"/api/processes/{started.Pirg}/close",
            new CloseProcessRequest("Ready to ship", completed.RowVersion));
        var closed =
            await closeResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        Assert.NotNull(closed);
        Assert.Equal("Closed", closed.Status);

        var list = await client.GetFromJsonAsync<ProcessSummary[]>(
            "/api/processes");
        var details = await client.GetFromJsonAsync<ProcessDetails>(
            $"/api/processes/{started.Pirg}");
        var timeline = await client.GetFromJsonAsync<ProcessTimelineEvent[]>(
            $"/api/processes/{started.Pirg}/timeline");
        var exportResponse = await client.GetAsync(
            $"/api/processes/{started.Pirg}/export");
        var markdown = await exportResponse.Content.ReadAsStringAsync();

        var summary = Assert.Single(list!, item => item.Pirg == started.Pirg);
        Assert.Equal(1, summary.CompletedStepCount);
        Assert.Equal(0, summary.BlockedStepCount);
        Assert.Equal(1, summary.StepCount);
        Assert.Contains(actor.DisplayName, summary.Assignees);
        Assert.Contains("Complete", summary.StepStatuses);
        Assert.Equal("Ready to ship", summary.FinalNote);
        Assert.Equal("Closed", details!.Status);
        Assert.Contains(timeline!, item => item.Action == "Assignment changed");
        Assert.Contains(timeline!, item => item.Action == "Process closed");
        Assert.Equal("text/markdown", exportResponse.Content.Headers.ContentType!.MediaType);
        Assert.Contains("# Release 1.0", markdown);
        Assert.Contains("### 1. Validate release", markdown);
        Assert.Contains("Ready to ship", markdown);
    }

    [Fact]
    public async Task FirstLoginWithoutPermissions_CannotAccessProcessResources()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient("first-login-user");

        var deniedReads = new[]
        {
            await client.GetAsync("/api/processes"),
            await client.GetAsync($"/api/processes/{Guid.NewGuid()}"),
            await client.GetAsync(
                $"/api/processes/{Guid.NewGuid()}/timeline"),
            await client.GetAsync(
                $"/api/processes/{Guid.NewGuid()}/export"),
            await client.GetAsync(
                $"/api/processes/{Guid.NewGuid()}/steps/{Guid.NewGuid()}/eligible-assignees"),
        };
        var deniedStart = await client.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(Guid.NewGuid(), "Denied", []));
        var deniedStepMutation = await client.PatchAsJsonAsync(
            $"/api/processes/{Guid.NewGuid()}/steps/{Guid.NewGuid()}/status",
            new SetProcessStepStatusRequest("InProgress", null, "row-version"));

        Assert.All(
            deniedReads,
            response => Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode));
        Assert.Equal(HttpStatusCode.Forbidden, deniedStart.StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            deniedStepMutation.StatusCode);
    }

    [Fact]
    public async Task ManageProcesses_ImpliedPermissionsAllowReadsAndStart()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var templateManager = factory.CreateAuthenticatedClient(
            "template-manager");
        await factory.GrantPermissionAsync(
            templateManager,
            "template-manager",
            ApplicationPermission.ManageTemplates);
        var template = await CreateTemplateAsync(templateManager);
        await templateManager.PostAsync(
            $"/api/templates/{template.Ptrg}/publish",
            null);

        using var processManager = factory.CreateAuthenticatedClient(
            "process-manager");
        await factory.GrantPermissionAsync(
            processManager,
            "process-manager",
            ApplicationPermission.ManageProcesses);

        var profile = await processManager.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        var templatesResponse = await processManager.GetAsync("/api/templates");
        var startResponse = await processManager.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(
                template.Ptrg,
                "Managed release",
                [
                    new StartProcessRequestValue(
                        template.RequestFields.Single().Rfrg,
                        "2.0"),
                ]));
        var started = await startResponse.Content
            .ReadFromJsonAsync<ProcessDetails>();
        var detailsResponse = await processManager.GetAsync(
            $"/api/processes/{started!.Pirg}");

        Assert.Contains(
            PermissionContracts.StartProcesses,
            profile!.Permissions);
        Assert.Contains(
            PermissionContracts.ViewProcesses,
            profile.Permissions);
        Assert.Contains(
            PermissionContracts.ViewTemplates,
            profile.Permissions);
        Assert.Equal(HttpStatusCode.OK, templatesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
    }

    private static async Task<TemplateDetails> CreateTemplateAsync(
        HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new SaveTemplateRequest(
                "Release readiness",
                "Coordinates release validation.",
                [
                    new SaveTemplateRequestField(
                        "Release name",
                        "text",
                        true,
                        "",
                        "manual",
                        null,
                        null,
                        null,
                        []),
                ],
                [
                    new SaveTemplateStep(
                        "Validate release",
                        null,
                        "QA",
                        "Run release checks.",
                        null,
                        null,
                        null,
                        1,
                        true),
                ]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TemplateDetails>())!;
    }
}

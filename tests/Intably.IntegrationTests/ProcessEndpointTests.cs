using System.Net;
using System.Net.Http.Json;
using Intably.Application.Permissions;
using Intably.Application.Processes;
using Intably.Application.Templates;
using Intably.Application.Users;
using Intably.Domain.Permissions;
using Intably.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.IntegrationTests;

public sealed class ProcessEndpointTests
{
    [Fact]
    public async Task Start_RequiresPublishedTemplateAndRequiredValues()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(
            factory,
            client,
            "integration-test-user");
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
        await GrantTemplatePermissionsAsync(
            factory,
            client,
            "integration-test-user");
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
                    new StartProcessInformationValue(
                        template.InformationFields.Single(field =>
                            field.Kind == "LaunchInput").Rfrg,
                        "1.0"),
                ]));
        var started =
            await startResponse.Content.ReadFromJsonAsync<ProcessDetails>();
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.NotNull(started);
        Assert.Equal(template.Ptrg, started.Ptrg);
        var launchInformation = started.InformationValues.Single(value =>
            value.Kind == "LaunchInput");
        var outputInformation = started.InformationValues.Single(value =>
            value.Kind == "StepOutput");
        Assert.Equal("Release name", launchInformation.Label);
        Assert.Equal("text", launchInformation.Type);
        Assert.True(launchInformation.Required);
        Assert.Equal(string.Empty, outputInformation.Value);
        Assert.Equal(
            template.Steps.Single().Ptsrg,
            started.Steps.Single().Ptsrg);
        Assert.Equal(
            template.Groups.Single().Ptsgrg,
            started.Groups.Single().Ptsgrg);
        Assert.Equal(
            started.Groups.Single().Psgrg,
            started.Steps.Single().Psgrg);
        Assert.Equal(1, started.Steps.Single().Order);

        var step = started.Steps.Single();
        var candidates = await client.GetFromJsonAsync<EligibleAssignee[]>(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/eligible-assignees");
        Assert.NotNull(candidates);
        Assert.Contains(candidates, candidate => candidate.Grg == actor.Grg);

        var missingOutputResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{step.Psrg}/status",
            new SetProcessStepStatusRequest(
                "Complete",
                "Premature",
                step.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, missingOutputResponse.StatusCode);

        var launchUpdateResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{launchInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "1.0.1",
                launchInformation.RowVersion));
        var launchUpdated = await launchUpdateResponse.Content
            .ReadFromJsonAsync<ProcessDetails>();
        Assert.Equal(HttpStatusCode.OK, launchUpdateResponse.StatusCode);
        Assert.Contains("1.0.1", launchUpdated!.Context);
        var staleInformationResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{launchInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "stale",
                launchInformation.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleInformationResponse.StatusCode);

        var invalidOutputResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{outputInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "Unknown",
                outputInformation.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, invalidOutputResponse.StatusCode);
        var outputUpdateResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{outputInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "Passed",
                outputInformation.RowVersion));
        Assert.Equal(HttpStatusCode.OK, outputUpdateResponse.StatusCode);

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

        var currentLaunchInformation = launchUpdated.InformationValues.Single(
            value => value.Rfrg == launchInformation.Rfrg);
        var forbiddenInformationResponse = await otherClient.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{launchInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "forbidden",
                currentLaunchInformation.RowVersion));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenInformationResponse.StatusCode);
        await factory.GrantPermissionAsync(
            otherClient,
            "other-user",
            ApplicationPermission.UpdateProcessInformation);
        var globalInformationResponse = await otherClient.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{launchInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "Globally updated",
                currentLaunchInformation.RowVersion));
        Assert.Equal(HttpStatusCode.OK, globalInformationResponse.StatusCode);

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
        var closedInformation = closed.InformationValues.Single(
            value => value.Rfrg == launchInformation.Rfrg);
        var closedInformationResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{launchInformation.Rfrg}",
            new UpdateProcessInformationRequest(
                "closed",
                closedInformation.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, closedInformationResponse.StatusCode);

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
        Assert.Contains(
            timeline!,
            item => item.Action == "Process information updated");
        Assert.Contains(
            timeline!,
            item => item.Action == "Process information updated"
                && item.AffectedItem == "Validation result"
                && item.Psrg == step.Psrg);
        Assert.Contains(timeline!, item => item.Action == "Process closed");
        Assert.Equal("text/markdown", exportResponse.Content.Headers.ContentType!.MediaType);
        Assert.Contains("# Release 1.0", markdown);
        Assert.Contains("### 1. Release validation", markdown);
        Assert.Contains("#### 1.1. Validate release", markdown);
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
    public async Task InformationContext_TruncatesLongStartAndUpdateValues()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(
            factory,
            client,
            "integration-test-user");
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.StartProcesses);
        var template = await CreateTemplateAsync(client);
        await client.PostAsync($"/api/templates/{template.Ptrg}/publish", null);
        var launchField = template.InformationFields.Single(field =>
            field.Kind == "LaunchInput");
        var initialValue = new string('A', 4000);

        var startResponse = await client.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(
                template.Ptrg,
                "Long context",
                [new StartProcessInformationValue(launchField.Rfrg, initialValue)]));
        var started = await startResponse.Content.ReadFromJsonAsync<ProcessDetails>();

        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.Equal(
            $"Release name: {initialValue}"[..1000],
            started!.Context);
        var information = started.InformationValues.Single(value =>
            value.Rfrg == launchField.Rfrg);
        var updatedValue = new string('B', 4000);
        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{launchField.Rfrg}",
            new UpdateProcessInformationRequest(
                updatedValue,
                information.RowVersion));
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProcessDetails>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(
            $"Release name: {updatedValue}"[..1000],
            updated!.Context);
    }

    [Fact]
    public async Task Persistence_RejectsCrossProcessGroupAndProducerReferences()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(
            factory,
            client,
            "integration-test-user");
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.StartProcesses);
        var template = await CreateTemplateAsync(client);
        await client.PostAsync($"/api/templates/{template.Ptrg}/publish", null);
        var launchField = template.InformationFields.Single(field =>
            field.Kind == "LaunchInput");
        var first = await StartProcessAsync(client, template.Ptrg, launchField.Rfrg, "First");
        var second = await StartProcessAsync(client, template.Ptrg, launchField.Rfrg, "Second");
        var secondStep = second.Steps.Single();
        var foreignGroupId = first.Groups.Single().Psgrg;
        var secondOutput = second.InformationValues.Single(value =>
            value.Kind == "StepOutput");
        var foreignStepId = first.Steps.Single().Psrg;

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ProcessSteps SET ProcessStepGroupId = {foreignGroupId} WHERE psrg = {secondStep.Psrg}"));
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ProcessRequestValues SET ProducingProcessStepId = {foreignStepId} WHERE ProcessId = {second.Pirg} AND SourceRequestFieldId = {secondOutput.Rfrg}"));
    }

    [Fact]
    public async Task SequentialProcess_RejectsLaterStepUntilEarlierStepCompletes()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(
            factory,
            client,
            "integration-test-user");
        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.StartProcesses);

        var templateResponse = await client.PostAsJsonAsync(
            "/api/templates",
            CreateSequentialTemplateRequest());
        var template = await templateResponse.Content
            .ReadFromJsonAsync<TemplateDetails>();
        await client.PostAsync($"/api/templates/{template!.Ptrg}/publish", null);
        var startResponse = await client.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(template.Ptrg, "Sequential run", []));
        var started = await startResponse.Content
            .ReadFromJsonAsync<ProcessDetails>();
        var steps = started!.Steps.OrderBy(step => step.Order).ToArray();
        var output = started.InformationValues.Single();

        var unavailableOutputResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{output.Rfrg}",
            new UpdateProcessInformationRequest(
                "Too early",
                output.RowVersion));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            unavailableOutputResponse.StatusCode);

        var blockedResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{steps[1].Psrg}/status",
            new SetProcessStepStatusRequest(
                "InProgress",
                null,
                steps[1].RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, blockedResponse.StatusCode);

        var firstResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/steps/{steps[0].Psrg}/status",
            new SetProcessStepStatusRequest(
                "Complete",
                null,
                steps[0].RowVersion));
        var afterFirst = await firstResponse.Content
            .ReadFromJsonAsync<ProcessDetails>();

        Assert.Equal("Sequential", started.Groups.Single().ExecutionMode);
        Assert.False(steps[1].IsAvailable);
        Assert.True(afterFirst!.Steps.Single(step => step.Order == 2).IsAvailable);
        var availableOutputResponse = await client.PatchAsJsonAsync(
            $"/api/processes/{started.Pirg}/information/{output.Rfrg}",
            new UpdateProcessInformationRequest(
                "Now available",
                output.RowVersion));
        Assert.Equal(HttpStatusCode.OK, availableOutputResponse.StatusCode);
    }

    private static SaveTemplateRequest CreateSequentialTemplateRequest()
    {
        var groupId = Guid.NewGuid();
        var firstStepId = Guid.NewGuid();
        var secondStepId = Guid.NewGuid();
        return
            new SaveTemplateRequest(
                "Sequential template",
                "Runs steps in order.",
                [
                    new SaveTemplateInformationField(
                        "Second result",
                        "text",
                        false,
                        "",
                        "StepOutput",
                        false,
                        secondStepId,
                        []),
                ],
                [
                    new SaveTemplateStepGroup(
                        groupId,
                        "Sequential steps",
                        "",
                        1,
                        "Sequential",
                        []),
                ],
                [
                    new SaveTemplateStep(
                        firstStepId,
                        groupId,
                        1,
                        "First",
                        null,
                        "",
                        "",
                        null,
                        null,
                        null,
                        null,
                        false),
                    new SaveTemplateStep(
                        secondStepId,
                        groupId,
                        2,
                        "Second",
                        null,
                        "",
                        "",
                        null,
                        null,
                        null,
                        null,
                        false),
                ]);
    }

    [Fact]
    public async Task DetailedProcessPermissions_AllowReadsAndStart()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var templateManager = factory.CreateAuthenticatedClient(
            "template-manager");
        await GrantTemplatePermissionsAsync(
            factory,
            templateManager,
            "template-manager");
        var template = await CreateTemplateAsync(templateManager);
        await templateManager.PostAsync(
            $"/api/templates/{template.Ptrg}/publish",
            null);

        using var processManager = factory.CreateAuthenticatedClient(
            "process-manager");
        await factory.GrantPermissionAsync(
            processManager,
            "process-manager",
            ApplicationPermission.StartProcesses);
        await factory.GrantPermissionAsync(
            processManager,
            "process-manager",
            ApplicationPermission.UpdateProcessSteps);
        await factory.GrantPermissionAsync(
            processManager,
            "process-manager",
            ApplicationPermission.AssignProcessSteps);
        await factory.GrantPermissionAsync(
            processManager,
            "process-manager",
            ApplicationPermission.CloseProcesses);

        var profile = await processManager.GetFromJsonAsync<CurrentUserProfile>(
            "/api/users/me");
        var templatesResponse = await processManager.GetAsync("/api/templates");
        var startResponse = await processManager.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(
                template.Ptrg,
                "Managed release",
                [
                    new StartProcessInformationValue(
                        template.InformationFields.Single(field =>
                            field.Kind == "LaunchInput").Rfrg,
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
        Assert.Contains(
            PermissionContracts.UpdateProcessSteps,
            profile.Permissions);
        Assert.Contains(
            PermissionContracts.AssignProcessSteps,
            profile.Permissions);
        Assert.Contains(
            PermissionContracts.CloseProcesses,
            profile.Permissions);
        Assert.Equal(HttpStatusCode.OK, templatesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
    }

    private static async Task GrantTemplatePermissionsAsync(
        IntablyApiFactory factory,
        HttpClient client,
        string externalUserId)
    {
        foreach (var permission in new[]
                 {
                     ApplicationPermission.CreateTemplates,
                     ApplicationPermission.EditTemplates,
                     ApplicationPermission.PublishTemplates,
                     ApplicationPermission.ArchiveTemplates,
                 })
        {
            await factory.GrantPermissionAsync(
                client,
                externalUserId,
                permission);
        }
    }

    private static async Task<TemplateDetails> CreateTemplateAsync(
        HttpClient client)
    {
        var groupId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            "/api/templates",
            new SaveTemplateRequest(
                "Release readiness",
                "Coordinates release validation.",
                [
                    new SaveTemplateInformationField(
                        "Release name",
                        "text",
                        true,
                        "",
                        "LaunchInput",
                        true,
                        null,
                        []),
                    new SaveTemplateInformationField(
                        "Validation result",
                        "select",
                        true,
                        "",
                        "StepOutput",
                        false,
                        stepId,
                        ["Passed", "Failed"]),
                ],
                [
                    new SaveTemplateStepGroup(
                        groupId,
                        "Release validation",
                        "",
                        1,
                        "Parallel",
                        []),
                ],
                [
                    new SaveTemplateStep(
                        stepId,
                        groupId,
                        1,
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

    private static async Task<ProcessDetails> StartProcessAsync(
        HttpClient client,
        Guid ptrg,
        Guid rfrg,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/processes",
            new StartProcessRequest(
                ptrg,
                name,
                [new StartProcessInformationValue(rfrg, name)]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProcessDetails>())!;
    }
}

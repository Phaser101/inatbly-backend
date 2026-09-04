using System.Net;
using System.Net.Http.Json;
using Intably.Application.Templates;
using Intably.Domain.Permissions;
using Intably.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Intably.IntegrationTests;

public sealed class TemplateEndpointTests
{
    [Fact]
    public async Task Draft_AllowsIncompleteContent_ButPublishRejectsIt()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var request = new SaveTemplateRequest(
            "Incomplete template",
            "",
            Enumerable.Range(1, 5)
                .Select(_ => new SaveTemplateInformationField(
                    "",
                    "text",
                    false,
                    "",
                    "StepOutput",
                    true,
                    null,
                    []))
                .ToArray(),
            [
                new SaveTemplateStepGroup(
                    Guid.NewGuid(),
                    "",
                    "",
                    1,
                    "Parallel",
                    []),
            ],
            []);

        var createResponse = await client.PostAsJsonAsync(
            "/api/templates",
            request);
        var created =
            await createResponse.Content.ReadFromJsonAsync<TemplateDetails>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        var publishResponse = await client.PostAsync(
            $"/api/templates/{created.Ptrg}/publish",
            null);
        Assert.Equal(HttpStatusCode.BadRequest, publishResponse.StatusCode);
    }

    [Fact]
    public async Task Publish_AllowsStepWithoutRequiredRole()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var baseRequest = CreateRequest("Role optional template");
        var request = baseRequest with
        {
            Steps =
            [
                new SaveTemplateStep(
                    Guid.NewGuid(),
                    baseRequest.Groups.Single().Ptsgrg,
                    1,
                    "Available to any active user",
                    null,
                    "",
                    "",
                    null,
                    null,
                    null,
                    null,
                    false),
            ],
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/templates",
            request);
        var created =
            await createResponse.Content.ReadFromJsonAsync<TemplateDetails>();
        var publishResponse = await client.PostAsync(
            $"/api/templates/{created!.Ptrg}/publish",
            null);
        var published =
            await publishResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Equal(
            "Any active user",
            published!.Steps.Single().RequiredRole);
    }

    [Fact]
    public async Task TemplateLifecycle_CreatesPublishesDuplicatesAndArchives()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);

        var createResponse = await client.PostAsJsonAsync(
            "/api/templates",
            CreateRequest("Release readiness", "Sequential"));
        var created =
            await createResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Draft", created.Status);
        Assert.Equal("Sequential", created.Groups.Single().ExecutionMode);

        var duplicateNameResponse = await client.PostAsJsonAsync(
            "/api/templates",
            CreateRequest("release readiness"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateNameResponse.StatusCode);
        Assert.Equal(1, created.Version);
        Assert.Single(created.InformationFields);
        Assert.Single(created.Groups);
        Assert.Single(created.Steps);
        Assert.Equal(created.Groups.Single().Ptsgrg, created.Steps.Single().Ptsgrg);

        var publishResponse = await client.PostAsync(
            $"/api/templates/{created.Ptrg}/publish",
            null);
        var published =
            await publishResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.NotNull(published);
        Assert.Equal("Active", published.Status);
        Assert.True(published.HasPublishedOnce);
        Assert.Equal(1, published.Version);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/templates/{created.Ptrg}",
            CreateRequest("Release readiness v2", "Sequential"));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated =
            await updateResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.NotNull(updated);
        Assert.Equal("Release readiness v2", updated.Name);
        Assert.Equal(2, updated.Version);

        var publishedSnapshot = await client.GetFromJsonAsync<TemplateDetails>(
            $"/api/templates/{created.Ptrg}/published");
        Assert.NotNull(publishedSnapshot);
        Assert.Equal("Release readiness", publishedSnapshot.Name);
        Assert.Equal(1, publishedSnapshot.Version);

        var republishResponse = await client.PostAsync(
            $"/api/templates/{created.Ptrg}/publish",
            null);
        var republished =
            await republishResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.OK, republishResponse.StatusCode);
        Assert.NotNull(republished);
        Assert.Equal(2, republished.Version);

        var duplicateResponse = await client.PostAsync(
            $"/api/templates/{created.Ptrg}/duplicate",
            null);
        var duplicate =
            await duplicateResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);
        Assert.NotNull(duplicate);
        Assert.Equal("Release readiness v2 (copy)", duplicate.Name);
        Assert.Equal("Draft", duplicate.Status);
        Assert.Equal("Sequential", duplicate.Groups.Single().ExecutionMode);
        Assert.Equal(
            duplicate.Groups.Single().Ptsgrg,
            duplicate.Steps.Single().Ptsgrg);

        var secondDuplicateResponse = await client.PostAsync(
            $"/api/templates/{created.Ptrg}/duplicate",
            null);
        Assert.Equal(HttpStatusCode.Conflict, secondDuplicateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync(
            $"/api/templates/{created.Ptrg}");
        var templates = await client.GetFromJsonAsync<TemplateSummary[]>(
            "/api/templates");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotNull(templates);
        Assert.Contains(
            templates,
            template =>
                template.Ptrg == created.Ptrg
                && template.Status == "Archived");
        Assert.Contains(templates, template => template.Ptrg == duplicate.Ptrg);
    }

    [Fact]
    public async Task Create_RejectsCyclicGroupPrerequisites()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();
        var request = new SaveTemplateRequest(
            "Cyclic groups",
            "",
            [],
            [
                new SaveTemplateStepGroup(
                    firstGroupId,
                    "First",
                    "",
                    1,
                    "Parallel",
                    [secondGroupId]),
                new SaveTemplateStepGroup(
                    secondGroupId,
                    "Second",
                    "",
                    2,
                    "Parallel",
                    [firstGroupId]),
            ],
            [
                CreateStep(firstGroupId, 1, "First step"),
                CreateStep(secondGroupId, 1, "Second step"),
            ]);

        var response = await client.PostAsJsonAsync("/api/templates", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsUndefinedNumericEnumValues()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var executionRequest = CreateRequest("Invalid execution");
        var invalidExecutionMode = executionRequest with
        {
            Groups =
            [
                executionRequest.Groups.Single() with
                {
                    ExecutionMode = "999",
                },
            ],
        };
        var invalidType = WithInformationEnum(
            CreateRequest("Invalid type"),
            type: "999");
        var invalidKind = WithInformationEnum(
            CreateRequest("Invalid kind"),
            kind: "999");

        var responses = new[]
        {
            await client.PostAsJsonAsync("/api/templates", invalidExecutionMode),
            await client.PostAsJsonAsync("/api/templates", invalidType),
            await client.PostAsJsonAsync("/api/templates", invalidKind),
        };

        Assert.All(
            responses,
            response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
    }

    [Fact]
    public async Task Create_RoundTripsGroupsAndPrerequisites()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var preparationId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var executionStepId = Guid.NewGuid();
        var request = new SaveTemplateRequest(
            "Grouped template",
            "",
            [
                new SaveTemplateInformationField(
                    "Requester",
                    "email",
                    true,
                    "",
                    "LaunchInput",
                    true,
                    null,
                    []),
                new SaveTemplateInformationField(
                    "Result",
                    "select",
                    true,
                    "",
                    "StepOutput",
                    true,
                    executionStepId,
                    ["Pass", "Fail"]),
            ],
            [
                new SaveTemplateStepGroup(
                    preparationId,
                    "Preparation",
                    "Prepare the work.",
                    1,
                    "Parallel",
                    []),
                new SaveTemplateStepGroup(
                    executionId,
                    "Execution",
                    "Execute in order.",
                    2,
                    "Sequential",
                    [preparationId]),
            ],
            [
                CreateStep(preparationId, 1, "Prepare"),
                CreateStep(executionId, 1, "Execute", executionStepId),
            ]);

        var response = await client.PostAsJsonAsync("/api/templates", request);
        var created = await response.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        var preparation = created.Groups.Single(group => group.Order == 1);
        var execution = created.Groups.Single(group => group.Order == 2);
        Assert.Equal("Sequential", execution.ExecutionMode);
        Assert.Equal([preparation.Ptsgrg], execution.PrerequisitePtsgrgs);
        var executionStep = created.Steps.Single(step => step.Title == "Execute");
        var output = created.InformationFields.Single(field =>
            field.Kind == "StepOutput");
        Assert.Equal(executionStep.Ptsrg, output.ProducingPtsrg);
        Assert.All(
            created.Steps,
            step => Assert.Contains(
                created.Groups,
                group => group.Ptsgrg == step.Ptsgrg));
    }

    [Fact]
    public async Task Persistence_RejectsCrossVersionGroupAndProducerReferences()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var createResponse = await client.PostAsJsonAsync(
            "/api/templates",
            CreateOutputTemplateRequest("Constraint template"));
        var created = await createResponse.Content.ReadFromJsonAsync<TemplateDetails>();
        var publishResponse = await client.PostAsync(
            $"/api/templates/{created!.Ptrg}/publish",
            null);
        var published = await publishResponse.Content
            .ReadFromJsonAsync<TemplateDetails>();
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/templates/{created.Ptrg}",
            CreateOutputTemplateRequest("Constraint template v2"));
        var draft = await updateResponse.Content.ReadFromJsonAsync<TemplateDetails>();
        Assert.NotNull(published);
        Assert.NotNull(draft);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IntablyDbContext>();
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TemplateSteps SET TemplateStepGroupId = {published.Groups.Single().Ptsgrg} WHERE ptsrg = {draft.Steps.Single().Ptsrg}"));
        await Assert.ThrowsAsync<SqlException>(() =>
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE TemplateRequestFields SET ProducingTemplateStepId = {published.Steps.Single().Ptsrg} WHERE rfrg = {draft.InformationFields.Single().Rfrg}"));
    }

    [Fact]
    public async Task ReadsRequireViewTemplatesAndCreateRequiresCreateTemplates()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var deniedReads = new[]
        {
            await client.GetAsync("/api/templates"),
            await client.GetAsync($"/api/templates/{Guid.NewGuid()}"),
            await client.GetAsync($"/api/templates/{Guid.NewGuid()}/published"),
        };
        var deniedMutation = await client.PostAsJsonAsync(
            "/api/templates",
            CreateRequest("Denied"));

        Assert.All(
            deniedReads,
            response => Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode));
        Assert.Equal(HttpStatusCode.Forbidden, deniedMutation.StatusCode);

        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.ViewTemplates);
        var allowedRead = await client.GetAsync("/api/templates");
        var stillDeniedMutation = await client.PostAsJsonAsync(
            "/api/templates",
            CreateRequest("Still denied"));

        Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            stillDeniedMutation.StatusCode);

        await factory.GrantPermissionAsync(
            client,
            "integration-test-user",
            ApplicationPermission.CreateTemplates);
        var allowedResponse = await client.PostAsJsonAsync(
            "/api/templates",
            CreateRequest("Allowed"));

        Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
    }

    private static async Task GrantTemplatePermissionsAsync(
        IntablyApiFactory factory,
        HttpClient client)
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
                "integration-test-user",
                permission);
        }
    }

    private static SaveTemplateRequest CreateRequest(
        string name,
        string executionMode = "Parallel")
    {
        var groupId = Guid.NewGuid();
        return new SaveTemplateRequest(
            name,
            "Coordinates release validation.",
            [
                new SaveTemplateInformationField(
                    "Release name",
                    "text",
                    true,
                    "Enter the release name",
                    "LaunchInput",
                    true,
                    null,
                    []),
            ],
            [
                new SaveTemplateStepGroup(
                    groupId,
                    "Release validation",
                    "Release validation steps.",
                    1,
                    executionMode,
                    []),
            ],
            [
                new SaveTemplateStep(
                    Guid.NewGuid(),
                    groupId,
                    1,
                    "Validate release",
                    null,
                    "QA",
                    "Run the release checks.",
                    null,
                    null,
                    null,
                    1,
                    true),
            ]);
    }

    private static SaveTemplateStep CreateStep(
        Guid groupId,
        int order,
        string title,
        Guid? stepId = null)
    {
        return new SaveTemplateStep(
            stepId ?? Guid.NewGuid(),
            groupId,
            order,
            title,
            null,
            "",
            "",
            null,
            null,
            null,
            null,
            false);
    }

    private static SaveTemplateRequest CreateOutputTemplateRequest(string name)
    {
        var groupId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new SaveTemplateRequest(
            name,
            "",
            [
                new SaveTemplateInformationField(
                    "Result",
                    "text",
                    false,
                    "",
                    "StepOutput",
                    false,
                    stepId,
                    []),
            ],
            [
                new SaveTemplateStepGroup(
                    groupId,
                    "Execution",
                    "",
                    1,
                    "Parallel",
                    []),
            ],
            [CreateStep(groupId, 1, "Execute", stepId)]);
    }

    private static SaveTemplateRequest WithInformationEnum(
        SaveTemplateRequest request,
        string? type = null,
        string? kind = null)
    {
        var field = request.InformationFields.Single();
        return request with
        {
            InformationFields =
            [
                field with
                {
                    Type = type ?? field.Type,
                    Kind = kind ?? field.Kind,
                },
            ],
        };
    }
}

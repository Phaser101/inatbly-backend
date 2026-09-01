using System.Net;
using System.Net.Http.Json;
using Intably.Application.Templates;
using Intably.Domain.Permissions;

namespace Intably.IntegrationTests;

public sealed class TemplateEndpointTests
{
    [Fact]
    public async Task Draft_AllowsIncompleteStep_ButPublishRejectsIt()
    {
        await using var factory = new IntablyApiFactory();
        await factory.MigrateDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        await GrantTemplatePermissionsAsync(factory, client);
        var request = CreateRequest("Incomplete template") with
        {
            Steps =
            [
                new SaveTemplateStep(
                    "",
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
        var request = CreateRequest("Role optional template") with
        {
            Steps =
            [
                new SaveTemplateStep(
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
            CreateRequest("Release readiness"));
        var created =
            await createResponse.Content.ReadFromJsonAsync<TemplateDetails>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Draft", created.Status);
        Assert.Equal(1, created.Version);
        Assert.Single(created.RequestFields);
        Assert.Single(created.Steps);

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
            CreateRequest("Release readiness v2"));
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

    private static SaveTemplateRequest CreateRequest(string name)
    {
        return new SaveTemplateRequest(
            name,
            "Coordinates release validation.",
            [
                new SaveTemplateRequestField(
                    "Release name",
                    "text",
                    true,
                    "Enter the release name",
                    "manual",
                    []),
            ],
            [
                new SaveTemplateStep(
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
}

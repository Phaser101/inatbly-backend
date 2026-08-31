using Intably.Api;
using Intably.Api.Authentication;
using Intably.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AdministrationExceptionHandler>();
builder.Services
    .AddOptions<BackendTrustOptions>()
    .BindConfiguration(BackendTrustOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<
    Microsoft.Extensions.Options.IValidateOptions<BackendTrustOptions>,
    BackendTrustOptionsValidator>();
builder.Services
    .AddAuthentication(BackendTrustAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BackendTrustAuthenticationHandler>(
        BackendTrustAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(AuthorizationPolicies.AddTo);
builder.Services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins =
            builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;

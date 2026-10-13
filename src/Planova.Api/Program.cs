using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Planova.Api.Diagnostics;
using Planova.Api.Security;
using Planova.Identity.Api;
using Planova.Identity.Infrastructure;
using Planova.Projects.Api;
using Planova.Projects.Application;
using Planova.Projects.Infrastructure;
using Planova.Api.Projects;
using Planova.Infrastructure;
using Planova.Infrastructure.Persistence;
using Planova.SharedKernel.Diagnostics;
using Planova.SharedKernel.Identity;
using Planova.Web.Components;
using Planova.Web.Navigation;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<HttpIdentityContext>();
builder.Services.AddScoped<ICurrentUser>(services => services.GetRequiredService<HttpIdentityContext>());
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<HttpIdentityContext>());
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
builder.Services.AddScoped<INavigationCapabilityProvider, ServerNavigationCapabilityProvider>();
builder.Services.AddPlanovaAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(PlanovaClaimTypes.UserId)
        .RequireClaim(PlanovaClaimTypes.OrganisationId)
        .Build();
});
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationMiddlewareResultHandler>();
builder.Services.AddAntiforgery();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["code"] ??= "unexpected_error";
        context.ProblemDetails.Extensions["correlationId"] =
            context.HttpContext.Items[CorrelationHeaders.CorrelationId]?.ToString();
        context.ProblemDetails.Extensions.Remove("traceId");
    };
});
builder.Services.AddOpenApi();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddPlanovaInfrastructure(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddProjectsInfrastructure(builder.Configuration);
builder.Services.AddScoped<IProjectIdentityReader, ProjectIdentityReferenceReader>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, EnterpriseIdentityClaimsTransformation>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("identity-database", tags: ["ready"])
    .AddDbContextCheck<PlatformDbContext>("platform-database", tags: ["ready"]);
builder.Services.AddHealthChecks().AddDbContextCheck<ProjectsDbContext>("projects-database", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();
var unhandledRequestFailure = LoggerMessage.Define(
    LogLevel.Error,
    new EventId(2001, "UnhandledRequestFailure"),
    "Unhandled request failure.");

app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    unhandledRequestFailure(app.Logger, exception);
    var isConcurrencyConflict = exception is DbUpdateConcurrencyException;
    await Results.Problem(
        statusCode: isConcurrencyConflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError,
        title: isConcurrencyConflict ? "The resource changed" : "An unexpected error occurred",
        detail: isConcurrencyConflict
            ? "The resource was changed by another operation. Reload it and try again."
            : "The request could not be completed.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = isConcurrencyConflict ? "concurrency_conflict" : "unexpected_error",
            ["correlationId"] = context.Items[CorrelationHeaders.CorrelationId]?.ToString()
        }).ExecuteAsync(context);
}));
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenApi("/openapi/v1.json").AllowAnonymous();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapIdentityEndpoints();
app.MapProjectEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await InitialiseDatabaseAsync(app);
await app.RunAsync();

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:MigrateOnStartup", false))
    {
        return;
    }

    await using var scope = app.Services.CreateAsyncScope();
    var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var projects = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
    await identity.Database.MigrateAsync();
    await platform.Database.MigrateAsync();
    await projects.Database.MigrateAsync();

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue("DevelopmentAuth:Enabled", false))
    {
        await DevelopmentIdentitySeeder.SeedAsync(identity);
    }
}

public partial class Program;

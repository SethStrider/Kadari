using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Kadari.Application;
using Kadari.Infrastructure;
using Kadari.Infrastructure.ProductionReadiness;
using Kadari.Infrastructure.Security;
using Kadari.Application.Abstractions.Access;
using Kadari.WebApi.Access;
using Kadari.WebApi.Configuration;
using Kadari.WebApi.Endpoints;
using Kadari.WebApi.ExceptionHandling;
using Kadari.WebApi.Http;
using Kadari.WebApi.Middleware;
using Kadari.WebApi.OpenApi;
using Kadari.WebApi.RateLimiting;

// Emitting the contract must not require Postgres and a full secret set, so it short-circuits
// before the composition root is touched (PAS-002 §6).
if (args.Contains(OpenApiDocumentEmitter.Switch))
{
    return await OpenApiDocumentEmitter.EmitAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// Every configuration section this host depends on is bound and validated at startup (PAS-002 §11) —
// a missing secret fails the boot with the section named, not a request with a stack trace.
builder.AddValidatedOptions();

// RFC 9457 ProblemDetails is the single error shape for the whole API (API standards, #3). The
// customizer stamps code/traceId/correlationId/instance on every problem, whoever produced it.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI document (consumed by Scalar) with KADARI metadata, the shared error contract, and the
// bearer scheme + per-operation permission requirements (PAS-002 §5) — without those last two the
// published contract cannot say which calls need a token.
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<ApiInfoTransformer>();
    options.AddDocumentTransformer<SecuritySchemeTransformer>();
    options.AddOperationTransformer<ProblemResponsesTransformer>();
    options.AddOperationTransformer<PermissionRequirementTransformer>();
});

// Browser clients must be named; the SPA sends credentials, so a wildcard origin is not available.
const string ClientCorsPolicy = "kadari-client";
var corsOrigins = builder.Configuration.GetSection(ClientCorsOptions.SectionName)
    .Get<ClientCorsOptions>()?.AllowedOrigins ?? [];

builder.Services.AddCors(options => options.AddPolicy(ClientCorsPolicy, policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)   // the client quotes this in support requests
    .AllowCredentials()));

builder.Services.AddKadariRateLimiting();

// 1 MB / 30 s for every endpoint unless it opts into uploads or long-running work (GL-18).
builder.AddKadariRequestLimits();

// Backs the per-request session-liveness check (PAS-002 §10).
builder.Services.AddMemoryCache();

// Authentication (#7c): validate the JWT and expose the real current user.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, JwtCurrentUser>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep 'sub' and role claims as-issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
        };

        // A signed token is not sufficient: its session must still be live (PAS-002 §10). Without this
        // TerminateSession has no effect until the access token expires on its own.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = SessionValidation.EnsureSessionIsActiveAsync,
        };
    });
builder.Services.AddAuthorization();

// Application (CQRS/MediatR + validation pipeline) + Infrastructure (Postgres, S3, sign-in).
builder.Services.AddApplication();
builder.AddInfrastructure();

var app = builder.Build();

// Refuse to serve traffic with placeholder providers or development secrets bound (PAS-002 §3).
// Immediately after Build so a misconfigured deploy crashes instead of coming up healthy-looking.
ProductionReadinessGuard.AssertProductionReady(app.Services, app.Environment);

// Behind a reverse proxy / tunnel (ngrok, ingress) the original scheme arrives in X-Forwarded-Proto —
// honour it so HTTPS redirection doesn't 307 proxied requests (e.g. Twilio webhooks). Trust is
// restricted to configured hops: rate limiting partitions on the resulting client IP, so an
// unrestricted trust boundary would make those limits spoofable (PAS-002 §13.3).
app.UseForwardedHeaders(BuildForwardedHeadersOptions(
    app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value));

// Turn application exceptions into ProblemDetails at the edge. First in the pipeline so it wraps
// everything below it.
app.UseExceptionHandler();

// Ensure/propagate X-Correlation-ID and push it into the log scope. Above UseStatusCodePages so
// framework-generated error bodies carry the id too.
app.UseMiddleware<CorrelationIdMiddleware>();

// Give a body to the status codes the framework returns without throwing — no route matched, no
// credentials, wrong content type. Without this those answers are a bare status with an empty body,
// and the "every error is ProblemDetails" contract only holds for errors that happen to be exceptions.
app.UseStatusCodePages();

app.UseCors(ClientCorsPolicy);

app.UseRequestTimeouts();

// After UseForwardedHeaders (so the client IP is real) and after authentication is configured but
// before endpoints run, so the global limiter can partition authenticated callers by subject.

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Aspire health/liveness endpoints (/health, /alive).
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    // Development: bring the schema to the latest migration on start. Under the AppHost the migration runner has
    // already done it (a no-op here); this keeps a bare `dotnet run` of the API working.
    using (var scope = app.Services.CreateScope())
    {
        await scope.ServiceProvider.GetRequiredService<Kadari.Infrastructure.Persistence.KadariDbContext>()
            .Database.MigrateAsync();
    }

    app.MapOpenApi();               // OpenAPI JSON at /openapi/v1.json
    app.MapScalarApiReference();    // Interactive API reference at /scalar/v1
    app.MapDevelopmentEndpoints();  // Demo helpers (sample household); never mapped outside Development

}

// Skip HTTPS redirection in Development. Under Aspire the SPA reaches the API same-origin through the
// client's nginx container over http (localhost:<assigned>/api). UseHttpsRedirection would 307 those
// proxied calls to the API's own https origin (localhost:7212) — which resolves on this box but points a
// phone at its OWN localhost, breaking device demos, and defeats the same-origin proxy. In every other
// environment TLS is terminated at the ingress and X-Forwarded-Proto (above) already reflects https.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () => Results.Ok(new { service = "Kadari.WebApi", status = "ok" }))
   .WithName("Root")
   .WithSummary("Liveness / smoke endpoint.");

// Feature endpoints — the list lives in KadariApiEndpoints so the OpenAPI emitter maps the same set.
app.MapKadariApiEndpoints();

app.Run();

return 0;

static ForwardedHeadersOptions BuildForwardedHeadersOptions(ReverseProxyOptions proxy)
{
    var options = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
        ForwardLimit = proxy.ForwardLimit,
    };

    // Defaults include loopback; clear them so configuration is the whole truth.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    foreach (var address in proxy.KnownProxies)
    {
        if (IPAddress.TryParse(address, out var parsed))
        {
            options.KnownProxies.Add(parsed);
        }
    }

    foreach (var network in proxy.KnownNetworks)
    {
        var parts = network.Split('/', 2);
        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var length))
        {
            options.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, length));
        }
    }

    // No configured hops means the dev/demo tunnel posture: believe any hop. Options validation
    // rejects this outside Development, so it can only be reached locally.
    if (proxy.TrustsAnyHop)
    {
        options.ForwardLimit = null;
    }

    return options;
}

/// <summary>Exposed so <c>WebApplicationFactory</c>-based tests can reach the composition root.</summary>
public partial class Program;

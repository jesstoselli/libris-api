using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Libris.Api.Endpoints;
using Libris.Infrastructure;
using Libris.Infrastructure.Auth;
using Libris.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Matches nginx's client_max_body_size: a 5 MB cover plus multipart overhead.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 6 * 1024 * 1024);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

// The container port is bound to 127.0.0.1, so only nginx can reach it. Without trusting its
// X-Forwarded-For every request would come from the Docker gateway and share one rate-limit bucket.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, configuredJwt) =>
    {
        var jwtOpts = configuredJwt.Value;

        // Keeps RFC 7519 claim names (sub, email) instead of the legacy WS-Federation mapping.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidAudience = jwtOpts.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Each Argon2id check allocates 64 MB; capping concurrency keeps a login burst
    // from exhausting the VPS that also runs BillFolder.
    options.AddConcurrencyLimiter(AuthEndpoints.RateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 2;
        limiter.QueueLimit = 4;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Global rather than an endpoint policy: an endpoint policy would replace the group's
    // concurrency limiter on login instead of adding to it.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.GetEndpoint()?.Metadata.GetMetadata<LoginEndpointMarker>() is not null
            ? RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    // Read per partition, not at startup, so test hosts can override it.
                    PermitLimit = context.RequestServices
                        .GetRequiredService<IConfiguration>()
                        .GetValue("Auth:LoginPermitPerMinute", 10),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                })
            : RateLimitPartition.GetNoLimiter("unlimited"));
});

var app = builder.Build();

// Single-instance deploy: migrating on boot is simpler than a separate migration job.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/v1/health", async (ApplicationDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        status = canConnect ? "ok" : "degraded",
        version = "0.1.0",
        timestamp = DateTime.UtcNow,
    });
});

app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapGenresEndpoints();
app.MapBooksEndpoints();
app.MapReadingLogsEndpoints();
app.MapCoversEndpoints();
app.MapIsbnEndpoints();
app.MapStatsEndpoints();

await app.RunAsync();

public partial class Program;

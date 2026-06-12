using System.Text;
using System.Threading.RateLimiting;
using Application;
using Infrastructure;
using API.Middleware;
using API.Observability;
using API.Services;
using Infrastructure.Services;
using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);


if (builder.Environment.IsProduction())
{
    builder.Configuration.AddSystemsManager(config =>
    {
        config.Path = "/vacancies/prod";
        config.ReloadAfter = TimeSpan.FromMinutes(5);
        config.Optional = false;
        config.AwsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions
        {
            Region = Amazon.RegionEndpoint.EUCentral1
        };
    });
}

// DSN from SSM (prod) or appsettings.Development.json (dev). Empty DSN → SDK no-op.
builder.WebHost.UseSentry(options =>
{
    builder.Configuration.GetSection("Sentry").Bind(options);
    options.Environment = builder.Environment.EnvironmentName;
    // Sentry SDK 4.x throws on null DSN; empty string is the documented "disabled" sentinel.
    if (string.IsNullOrWhiteSpace(options.Dsn))
        options.Dsn = string.Empty;
    options.SetBeforeSend(SentryPiiScrubber.BeforeSend);
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);


builder.Services.Configure<Application.Common.Configuration.ScoringOptions>(
    builder.Configuration.GetSection(
        Application.Common.Configuration.ScoringOptions.SectionName));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;

        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();


if (builder.Environment.IsProduction())
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    var hasSsl =
        conn.Contains("SslMode=Require", StringComparison.OrdinalIgnoreCase) ||
        conn.Contains("SslMode=VerifyCA", StringComparison.OrdinalIgnoreCase) ||
        conn.Contains("SslMode=VerifyFull", StringComparison.OrdinalIgnoreCase);
    if (!hasSsl)
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection must include SslMode=Require " +
            "(or VerifyCA / VerifyFull) in Production. Refusing to start with " +
            "an unencrypted RDS connection.");
    }
}


var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (builder.Environment.IsProduction() && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must be configured in Production. " +
        "Set the Cors__AllowedOrigins__0 environment variable (e.g. " +
        "https://app.vacancies.com) or appsettings.Production.json. " +
        "Refusing to start with permissive CORS.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("api", httpContext =>
    {
        var userId = httpContext.User?.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = !string.IsNullOrEmpty(userId)
            ? $"user:{userId}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ip:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "rate_limit_exceeded",
            message = "Too many requests. Please slow down."
        }, cancellationToken: ct);
    };
});


var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. " +
        "Development: set it in appsettings.Development.json under Jwt:Key. " +
        "Production: store it in SSM Parameter Store at " +
        "/vacancies/prod/Jwt/Key as SecureString.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vacancies API",
        Version = "v1",
        Description = "Job aggregation platform API"
    });


    c.UseInlineDefinitionsForEnums();

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter your JWT token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});


builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();


// Eagerly resolve so load/fallback decision is visible in boot logs.
_ = app.Services.GetRequiredService<Application.Common.Interfaces.IScoreCalibrator>();


if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider
        .GetRequiredService<Infrastructure.Persistence.AppDbContext>();
    await db.Database.MigrateAsync();
}


app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using PursuitHQ.API.Data;
using PursuitHQ.API.DTOs;
using PursuitHQ.API.Models;
using PursuitHQ.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------------------------------------------------------------------------
// Identity - accounts, password hashing, lockout policy
// ---------------------------------------------------------------------------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;

    options.User.RequireUniqueEmail = true;

    // The "login screen with attempts" requirement.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Password reset links expire after an hour. Identity's default is a day,
// which is a long time for a link that can take over an account - and if it
// expires before you get to it, asking for another one costs nothing.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

// ---------------------------------------------------------------------------
// JWT authentication
//
// AddIdentity above would default to cookie authentication, which is wrong for
// an API consumed by a separate frontend. Setting the default schemes here
// makes JWT the way requests are authenticated.
// ---------------------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Run: dotnet user-secrets set \"Jwt:Key\" \"<32+ character random string>\"");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PursuitHQ",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PursuitHQClient",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

        // Without this, tokens stay valid up to 5 minutes past expiry.
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ITokenService, TokenService>();

// File storage for study materials. Bound from the "FileStorage" section of
// appsettings.json. Swapping to cloud storage later means registering a
// different IFileStorageService here and changing nothing else.
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(FileStorageOptions.SectionName));
// Object storage when it is fully configured, local disk otherwise.
//
// A fallback rather than a hard failure on purpose: local development has no
// bucket, and a half-configured one in production should degrade to something
// that works while you fix it, not refuse to start. The log line says which was
// chosen, because silently writing to the wrong place is the bad outcome here.
var storageOptions = new FileStorageOptions();
builder.Configuration.GetSection(FileStorageOptions.SectionName).Bind(storageOptions);

if (storageOptions.UsesObjectStorage)
{
    builder.Services.AddSingleton<IFileStorageService, S3FileStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

// Pulls text out of PDF, DOCX, PPTX, and XLSX uploads. Powers the text
// preview now, and the AI study tools later.
builder.Services.AddScoped<ITextExtractionService, TextExtractionService>();

// Job search against an external board. A typed HttpClient gives connection
// pooling and a timeout; IMemoryCache keeps repeated searches inside the free
// tier's daily call limit.
builder.Services.AddMemoryCache();

// AI. Everything that talks to Gemini goes through IAiService, so the provider
// can change in one place. A typed HttpClient gives pooling and a timeout.
builder.Services.Configure<AiOptions>(
    builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.AddHttpClient<IAiService, GeminiAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

// Study tools sit on top of IAiService: that one knows how to reach Gemini,
// this one knows what to ask it and what a usable answer looks like.
builder.Services.AddScoped<IStudyToolAiService, StudyToolAiService>();
builder.Services.AddScoped<IStudyChatService, StudyChatService>();

// The merged calendar feed. Scoped, because it holds a DbContext - and because
// the calendar page and the dashboard must not answer differently.
builder.Services.AddScoped<IUserClock, UserClock>();
builder.Services.AddScoped<IConnectionService, ConnectionService>();
builder.Services.AddScoped<IProfilePhotoService, ProfilePhotoService>();
builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();
builder.Services.AddScoped<IResumeAiService, ResumeAiService>();

// Reads a job posting from a URL so a resume can be scored against it. A typed
// HttpClient for pooling and a hard timeout; redirects are followed by hand
// inside the service so every hop can be checked against private addresses.
builder.Services.AddHttpClient<IJobDescriptionFetcher, JobDescriptionFetcher>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false
});

// Email. Which implementation is registered depends on whether a key exists,
// so the reminder pipeline can be built and tested in full before a domain is
// bought - and so a misconfigured server prints emails rather than silently
// dropping them. Resend rejects any from-address on a domain you have not
// verified, which is why FromAddress counts as configuration too.
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

var emailReady =
    !string.IsNullOrWhiteSpace(builder.Configuration["Email:ApiKey"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["Email:FromAddress"]);

if (emailReady)
{
    builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
    {
        client.BaseAddress = new Uri("https://api.resend.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}
else
{
    builder.Services.AddSingleton<IEmailService, ConsoleEmailService>();
}

// Confirmation emails are queued from inside a request and sent afterwards,
// so adding a course never waits on the email provider.
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
builder.Services.AddHostedService<EmailQueueWorker>();
builder.Services.AddScoped<ICreationNotifier, CreationNotifier>();
builder.Services.AddScoped<IAccountNotifier, AccountNotifier>();

// Same idea for messages: queued after the message is safely saved, so the
// send never waits on an email provider and never fails because of one.
builder.Services.AddScoped<IMessageNotifier, MessageNotifier>();

// Connection requests and group invitations. Same queue, no throttle - these
// are single events that need an answer, not a stream.
builder.Services.AddScoped<IRequestNotifier, RequestNotifier>();

// Who is owed a reminder, and the record that stops it being sent twice.
// Scoped, because it holds a DbContext.
builder.Services.AddScoped<INotificationService, NotificationService>();

// Runs the above on a timer inside the API. At deployment this is replaced by
// an external cron calling a secured endpoint - the reminder logic does not
// change, because none of it lives in the timer.
builder.Services.AddHostedService<ReminderBackgroundService>();

// Singleton so the daily counts survive between requests. In-memory, so they
// do not survive a restart and would not be shared across instances - fine for
// a soft guard against runaway loops, not a billing control.
builder.Services.AddSingleton<IAiUsageLimiter, AiUsageLimiter>();

// ---------------------------------------------------------------------------
// Controllers and Swagger
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PursuitHQ API",
        Version = "v1",
        Description = "Student success platform: academics, study tools, internships, and career growth."
    });

    // Adds the "Authorize" button to Swagger so protected endpoints can be
    // tested with a real token.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT here. Swagger adds the \"Bearer \" prefix for you."
    });

    // Microsoft.OpenApi 2.x: schemes are referenced with
    // OpenApiSecuritySchemeReference rather than the old Reference property.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

// ---------------------------------------------------------------------------
// CORS - the Next.js frontend will need this
// ---------------------------------------------------------------------------
// Read from configuration so a deployed frontend can be allowed without a code
// change. Both shapes work, because hosting panels differ: one comma-separated
// value (Cors__AllowedOrigins="https://a.com,https://www.a.com") or indexed keys
// (Cors__AllowedOrigins__0, __1). Localhost only when nothing is configured.
var originList = builder.Configuration["Cors:AllowedOrigins"];

var allowedOrigins = string.IsNullOrWhiteSpace(originList)
    ? builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    : originList.Split(',', StringSplitOptions.RemoveEmptyEntries
                          | StringSplitOptions.TrimEntries);

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "http://localhost:3000" };
}

// ---------------------------------------------------------------------------
// Rate limiting
// ---------------------------------------------------------------------------
//
// The global limit is deliberately generous, because messaging polls: one
// person with one conversation open makes roughly 24 requests a minute without
// doing anything unusual, and somebody with four tabs open is near a hundred.
// A limit tuned for a normal REST app would spend its time rejecting ordinary
// use, and a limit that fires on ordinary use gets raised until it means
// nothing. Reduce this once SignalR replaces the polling.
//
// Signed-in requests are counted per user so that one person on a busy campus
// network cannot exhaust everyone else's allowance; anonymous ones fall back to
// the IP, which is accurate here only because the forwarded-headers middleware
// below runs first.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Sign-in, registration and password reset, by IP rather than by user -
    // the whole point is to limit somebody working through a list of accounts
    // they do not own. Identity's five-attempt lockout protects one account;
    // this protects every account at once.
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(15)
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Two checks on purpose.
//
// "/health" answers without touching anything. It is what the keep-warm ping
// hits every few minutes, and it must stay cheap: a health check that queries
// the database would keep the database awake too, and on a plan that bills by
// compute-hour and sleeps when idle, that ping alone would spend the monthly
// allowance on proving the app is alive.
//
// "/health/ready" does check the database, for when you actually want to know.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

var app = builder.Build();

// ---------------------------------------------------------------------------
// Apply schema migrations at startup.
//
// A managed database starts out empty. Nothing else in the deployment pipeline
// runs "dotnet ef database update", so without this the app boots fine and
// connects fine - which is exactly why /health/ready reports Healthy against an
// empty database - and then throws on the first real query because no tables
// exist. Applying migrations here keeps the deployed schema in step with the
// code that was just deployed, with no manual step to forget.
//
// This is safe because a single instance of this service runs at a time. If
// that ever changes, move this into a separate release command so two starting
// instances cannot race each other applying the same migration.
// ---------------------------------------------------------------------------
using (var migrationScope = app.Services.CreateScope())
{
    var migrationDb = migrationScope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await migrationDb.Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied.");
}

// First in the pipeline, before anything reads the scheme or the client address.
//
// A host like Render terminates TLS at its edge and forwards plain HTTP to the
// container. Without this the app believes every request arrived over HTTP,
// UseHttpsRedirection redirects it, the edge forwards the retry as HTTP again,
// and the browser gives up on a redirect loop. The Known* lists are cleared
// because they default to loopback only, which would ignore the proxy's headers
// entirely - safe here because the only route in is the host's own proxy.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();

app.UseForwardedHeaders(forwarded);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // In production an unhandled exception becomes the same error shape as
    // every other failure, rather than whatever the framework would otherwise
    // return - and never a stack trace.
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

// Order matters: authentication (who are you?) before authorization (are you allowed?).
app.UseAuthentication();
app.UseAuthorization();

// After authentication, so a signed-in request is counted against the user
// rather than against whatever address it arrived from.
app.UseRateLimiter();

if (storageOptions.UsesObjectStorage)
{
    app.Logger.LogInformation(
        "File storage: object storage, bucket \"{Bucket}\" at {Endpoint}.",
        storageOptions.Bucket,
        storageOptions.ServiceUrl);
}
else if (storageOptions.ConfigurationProblem is string problem)
{
    // A warning, not information. Object storage was asked for and could not be
    // built, so this is a deployment that will accept uploads and lose them on
    // the next restart. Saying which setting is wrong turns a day of guessing
    // into a one-line fix.
    app.Logger.LogWarning(
        "File storage: falling back to local disk because {Problem}. "
        + "Uploads will NOT survive a restart on an ephemeral host.",
        problem);
}
else
{
    app.Logger.LogInformation(
        "File storage: local disk (files will not survive a restart on an ephemeral host).");
}

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    // Nothing tagged: liveness only. See the comment where these are registered.
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// ---------------------------------------------------------------------------
// Where every unhandled exception ends up.
//
// The student is told nothing about what went wrong, which is correct - an
// exception message can name a table, a file path or a connection string. What
// they are given is a reference: eight characters they can quote in a bug
// report, printed in the log next to the stack trace it belongs to.
//
// Without that, finding the cause of one 500 means reading a server log by
// timestamp and hoping. With it, the log is searchable by the exact string the
// person who hit the error is looking at.
// ---------------------------------------------------------------------------
app.Map("/error", (HttpContext http, ILoggerFactory loggers) =>
{
    var failure = http.Features.Get<IExceptionHandlerPathFeature>();
    var reference = Guid.NewGuid().ToString("N")[..8];

    // The innermost exception, not the outer one.
    //
    // A failed save arrives as a DbUpdateException wrapping a PostgresException
    // wrapping the actual complaint. The outer message is always the same
    // sentence about an error occurring while saving; the inner one names the
    // constraint. Only the inner one is worth reading.
    var cause = failure?.Error;
    while (cause?.InnerException is not null) cause = cause.InnerException;

    // Type and message go in the message template rather than being left to the
    // stack trace underneath. A log viewer's search filter matches single lines,
    // so anything on a following line is invisible the moment you search for
    // the reference - which is precisely when you are looking for it.
    loggers.CreateLogger("PursuitHQ.UnhandledError").LogError(
        failure?.Error,
        "Unhandled exception. Reference {Reference}. Path {Path}. {ExceptionType}: {ExceptionMessage}",
        reference,
        failure?.Path ?? "unknown",
        cause?.GetType().FullName ?? "unknown",
        cause?.Message ?? "no message");

    return Results.Json(
        new ApiErrorDto(
            "ServerError",
            $"Something went wrong. Please try again. (Reference {reference})"),
        statusCode: StatusCodes.Status500InternalServerError);
});

app.Run();

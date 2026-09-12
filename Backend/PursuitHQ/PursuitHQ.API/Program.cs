using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PursuitHQ.API.Data;
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
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

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
builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();
builder.Services.AddScoped<IResumeAiService, ResumeAiService>();

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
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

// Order matters: authentication (who are you?) before authorization (are you allowed?).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

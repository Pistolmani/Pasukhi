using System.Net;
using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pasukhi.API.Middleware;
using Pasukhi.Application.AI;
using Pasukhi.Application.BotReadiness;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Application.Validators;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Channels;
using Pasukhi.Infrastructure.Consumers;
using Pasukhi.Infrastructure.Data;
using Pasukhi.Infrastructure.Queue;
using Pasukhi.Infrastructure.Services;
using Pasukhi.Infrastructure.Tenant;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/pasukhi-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddDbContext<PasukhiDbContext>(options =>
    options.UseNpgsql(ResolveConnectionString(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<PasukhiDbContext>()
.AddDefaultTokenProviders();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters. Set it via the Jwt__Secret environment variable.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                           ?? new[] { "http://localhost:5173" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IFaqService, FaqService>();
builder.Services.AddScoped<IRuleService, RuleService>();
builder.Services.AddScoped<IFaqMatcher, FaqMatcher>();
builder.Services.AddScoped<IRuleMatcher, RuleMatcher>();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AI"));
builder.Services.AddScoped<IAiPromptBuilder, AiPromptBuilder>();
builder.Services.AddScoped<IAiSafetyChecker, AiSafetyChecker>();
builder.Services.AddScoped<IBusinessPromptService, BusinessPromptService>();
builder.Services.AddScoped<IEscalationService, EscalationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IPlanLimitsService, PlanLimitsService>();
builder.Services.AddScoped<IBillingService, StripeBillingService>();
builder.Services.Configure<BotReadinessOptions>(builder.Configuration.GetSection("BotReadiness"));
builder.Services.AddScoped<IBotReadinessService, BotReadinessService>();
builder.Services.AddHttpClient<IMetaOAuthService, MetaOAuthService>();
builder.Services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddScoped<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
builder.Services.AddScoped<IWebhookResolver, WebhookResolver>();
builder.Services.AddScoped<IMetaWebhookParser, MetaWebhookParser>();

builder.Services.AddHttpClient<IInstagramChannelProvider, InstagramChannelProvider>();
builder.Services.AddHttpClient<IMessengerChannelProvider, MessengerChannelProvider>();
builder.Services.AddHttpClient<IWhatsAppChannelProvider, WhatsAppChannelProvider>();
builder.Services.AddHttpClient<IMessengerProfileService, MessengerProfileService>();
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

// AI provider selection based on configuration
var aiProvider = builder.Configuration.GetValue<string>("AI:Provider")?.ToLowerInvariant() ?? "gemini";
if (aiProvider == "gemini" || aiProvider == "google")
{
    builder.Services.AddHttpClient<IAiService, GeminiService>();
}
else
{
    builder.Services.AddHttpClient<IAiService, OpenAiService>();
}

// In-process message channels (replaces RabbitMQ/MassTransit).
// DropOldest prevents the webhook handler from blocking when the consumer is slow
// (e.g. Gemini latency spikes). Meta retries dropped events; the idempotency
// guard on ExternalMessageId prevents duplicate processing on retry.
var inboundChannel = Channel.CreateBounded<InboundMessageEvent>(new BoundedChannelOptions(512)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = true,
    SingleWriter = false
});
var outboundChannel = Channel.CreateBounded<OutboundMessageReadyEvent>(new BoundedChannelOptions(512)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = true,
    SingleWriter = false
});

builder.Services.AddSingleton(inboundChannel.Writer);
builder.Services.AddSingleton(inboundChannel.Reader);
builder.Services.AddSingleton(outboundChannel.Writer);
builder.Services.AddSingleton(outboundChannel.Reader);

builder.Services.AddScoped<InboundMessageConsumer>();
builder.Services.AddScoped<OutboundMessageConsumer>();
builder.Services.AddHostedService<InboundMessageBackgroundService>();
builder.Services.AddHostedService<OutboundMessageBackgroundService>();

builder.Services.AddHostedService<RefreshTokenCleanupService>();

builder.Services.AddRateLimiter(options =>
{
    // Per-IP: 10 auth requests per minute (login, refresh, OAuth callbacks)
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    // Per-IP: 50 webhook requests per 10 seconds — Meta can send bursts
    options.AddPolicy("webhook", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 50,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;
});

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// Converts a postgres:// URI (what Railway injects via DATABASE_URL) to the
// key=value format Npgsql's connection string builder expects.
static string ResolveConnectionString(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return raw;

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : string.Empty)};" +
           $"SSL Mode=Require;Trust Server Certificate=true";
}

using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pasukhi.API.Middleware;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Validators;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Channels;
using Pasukhi.Infrastructure.Consumers;
using Pasukhi.Infrastructure.Data;
using Pasukhi.Infrastructure.Messaging;
using Pasukhi.Infrastructure.Services;
using Pasukhi.Infrastructure.Tenant;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/pasukhi-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddDbContext<PasukhiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<PasukhiDbContext>()
.AddDefaultTokenProviders();

var jwtSecret = builder.Configuration["Jwt:Secret"]!;
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
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
builder.Services.AddScoped<IWebhookSignatureVerifier, WebhookSignatureVerifier>();
builder.Services.AddScoped<IWebhookResolver, WebhookResolver>();
builder.Services.AddScoped<IMetaWebhookParser, MetaWebhookParser>();

builder.Services.AddHttpClient<IInstagramChannelProvider, InstagramChannelProvider>();
builder.Services.AddHttpClient<IMessengerChannelProvider, MessengerChannelProvider>();
builder.Services.AddHttpClient<IWhatsAppChannelProvider, WhatsAppChannelProvider>();
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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InboundMessageConsumer>(c =>
    {
        c.UseMessageRetry(r => r.Exponential(
            retryLimit: 3,
            minInterval: TimeSpan.FromSeconds(2),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(5)));
    });
    x.AddConsumer<OutboundMessageConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbit = builder.Configuration.GetSection("RabbitMQ");
        var rabbitUrl = rabbit["Url"];
        if (!string.IsNullOrEmpty(rabbitUrl))
        {
            cfg.Host(new Uri(rabbitUrl));
        }
        else
        {
            cfg.Host(rabbit["Host"] ?? "localhost", h =>
            {
                h.Username(rabbit["Username"] ?? "guest");
                h.Password(rabbit["Password"] ?? "guest");
            });
        }

        cfg.ReceiveEndpoint("inbound-message-queue", e =>
        {
            e.PrefetchCount = 16;
            e.UseConsumeFilter(typeof(TenantContextFilter<>), context);
            e.ConfigureConsumer<InboundMessageConsumer>(context);
        });

        cfg.ReceiveEndpoint("outbound-message-queue", e =>
        {
            e.PrefetchCount = 16;
            e.UseConsumeFilter(typeof(TenantContextFilter<>), context);
            e.ConfigureConsumer<OutboundMessageConsumer>(context);
        });
    });
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
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

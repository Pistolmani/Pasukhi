# Codex Task — Phase 0: Project Scaffolding

> Read `AGENTS.md` first. This file describes what to BUILD. `AGENTS.md` describes the rules and patterns.

## Goal

By the end of this task, both projects start and a basic health check endpoint works. No business logic yet. Just the skeleton with the right structure and the right dependencies.

## Repo root

`C:\Users\piros\OneDrive\Desktop\Pasukhi\`

---

## Step 1 — .NET Solution

Run these from the repo root:

```bash
dotnet new sln -n Pasukhi

dotnet new webapi    -n Pasukhi.API           -o src/Pasukhi.API
dotnet new classlib  -n Pasukhi.Application   -o src/Pasukhi.Application
dotnet new classlib  -n Pasukhi.Domain        -o src/Pasukhi.Domain
dotnet new classlib  -n Pasukhi.Infrastructure -o src/Pasukhi.Infrastructure
dotnet new xunit     -n Pasukhi.UnitTests     -o tests/Pasukhi.UnitTests
dotnet new xunit     -n Pasukhi.IntegrationTests -o tests/Pasukhi.IntegrationTests

dotnet sln add src/Pasukhi.API/Pasukhi.API.csproj
dotnet sln add src/Pasukhi.Application/Pasukhi.Application.csproj
dotnet sln add src/Pasukhi.Domain/Pasukhi.Domain.csproj
dotnet sln add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj
dotnet sln add tests/Pasukhi.UnitTests/Pasukhi.UnitTests.csproj
dotnet sln add tests/Pasukhi.IntegrationTests/Pasukhi.IntegrationTests.csproj
```

### Project references (dependency direction: API → App → Domain ← Infra)

```bash
dotnet add src/Pasukhi.API/Pasukhi.API.csproj reference src/Pasukhi.Application/Pasukhi.Application.csproj
dotnet add src/Pasukhi.API/Pasukhi.API.csproj reference src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj
dotnet add src/Pasukhi.Application/Pasukhi.Application.csproj reference src/Pasukhi.Domain/Pasukhi.Domain.csproj
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj reference src/Pasukhi.Application/Pasukhi.Application.csproj
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj reference src/Pasukhi.Domain/Pasukhi.Domain.csproj
dotnet add tests/Pasukhi.UnitTests/Pasukhi.UnitTests.csproj reference src/Pasukhi.Application/Pasukhi.Application.csproj
dotnet add tests/Pasukhi.UnitTests/Pasukhi.UnitTests.csproj reference src/Pasukhi.Domain/Pasukhi.Domain.csproj
```

### NuGet packages

```bash
# Infrastructure
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj package MassTransit.RabbitMQ
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj package Mapster

# API
dotnet add src/Pasukhi.API/Pasukhi.API.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/Pasukhi.API/Pasukhi.API.csproj package FluentValidation.AspNetCore
dotnet add src/Pasukhi.API/Pasukhi.API.csproj package Serilog.AspNetCore
dotnet add src/Pasukhi.API/Pasukhi.API.csproj package Serilog.Sinks.File
dotnet add src/Pasukhi.API/Pasukhi.API.csproj package Swashbuckle.AspNetCore

# EF Core tools (add to Infrastructure for migrations)
dotnet add src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
```

---

## Step 2 — Domain Layer

Create these files. No logic yet — just the entity and enum definitions.

### `src/Pasukhi.Domain/Entities/TenantEntity.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public abstract class TenantEntity
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/Business.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class Business
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<ChannelConnection> ChannelConnections { get; set; } = new List<ChannelConnection>();
}
```

### `src/Pasukhi.Domain/Entities/AdminUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace Pasukhi.Domain.Entities;

public class AdminUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/RefreshToken.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public AdminUser User { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/ChannelConnection.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class ChannelConnection : TenantEntity
{
    public ChannelType ChannelType { get; set; }
    public string ExternalAccountId { get; set; } = string.Empty;
    public string? ExternalAccountName { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastWebhookAt { get; set; }
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
```

### `src/Pasukhi.Domain/Entities/Conversation.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Conversation : TenantEntity
{
    public Guid ChannelConnectionId { get; set; }
    public ChannelConnection ChannelConnection { get; set; } = null!;
    public ChannelType ChannelType { get; set; }
    public string ExternalCustomerId { get; set; } = string.Empty;
    public string? CustomerDisplayName { get; set; }
    public string? CustomerProfilePictureUrl { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.Active;
    public bool IsEscalated { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Escalation> Escalations { get; set; } = new List<Escalation>();
}
```

### `src/Pasukhi.Domain/Entities/Message.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Message : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public MessageDirection Direction { get; set; }
    public MessageType MessageType { get; set; }
    public string? TextContent { get; set; }
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public string ExternalSenderId { get; set; } = string.Empty;
    public string? SenderDisplayName { get; set; }
    public MessageSource Source { get; set; }
    public Guid? MatchedFaqItemId { get; set; }
    public Guid? MatchedRuleId { get; set; }
    public double? AiConfidenceScore { get; set; }
    public string ExternalMessageId { get; set; } = string.Empty;
    public string? ExternalTimestamp { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
    public string? RawPayloadJson { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/FaqItem.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class FaqItem : TenantEntity
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public int MatchCount { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/AutomationRule.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class AutomationRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public TriggerType TriggerType { get; set; }
    public string TriggerValue { get; set; } = string.Empty;
    public ActionType ActionType { get; set; }
    public string ActionValue { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int MatchCount { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/Escalation.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class Escalation : TenantEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public EscalationReason Reason { get; set; }
    public string? Notes { get; set; }
    public string? AiRejectedResponse { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/BusinessPrompt.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class BusinessPrompt : TenantEntity
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string ToneDescription { get; set; } = "professional and friendly";
    public string EscalationMessage { get; set; } = "Let me connect you with our team.";
    public int MaxAiTokensPerDay { get; set; } = 50000;
    public double AiConfidenceThreshold { get; set; } = 0.7;
    public double FaqConfidenceThreshold { get; set; } = 0.85;
    public bool IsAiEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/BusinessSetting.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class BusinessSetting : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

### `src/Pasukhi.Domain/Entities/DailyMetric.cs`

```csharp
using Pasukhi.Domain.Enums;

namespace Pasukhi.Domain.Entities;

public class DailyMetric : TenantEntity
{
    public DateOnly Date { get; set; }
    public ChannelType? ChannelType { get; set; }
    public int TotalInbound { get; set; }
    public int TotalOutbound { get; set; }
    public int FaqReplies { get; set; }
    public int RuleReplies { get; set; }
    public int AiReplies { get; set; }
    public int Escalations { get; set; }
    public int? AvgResponseTimeMs { get; set; }
}
```

### `src/Pasukhi.Domain/Entities/AuditLog.cs`

```csharp
namespace Pasukhi.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? BusinessId { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### `src/Pasukhi.Domain/Enums/` — create one file per enum

```csharp
// ChannelType.cs
namespace Pasukhi.Domain.Enums;
public enum ChannelType { Instagram = 0, Messenger = 1, WhatsApp = 2 }

// MessageDirection.cs
namespace Pasukhi.Domain.Enums;
public enum MessageDirection { Inbound = 0, Outbound = 1 }

// MessageType.cs
namespace Pasukhi.Domain.Enums;
public enum MessageType { Text = 0, Image = 1, Video = 2, Audio = 3, File = 4, Sticker = 5, StoryReply = 6, StoryMention = 7, Reaction = 8 }

// MessageSource.cs
namespace Pasukhi.Domain.Enums;
public enum MessageSource { Customer = 0, FaqAutoReply = 1, RuleAutoReply = 2, AiAutoReply = 3, OperatorManual = 4 }

// ConversationStatus.cs
namespace Pasukhi.Domain.Enums;
public enum ConversationStatus { Active = 0, Escalated = 1, Resolved = 2, Archived = 3 }

// DeliveryStatus.cs
namespace Pasukhi.Domain.Enums;
public enum DeliveryStatus { Pending = 0, Sent = 1, Delivered = 2, Read = 3, Failed = 4 }

// EscalationReason.cs
namespace Pasukhi.Domain.Enums;
public enum EscalationReason { NoMatch = 0, LowAiConfidence = 1, SafetyCheckFailed = 2, CustomerRequested = 3, OperatorTriggered = 4 }

// TriggerType.cs
namespace Pasukhi.Domain.Enums;
public enum TriggerType { Keyword = 0, Regex = 1, MessageType = 2, TimeOfDay = 3 }

// ActionType.cs
namespace Pasukhi.Domain.Enums;
public enum ActionType { SendReply = 0, TagConversation = 1, Escalate = 2 }

// AdminRole.cs
namespace Pasukhi.Domain.Enums;
public enum AdminRole { SuperAdmin = 0, Operator = 1 }
```

---

## Step 3 — Application Layer Interfaces

These are the contracts. Implementations come later (Phase 1+).

### `src/Pasukhi.Application/Interfaces/ITenantProvider.cs`

```csharp
namespace Pasukhi.Application.Interfaces;

public interface ITenantProvider
{
    Guid BusinessId { get; }
}
```

### `src/Pasukhi.Application/Interfaces/IAuthService.cs`

```csharp
using Pasukhi.Application.DTOs.Auth;

namespace Pasukhi.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string userId);
    Task<AdminUserDto> GetCurrentUserAsync(string userId);
}
```

### `src/Pasukhi.Application/DTOs/Auth/LoginRequest.cs`

```csharp
namespace Pasukhi.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);
```

### `src/Pasukhi.Application/DTOs/Auth/AuthResponse.cs`

```csharp
namespace Pasukhi.Application.DTOs.Auth;

public record AuthResponse(string AccessToken, AdminUserDto User);
```

### `src/Pasukhi.Application/DTOs/Auth/AdminUserDto.cs`

```csharp
namespace Pasukhi.Application.DTOs.Auth;

public record AdminUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid? BusinessId
);
```

---

## Step 4 — Infrastructure: DbContext

### `src/Pasukhi.Infrastructure/Data/PasukhiDbContext.cs`

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;

namespace Pasukhi.Infrastructure.Data;

public class PasukhiDbContext : IdentityDbContext<AdminUser>
{
    private readonly ITenantProvider _tenantProvider;

    public PasukhiDbContext(DbContextOptions<PasukhiDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<ChannelConnection> ChannelConnections => Set<ChannelConnection>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<Escalation> Escalations => Set<Escalation>();
    public DbSet<BusinessPrompt> BusinessPrompts => Set<BusinessPrompt>();
    public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
    public DbSet<DailyMetric> DailyMetrics => Set<DailyMetric>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Tenant isolation — global query filters
        builder.Entity<ChannelConnection>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Conversation>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Message>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<FaqItem>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<AutomationRule>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Escalation>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<BusinessPrompt>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<BusinessSetting>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<DailyMetric>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);

        // Unique constraints
        builder.Entity<Business>().HasIndex(b => b.Slug).IsUnique();
        builder.Entity<ChannelConnection>()
            .HasIndex(c => new { c.ExternalAccountId, c.ChannelType }).IsUnique();
        builder.Entity<BusinessSetting>()
            .HasIndex(s => new { s.BusinessId, s.Key }).IsUnique();

        // RefreshToken
        builder.Entity<RefreshToken>()
            .HasIndex(r => r.Token).IsUnique();
        builder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // AdminUser → Business (optional FK)
        builder.Entity<AdminUser>()
            .HasOne(u => u.Business)
            .WithMany()
            .HasForeignKey(u => u.BusinessId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Apply any IEntityTypeConfiguration files in this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(PasukhiDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (entry.Entity.BusinessId == Guid.Empty)
                        entry.Entity.BusinessId = _tenantProvider.BusinessId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### `src/Pasukhi.Infrastructure/Tenant/HttpTenantProvider.cs`

```csharp
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Tenant;

public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid BusinessId =>
        Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirst("BusinessId")?.Value,
            out var id) ? id : Guid.Empty;
}
```

### `src/Pasukhi.Infrastructure/Messaging/QueueTenantProvider.cs`

```csharp
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Messaging;

/// <summary>
/// Used by MassTransit consumers. Set BusinessId from the queue message before any DB operations.
/// </summary>
public class QueueTenantProvider : ITenantProvider
{
    public Guid BusinessId { get; set; }
}
```

---

## Step 5 — API Program.cs

### `src/Pasukhi.API/Program.cs`

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pasukhi.Application.Interfaces;
using Pasukhi.Domain.Entities;
using Pasukhi.Infrastructure.Data;
using Pasukhi.Infrastructure.Tenant;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/pasukhi-.log", rollingInterval: RollingInterval.Day));

// Database
builder.Services.AddDbContext<PasukhiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
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

// JWT
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                           ?? new[] { "http://localhost:5173" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Tenant provider (scoped — one per request)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();

// Controllers + FluentValidation
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
```

---

## Step 6 — appsettings

### `src/Pasukhi.API/appsettings.json`

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "Secret": "",
    "Issuer": "https://api.pasukhi.ge",
    "Audience": "https://admin.pasukhi.ge",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  },
  "Meta": {
    "AppSecret": "",
    "GraphApiVersion": "v21.0"
  },
  "AI": {
    "Provider": "OpenAI",
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "MaxTokens": 500,
    "Temperature": 0.3
  },
  "AllowedHosts": "*"
}
```

### `src/Pasukhi.API/appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=pasukhi_dev;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "pasukhi-dev-secret-key-must-be-at-least-32-characters-long"
  },
  "Cors": {
    "Origins": ["http://localhost:5173"]
  }
}
```

---

## Step 7 — docker-compose.yml (repo root)

```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: pasukhi_dev
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

---

## Step 8 — First Migration

```bash
# From repo root
dotnet ef migrations add InitialCreate \
  --project src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj \
  --startup-project src/Pasukhi.API/Pasukhi.API.csproj \
  --output-dir Data/Migrations

dotnet ef database update \
  --project src/Pasukhi.Infrastructure/Pasukhi.Infrastructure.csproj \
  --startup-project src/Pasukhi.API/Pasukhi.API.csproj
```

If this fails because `PasukhiDbContext` requires `ITenantProvider` in its constructor and EF can't resolve it at design time, add a design-time factory:

```csharp
// src/Pasukhi.Infrastructure/Data/PasukhiDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pasukhi.Application.Interfaces;

namespace Pasukhi.Infrastructure.Data;

public class PasukhiDbContextFactory : IDesignTimeDbContextFactory<PasukhiDbContext>
{
    public PasukhiDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PasukhiDbContext>()
            .UseNpgsql("Host=localhost;Database=pasukhi_dev;Username=postgres;Password=postgres")
            .Options;

        // Use a stub tenant provider for migrations (BusinessId = Empty, filters inactive)
        return new PasukhiDbContext(options, new DesignTimeTenantProvider());
    }

    private class DesignTimeTenantProvider : ITenantProvider
    {
        public Guid BusinessId => Guid.Empty;
    }
}
```

---

## Step 9 — React Frontend

```bash
cd C:\Users\piros\OneDrive\Desktop\Pasukhi

npm create vite@latest pasukhi-admin -- --template react-ts
cd pasukhi-admin

npm install
npm install @tanstack/react-query @tanstack/react-query-devtools
npm install zustand
npm install react-hook-form @hookform/resolvers zod
npm install axios
npm install react-router-dom
npm install lucide-react
npm install sonner
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
npx shadcn@latest init
```

### `pasukhi-admin/src/main.tsx`

```tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { BrowserRouter } from 'react-router-dom'
import { Toaster } from 'sonner'
import App from './App.tsx'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
        <Toaster richColors position="top-right" />
      </BrowserRouter>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  </StrictMode>
)
```

### `pasukhi-admin/src/App.tsx`

```tsx
import { Routes, Route, Navigate } from 'react-router-dom'
import { LoginPage } from './features/auth/login-page'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  )
}

export default App
```

### `pasukhi-admin/src/api/client.ts`

```typescript
import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

export default api
```

### `pasukhi-admin/src/features/auth/login-page.tsx`

```tsx
export function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="text-center">
        <h1 className="text-2xl font-bold">Pasukhi Admin</h1>
        <p className="text-gray-500 mt-2">Login coming in Phase 1</p>
      </div>
    </div>
  )
}
```

### `pasukhi-admin/.env.development`

```
VITE_API_URL=http://localhost:5000
```

---

## Step 10 — Verify Everything Starts

```bash
# Terminal 1 — infrastructure
docker compose up -d

# Terminal 2 — backend
cd src/Pasukhi.API
dotnet run
# Expected: API starts on https://localhost:5001, http://localhost:5000
# Visit http://localhost:5000/api/health → { "status": "healthy" }
# Visit http://localhost:5000/swagger → Swagger UI

# Terminal 3 — frontend
cd pasukhi-admin
npm run dev
# Expected: Vite starts on http://localhost:5173
# Visit http://localhost:5173 → Login placeholder page

# Build checks
dotnet build   # 0 errors
cd pasukhi-admin && npx tsc --noEmit  # 0 errors
```

---

## Commit

```
feat(00-01): scaffold solution — all projects, Docker, migrations, React app
```

Stage everything:
```bash
git add src/ tests/ pasukhi-admin/ docker-compose.yml Pasukhi.sln
git commit -m "feat(00-01): scaffold solution — all projects, Docker, migrations, React app"
```

---

## What's Next

Phase 1 task file: `docs/codex/phase-1.md` (Auth + Business CRUD)

The five most critical files to get right (do not skip these):
1. `src/Pasukhi.Infrastructure/Data/PasukhiDbContext.cs` — global query filters are the foundation of tenant safety
2. `src/Pasukhi.Infrastructure/Data/PasukhiDbContextFactory.cs` — needed for migrations
3. `src/Pasukhi.Domain/Entities/TenantEntity.cs` — base class for all tenant-scoped entities
4. `src/Pasukhi.API/Program.cs` — wires everything together
5. `docker-compose.yml` — local infrastructure

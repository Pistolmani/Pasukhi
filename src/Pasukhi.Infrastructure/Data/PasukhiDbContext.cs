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

        builder.Entity<ChannelConnection>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Conversation>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Message>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<FaqItem>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<AutomationRule>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<Escalation>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<BusinessPrompt>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<BusinessSetting>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);
        builder.Entity<DailyMetric>().HasQueryFilter(e => e.BusinessId == _tenantProvider.BusinessId);

        builder.Entity<Business>().HasIndex(b => b.Slug).IsUnique();
        builder.Entity<ChannelConnection>()
            .HasIndex(c => new { c.ExternalAccountId, c.ChannelType })
            .IsUnique();
        builder.Entity<BusinessSetting>()
            .HasIndex(s => new { s.BusinessId, s.Key })
            .IsUnique();

        // Idempotency for inbound webhook replays — a given external message
        // from a platform can only be persisted once per business.
        builder.Entity<Message>()
            .HasIndex(m => new { m.BusinessId, m.ExternalMessageId })
            .IsUnique();

        builder.Entity<RefreshToken>().HasIndex(r => r.Token).IsUnique();
        builder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AdminUser>()
            .HasOne(u => u.Business)
            .WithMany()
            .HasForeignKey(u => u.BusinessId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

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
                    {
                        entry.Entity.BusinessId = _tenantProvider.BusinessId;
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

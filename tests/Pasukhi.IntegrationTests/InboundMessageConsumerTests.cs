using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Consumers;
using Pasukhi.Infrastructure.Data;
using Pasukhi.Infrastructure.Messaging;
using Pasukhi.Infrastructure.Tenant;

namespace Pasukhi.IntegrationTests;

public class InboundMessageConsumerTests
{
    private static async Task<(ITestHarness harness, ServiceProvider provider)> CreateHarnessAsync(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());

        services.AddDbContext<PasukhiDbContext>(o => o.UseInMemoryDatabase(dbName));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<InboundMessageConsumer>();
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ReceiveEndpoint("inbound-message-queue", e =>
                {
                    e.UseConsumeFilter(typeof(TenantContextFilter<>), context);
                    e.ConfigureConsumer<InboundMessageConsumer>(context);
                });
            });
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    private static async Task SeedChannelAsync(IServiceProvider provider, Guid businessId, Guid channelConnectionId)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
        var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
        db.Businesses.Add(new Business { Id = businessId, Name = "Test", Slug = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ChannelConnections.Add(new ChannelConnection
        {
            Id = channelConnectionId,
            BusinessId = businessId,
            ChannelType = ChannelType.Instagram,
            ExternalAccountId = "acct-1",
            VerifyToken = "tok",
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static InboundMessageEvent NewEvent(Guid businessId, Guid channelConnectionId, string externalMessageId, string sender = "sender-1") =>
        new()
        {
            BusinessId = businessId,
            ChannelConnectionId = channelConnectionId,
            ChannelType = "Instagram",
            ExternalSenderId = sender,
            ExternalMessageId = externalMessageId,
            TextContent = "hello",
            MessageType = "Text",
            ExternalTimestamp = "1700000000",
            RawPayloadJson = "{}"
        };

    [Fact]
    public async Task Single_event_persists_one_message_one_conversation_and_increments_metric()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Single_event_persists_one_message_one_conversation_and_increments_metric));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-1"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.Equal(1, await db.Conversations.CountAsync());
            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(ChannelType.Instagram, metric.ChannelType);
        }
    }

    [Fact]
    public async Task Duplicate_event_is_idempotent()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Duplicate_event_is_idempotent));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-dup"));
            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-dup"));
            Assert.Equal(2, await harness.Consumed.SelectAsync<InboundMessageEvent>().Count());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.Equal(1, await db.Conversations.CountAsync());
            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
        }
    }

    [Fact]
    public async Task Two_senders_same_channel_create_two_conversations()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Two_senders_same_channel_create_two_conversations));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-a", "sender-A"));
            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-b", "sender-B"));
            Assert.Equal(2, await harness.Consumed.SelectAsync<InboundMessageEvent>().Count());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(2, await db.Messages.CountAsync());
            Assert.Equal(2, await db.Conversations.CountAsync());
            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(2, metric.TotalInbound);
        }
    }

    [Fact]
    public async Task Empty_business_id_is_dropped()
    {
        var (harness, provider) = await CreateHarnessAsync(nameof(Empty_business_id_is_dropped));
        await using (provider)
        {
            await harness.Bus.Publish(NewEvent(Guid.Empty, Guid.NewGuid(), "ext-0"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
            // DbContext without tenant still sees empty set for tenant-filtered entities.
            Assert.Equal(0, await db.Messages.IgnoreQueryFilters().CountAsync());
            Assert.Equal(0, await db.Conversations.IgnoreQueryFilters().CountAsync());
        }
    }
}

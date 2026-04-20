using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pasukhi.Application.AI;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Consumers;
using Pasukhi.Infrastructure.Data;
using Pasukhi.Infrastructure.Messaging;
using Pasukhi.Infrastructure.Services;
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
        services.AddScoped<IFaqMatcher, FaqMatcher>();
        services.AddScoped<IRuleMatcher, RuleMatcher>();
        services.Configure<AiOptions>(options =>
        {
            options.Provider = "OpenAI";
            options.Model = "gpt-5-mini";
            options.MaxTokens = 500;
            options.Temperature = 0.3;
            options.RequestTimeoutSeconds = 30;
            options.ReasoningEffort = "low";
        });
        services.AddScoped<IAiPromptBuilder, AiPromptBuilder>();
        services.AddScoped<IAiSafetyChecker, AiSafetyChecker>();
        services.AddSingleton<FakeAiService>();
        services.AddScoped<IAiService>(sp => sp.GetRequiredService<FakeAiService>());

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

    private static async Task<FaqItem> SeedFaqAsync(
        IServiceProvider provider,
        Guid businessId,
        string question,
        string answer,
        string? keywords = null)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
        var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
        var faq = new FaqItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Question = question,
            Answer = answer,
            Keywords = keywords,
            IsActive = true
        };
        db.FaqItems.Add(faq);
        await db.SaveChangesAsync();
        return faq;
    }

    private static async Task<AutomationRule> SeedRuleAsync(
        IServiceProvider provider,
        Guid businessId,
        string name,
        int priority,
        TriggerType triggerType,
        string triggerValue,
        ActionType actionType = ActionType.SendReply,
        string actionValue = "Rule reply")
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
        var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = name,
            Priority = priority,
            TriggerType = triggerType,
            TriggerValue = triggerValue,
            ActionType = actionType,
            ActionValue = actionValue,
            IsActive = true
        };
        db.AutomationRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    private static async Task<BusinessPrompt> SeedBusinessPromptAsync(
        IServiceProvider provider,
        Guid businessId,
        bool isAiEnabled = true,
        int maxAiTokensPerDay = 50_000,
        double aiConfidenceThreshold = 0.7,
        string systemPrompt = "Answer using the business context only.",
        string toneDescription = "professional and friendly")
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
        var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
        var prompt = new BusinessPrompt
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            IsAiEnabled = isAiEnabled,
            SystemPrompt = systemPrompt,
            ToneDescription = toneDescription,
            EscalationMessage = "Let me connect you with our team.",
            MaxAiTokensPerDay = maxAiTokensPerDay,
            AiConfidenceThreshold = aiConfidenceThreshold,
            FaqConfidenceThreshold = 0.85
        };
        db.BusinessPrompts.Add(prompt);
        await db.SaveChangesAsync();
        return prompt;
    }

    private static InboundMessageEvent NewEvent(
        Guid businessId,
        Guid channelConnectionId,
        string externalMessageId,
        string sender = "sender-1",
        string? textContent = "hello",
        string messageType = "Text") =>
        new()
        {
            BusinessId = businessId,
            ChannelConnectionId = channelConnectionId,
            ChannelType = "Instagram",
            ExternalAccountId = "acct-1",
            ExternalSenderId = sender,
            ExternalMessageId = externalMessageId,
            TextContent = textContent,
            MessageType = messageType,
            ExternalTimestamp = "1700000000",
            RawPayloadJson = "{}"
        };

    private static async Task WaitForMessagesAsync(
        IServiceProvider provider,
        Guid businessId,
        int expectedCount,
        int attempts = 30)
    {
        for (var i = 0; i < attempts; i++)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
            if (await db.Messages.CountAsync() >= expectedCount)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private static async Task WaitForEscalationsAsync(
        IServiceProvider provider,
        Guid businessId,
        int expectedCount,
        int attempts = 30)
    {
        for (var i = 0; i < attempts; i++)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
            if (await db.Escalations.CountAsync() >= expectedCount)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private sealed class FakeAiService : IAiService
    {
        public int CallCount { get; private set; }

        public AiReplyResult Result { get; set; } = new(
            true,
            "AI reply",
            0.9,
            false,
            null,
            42,
            TimeSpan.FromMilliseconds(10));

        public Task<AiReplyResult> GenerateReplyAsync(AiContext context, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

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
    public async Task Faq_match_creates_pending_outbound_reply_and_publishes_event()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Faq_match_creates_pending_outbound_reply_and_publishes_event));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true);
            var faq = await SeedFaqAsync(provider, businessId, "hello", "Hi from FAQ");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-faq", textContent: "hello"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());
            Assert.True(await harness.Published.Any<OutboundMessageReadyEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var messages = await db.Messages.OrderBy(m => m.CreatedAt).ToListAsync();
            Assert.Equal(2, messages.Count);
            var inbound = Assert.Single(messages, m => m.Direction == MessageDirection.Inbound);
            var outbound = Assert.Single(messages, m => m.Direction == MessageDirection.Outbound);

            Assert.Equal(MessageSource.FaqAutoReply, outbound.Source);
            Assert.Equal(DeliveryStatus.Pending, outbound.DeliveryStatus);
            Assert.Equal("Hi from FAQ", outbound.TextContent);
            Assert.Equal(faq.Id, outbound.MatchedFaqItemId);
            Assert.Equal(inbound.Id, outbound.ReplyToMessageId);

            var savedFaq = await db.FaqItems.SingleAsync(f => f.Id == faq.Id);
            Assert.Equal(1, savedFaq.MatchCount);

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(1, metric.TotalOutbound);
            Assert.Equal(1, metric.FaqReplies);

            var published = await harness.Published.SelectAsync<OutboundMessageReadyEvent>().FirstOrDefault();
            Assert.NotNull(published);
            Assert.Equal(outbound.Id, published.Context.Message.MessageId);
            Assert.Equal("Hi from FAQ", published.Context.Message.TextContent);
            Assert.Equal(inbound.ConversationId, published.Context.Message.ConversationId);
        }
    }

    [Fact]
    public async Task Duplicate_event_does_not_create_duplicate_faq_reply()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Duplicate_event_does_not_create_duplicate_faq_reply));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedFaqAsync(provider, businessId, "hello", "Hi from FAQ");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-faq-dup", textContent: "hello"));
            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-faq-dup", textContent: "hello"));
            Assert.Equal(2, await harness.Consumed.SelectAsync<InboundMessageEvent>().Count());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync(m => m.Direction == MessageDirection.Inbound));
            Assert.Equal(1, await db.Messages.CountAsync(m => m.Source == MessageSource.FaqAutoReply));
            Assert.Equal(1, await harness.Published.SelectAsync<OutboundMessageReadyEvent>().Count());
        }
    }

    [Fact]
    public async Task Faq_match_preempts_ai_fallback()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Faq_match_preempts_ai_fallback));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true);
            await SeedFaqAsync(provider, businessId, "hello", "FAQ wins before AI");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-faq-before-ai", textContent: "hello"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var outbound = await db.Messages.SingleAsync(m => m.Direction == MessageDirection.Outbound);
            Assert.Equal(MessageSource.FaqAutoReply, outbound.Source);
            Assert.Equal("FAQ wins before AI", outbound.TextContent);
            Assert.Equal(0, provider.GetRequiredService<FakeAiService>().CallCount);
        }
    }

    [Fact]
    public async Task No_match_text_without_ai_prompt_escalates_and_publishes_no_outbound()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(No_match_text_without_ai_prompt_escalates_and_publishes_no_outbound));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedFaqAsync(provider, businessId, "shipping", "We ship tomorrow");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-no-match", textContent: "hello"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(0, metric.TotalOutbound);
            Assert.Equal(0, metric.FaqReplies);
            Assert.Equal(1, metric.Escalations);

            var escalation = await db.Escalations.SingleAsync();
            Assert.Equal(EscalationReason.NoMatch, escalation.Reason);
        }
    }

    [Fact]
    public async Task Rule_match_creates_pending_outbound_reply_and_publishes_event()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Rule_match_creates_pending_outbound_reply_and_publishes_event));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            var rule = await SeedRuleAsync(
                provider,
                businessId,
                "Help reply",
                priority: 0,
                TriggerType.Keyword,
                "help",
                actionValue: "A human-friendly rule reply");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-rule", textContent: "help me"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());
            Assert.True(await harness.Published.Any<OutboundMessageReadyEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var messages = await db.Messages.OrderBy(m => m.CreatedAt).ToListAsync();
            Assert.Equal(2, messages.Count);
            var inbound = Assert.Single(messages, m => m.Direction == MessageDirection.Inbound);
            var outbound = Assert.Single(messages, m => m.Direction == MessageDirection.Outbound);

            Assert.Equal(MessageSource.RuleAutoReply, outbound.Source);
            Assert.Equal(DeliveryStatus.Pending, outbound.DeliveryStatus);
            Assert.Equal("A human-friendly rule reply", outbound.TextContent);
            Assert.Equal(rule.Id, outbound.MatchedRuleId);
            Assert.Equal(inbound.Id, outbound.ReplyToMessageId);

            var savedRule = await db.AutomationRules.SingleAsync(r => r.Id == rule.Id);
            Assert.Equal(1, savedRule.MatchCount);

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(1, metric.TotalOutbound);
            Assert.Equal(0, metric.FaqReplies);
            Assert.Equal(1, metric.RuleReplies);

            var published = await harness.Published.SelectAsync<OutboundMessageReadyEvent>().FirstOrDefault();
            Assert.NotNull(published);
            Assert.Equal(outbound.Id, published.Context.Message.MessageId);
            Assert.Equal("A human-friendly rule reply", published.Context.Message.TextContent);
        }
    }

    [Fact]
    public async Task Rule_match_preempts_ai_fallback()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Rule_match_preempts_ai_fallback));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true);
            await SeedRuleAsync(
                provider,
                businessId,
                "Help reply",
                priority: 0,
                TriggerType.Keyword,
                "help",
                actionValue: "Rule wins before AI");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-rule-before-ai", textContent: "help me"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var outbound = await db.Messages.SingleAsync(m => m.Direction == MessageDirection.Outbound);
            Assert.Equal(MessageSource.RuleAutoReply, outbound.Source);
            Assert.Equal("Rule wins before AI", outbound.TextContent);
            Assert.Equal(0, provider.GetRequiredService<FakeAiService>().CallCount);
        }
    }

    [Fact]
    public async Task Faq_match_preempts_matching_rule()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Faq_match_preempts_matching_rule));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedFaqAsync(provider, businessId, "help", "FAQ wins");
            var rule = await SeedRuleAsync(
                provider,
                businessId,
                "Help rule",
                priority: 0,
                TriggerType.Keyword,
                "help",
                actionValue: "Rule should not win");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-faq-before-rule", textContent: "help"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var outbound = await db.Messages.SingleAsync(m => m.Direction == MessageDirection.Outbound);
            Assert.Equal(MessageSource.FaqAutoReply, outbound.Source);
            Assert.Equal("FAQ wins", outbound.TextContent);
            Assert.Null(outbound.MatchedRuleId);

            var savedRule = await db.AutomationRules.SingleAsync(r => r.Id == rule.Id);
            Assert.Equal(0, savedRule.MatchCount);
        }
    }

    [Fact]
    public async Task Higher_priority_rule_wins_when_multiple_rules_match()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Higher_priority_rule_wins_when_multiple_rules_match));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            var later = await SeedRuleAsync(
                provider,
                businessId,
                "Later",
                priority: 5,
                TriggerType.Keyword,
                "help",
                actionValue: "Later reply");
            var first = await SeedRuleAsync(
                provider,
                businessId,
                "First",
                priority: 1,
                TriggerType.Keyword,
                "help",
                actionValue: "First reply");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-rule-priority", textContent: "help"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var outbound = await db.Messages.SingleAsync(m => m.Direction == MessageDirection.Outbound);
            Assert.Equal("First reply", outbound.TextContent);
            Assert.Equal(first.Id, outbound.MatchedRuleId);

            var savedRules = await db.AutomationRules.ToDictionaryAsync(r => r.Id);
            Assert.Equal(1, savedRules[first.Id].MatchCount);
            Assert.Equal(1, savedRules[later.Id].MatchCount);
        }
    }

    [Fact]
    public async Task Duplicate_event_does_not_create_duplicate_rule_reply()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Duplicate_event_does_not_create_duplicate_rule_reply));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedRuleAsync(provider, businessId, "Help", 0, TriggerType.Keyword, "help");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-rule-dup", textContent: "help"));
            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-rule-dup", textContent: "help"));
            Assert.Equal(2, await harness.Consumed.SelectAsync<InboundMessageEvent>().Count());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync(m => m.Direction == MessageDirection.Inbound));
            Assert.Equal(1, await db.Messages.CountAsync(m => m.Source == MessageSource.RuleAutoReply));
            Assert.Equal(1, await harness.Published.SelectAsync<OutboundMessageReadyEvent>().Count());
        }
    }

    [Fact]
    public async Task Rule_escalate_action_creates_escalation_without_outbound_reply()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Rule_escalate_action_creates_escalation_without_outbound_reply));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            var rule = await SeedRuleAsync(
                provider,
                businessId,
                "Escalate agent",
                priority: 0,
                TriggerType.Keyword,
                "agent",
                ActionType.Escalate,
                "Customer asked for a person");

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-rule-escalate", textContent: "agent please"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());

            var escalation = await db.Escalations.SingleAsync();
            Assert.Equal("Customer asked for a person", escalation.Notes);
            Assert.Equal(EscalationReason.CustomerRequested, escalation.Reason);

            var conversation = await db.Conversations.SingleAsync();
            Assert.True(conversation.IsEscalated);
            Assert.Equal(ConversationStatus.Escalated, conversation.Status);

            var savedRule = await db.AutomationRules.SingleAsync(r => r.Id == rule.Id);
            Assert.Equal(1, savedRule.MatchCount);

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(0, metric.TotalOutbound);
            Assert.Equal(1, metric.Escalations);
        }
    }

    [Fact]
    public async Task Ai_disabled_creates_no_match_escalation_and_publishes_no_outbound()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Ai_disabled_creates_no_match_escalation_and_publishes_no_outbound));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: false);

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-disabled", textContent: "unknown question"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            var fakeAi = provider.GetRequiredService<FakeAiService>();
            Assert.Equal(0, fakeAi.CallCount);
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            var escalation = await db.Escalations.SingleAsync();
            Assert.Equal(EscalationReason.NoMatch, escalation.Reason);
            Assert.Equal("AI fallback is disabled for this business.", escalation.Notes);

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(0, metric.TotalOutbound);
            Assert.Equal(1, metric.Escalations);
        }
    }

    [Fact]
    public async Task Ai_token_budget_exceeded_escalates_without_calling_ai()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Ai_token_budget_exceeded_escalates_without_calling_ai));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true, maxAiTokensPerDay: 100);

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-budget", textContent: "unknown question"));
            await WaitForMessagesAsync(provider, businessId, expectedCount: 1);
            await WaitForEscalationsAsync(provider, businessId, expectedCount: 1);

            var fakeAi = provider.GetRequiredService<FakeAiService>();
            Assert.Equal(0, fakeAi.CallCount);
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            var escalation = await db.Escalations.SingleAsync();
            Assert.Equal(EscalationReason.NoMatch, escalation.Reason);
            Assert.Equal("AI daily token budget was exceeded.", escalation.Notes);

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(0, metric.TotalOutbound);
            Assert.Equal(0, metric.AiReplies);
            Assert.Equal(0, metric.AiTokensUsed);
            Assert.Equal(1, metric.Escalations);
        }
    }

    [Fact]
    public async Task Ai_success_creates_pending_outbound_reply_and_publishes_event()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Ai_success_creates_pending_outbound_reply_and_publishes_event));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true);

            var fakeAi = provider.GetRequiredService<FakeAiService>();
            fakeAi.Result = new AiReplyResult(
                true,
                "AI answer",
                0.91,
                false,
                null,
                42,
                TimeSpan.FromMilliseconds(12));

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-success", textContent: "unknown question"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());
            Assert.True(await harness.Published.Any<OutboundMessageReadyEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            var messages = await db.Messages.OrderBy(m => m.CreatedAt).ToListAsync();
            Assert.Equal(2, messages.Count);
            var inbound = Assert.Single(messages, m => m.Direction == MessageDirection.Inbound);
            var outbound = Assert.Single(messages, m => m.Direction == MessageDirection.Outbound);

            Assert.Equal(1, fakeAi.CallCount);
            Assert.Equal(MessageSource.AiAutoReply, outbound.Source);
            Assert.Equal(DeliveryStatus.Pending, outbound.DeliveryStatus);
            Assert.Equal("AI answer", outbound.TextContent);
            Assert.Equal(0.91, outbound.AiConfidenceScore);
            Assert.Equal(inbound.Id, outbound.ReplyToMessageId);
            Assert.Empty(await db.Escalations.ToListAsync());

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(1, metric.TotalInbound);
            Assert.Equal(1, metric.TotalOutbound);
            Assert.Equal(1, metric.AiReplies);
            Assert.Equal(42, metric.AiTokensUsed);

            var published = await harness.Published.SelectAsync<OutboundMessageReadyEvent>().FirstOrDefault();
            Assert.NotNull(published);
            Assert.Equal(outbound.Id, published.Context.Message.MessageId);
            Assert.Equal("AI answer", published.Context.Message.TextContent);
        }
    }

    [Fact]
    public async Task Low_confidence_ai_creates_escalation_with_rejected_text()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Low_confidence_ai_creates_escalation_with_rejected_text));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true, aiConfidenceThreshold: 0.8);
            provider.GetRequiredService<FakeAiService>().Result = new AiReplyResult(
                true,
                "Maybe this is the answer",
                0.5,
                false,
                null,
                30,
                TimeSpan.FromMilliseconds(10));

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-low-confidence", textContent: "unknown question"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
            var escalation = await db.Escalations.SingleAsync();
            Assert.Equal(EscalationReason.LowAiConfidence, escalation.Reason);
            Assert.Equal("Maybe this is the answer", escalation.AiRejectedResponse);

            var metric = await db.DailyMetrics.SingleAsync();
            Assert.Equal(0, metric.AiReplies);
            Assert.Equal(0, metric.TotalOutbound);
            Assert.Equal(1, metric.Escalations);
        }
    }

    [Fact]
    public async Task Unsafe_ai_reply_creates_safety_escalation_with_rejected_text()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Unsafe_ai_reply_creates_safety_escalation_with_rejected_text));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true);
            provider.GetRequiredService<FakeAiService>().Result = new AiReplyResult(
                true,
                "Please visit https://made-up.example for details.",
                0.95,
                false,
                null,
                31,
                TimeSpan.FromMilliseconds(10));

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-unsafe", textContent: "unknown question"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
            var escalation = await db.Escalations.SingleAsync();
            Assert.Equal(EscalationReason.SafetyCheckFailed, escalation.Reason);
            Assert.Equal("Please visit https://made-up.example for details.", escalation.AiRejectedResponse);
        }
    }

    [Fact]
    public async Task Duplicate_event_does_not_create_duplicate_ai_reply_or_escalation()
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Duplicate_event_does_not_create_duplicate_ai_reply_or_escalation));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            await SeedBusinessPromptAsync(provider, businessId, isAiEnabled: true);

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-dup", textContent: "unknown question"));
            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-ai-dup", textContent: "unknown question"));
            Assert.Equal(2, await harness.Consumed.SelectAsync<InboundMessageEvent>().Count());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync(m => m.Direction == MessageDirection.Inbound));
            Assert.Equal(1, await db.Messages.CountAsync(m => m.Source == MessageSource.AiAutoReply));
            Assert.Equal(0, await db.Escalations.CountAsync());
            Assert.Equal(1, provider.GetRequiredService<FakeAiService>().CallCount);
            Assert.Equal(1, await harness.Published.SelectAsync<OutboundMessageReadyEvent>().Count());
        }
    }

    [Fact]
    public async Task Cross_tenant_ai_prompt_is_not_used()
    {
        var businessId = Guid.NewGuid();
        var otherBusinessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Cross_tenant_ai_prompt_is_not_used));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            using (var scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
                var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
                db.Businesses.Add(new Business
                {
                    Id = otherBusinessId,
                    Name = "Other AI",
                    Slug = "other-ai",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                db.BusinessPrompts.Add(new BusinessPrompt
                {
                    Id = Guid.NewGuid(),
                    BusinessId = otherBusinessId,
                    IsAiEnabled = true,
                    SystemPrompt = "Wrong tenant prompt",
                    ToneDescription = "wrong",
                    EscalationMessage = "wrong",
                    MaxAiTokensPerDay = 50_000,
                    AiConfidenceThreshold = 0.7,
                    FaqConfidenceThreshold = 0.85
                });
                await db.SaveChangesAsync();
            }

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-cross-ai", textContent: "unknown question"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var verifyScope = provider.CreateScope();
            verifyScope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await verifyDb.Messages.CountAsync());
            Assert.Equal(1, await verifyDb.Escalations.CountAsync());
            Assert.Equal(0, provider.GetRequiredService<FakeAiService>().CallCount);
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
        }
    }

    [Theory]
    [InlineData("Image", "hello")]
    [InlineData("Text", "")]
    [InlineData("Text", null)]
    public async Task Non_text_or_empty_text_does_not_run_faq_matching(string messageType, string? textContent)
    {
        var businessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync($"{nameof(Non_text_or_empty_text_does_not_run_faq_matching)}_{messageType}_{textContent ?? "null"}");
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            var faq = await SeedFaqAsync(provider, businessId, "hello", "Hi from FAQ");

            await harness.Bus.Publish(NewEvent(
                businessId,
                channelId,
                $"ext-{messageType}-{textContent ?? "null"}",
                textContent: textContent,
                messageType: messageType));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await db.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
            var savedFaq = await db.FaqItems.SingleAsync(f => f.Id == faq.Id);
            Assert.Equal(0, savedFaq.MatchCount);
            Assert.Equal(0, provider.GetRequiredService<FakeAiService>().CallCount);
        }
    }

    [Fact]
    public async Task Cross_tenant_faq_is_not_matched()
    {
        var businessId = Guid.NewGuid();
        var otherBusinessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Cross_tenant_faq_is_not_matched));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            using (var scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
                var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
                db.Businesses.Add(new Business
                {
                    Id = otherBusinessId,
                    Name = "Other",
                    Slug = "other",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                db.FaqItems.Add(new FaqItem
                {
                    Id = Guid.NewGuid(),
                    BusinessId = otherBusinessId,
                    Question = "hello",
                    Answer = "Wrong tenant",
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-cross-tenant", textContent: "hello"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var verifyScope = provider.CreateScope();
            verifyScope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await verifyDb.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
        }
    }

    [Fact]
    public async Task Cross_tenant_rule_is_not_matched()
    {
        var businessId = Guid.NewGuid();
        var otherBusinessId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var (harness, provider) = await CreateHarnessAsync(nameof(Cross_tenant_rule_is_not_matched));
        await using (provider)
        {
            await SeedChannelAsync(provider, businessId, channelId);
            using (var scope = provider.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
                var db = scope.ServiceProvider.GetRequiredService<PasukhiDbContext>();
                db.Businesses.Add(new Business
                {
                    Id = otherBusinessId,
                    Name = "Other Rules",
                    Slug = "other-rules",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                db.AutomationRules.Add(new AutomationRule
                {
                    Id = Guid.NewGuid(),
                    BusinessId = otherBusinessId,
                    Name = "Wrong tenant rule",
                    Priority = 0,
                    TriggerType = TriggerType.Keyword,
                    TriggerValue = "help",
                    ActionType = ActionType.SendReply,
                    ActionValue = "Wrong tenant",
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            await harness.Bus.Publish(NewEvent(businessId, channelId, "ext-cross-rule", textContent: "help"));
            Assert.True(await harness.Consumed.Any<InboundMessageEvent>());

            using var verifyScope = provider.CreateScope();
            verifyScope.ServiceProvider.GetRequiredService<ITenantContext>().SetBusinessId(businessId);
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PasukhiDbContext>();

            Assert.Equal(1, await verifyDb.Messages.CountAsync());
            Assert.False(await harness.Published.Any<OutboundMessageReadyEvent>());
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

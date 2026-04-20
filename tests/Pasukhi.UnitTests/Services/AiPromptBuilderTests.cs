using Microsoft.EntityFrameworkCore;
using Pasukhi.Domain.Entities;
using Pasukhi.Domain.Enums;
using Pasukhi.Infrastructure.Services;

namespace Pasukhi.UnitTests.Services;

public class AiPromptBuilderTests
{
    [Fact]
    public async Task BuildAsync_includes_prompt_faq_context_history_and_current_message()
    {
        var businessId = Guid.NewGuid();
        await using var db = TestDb.Create(businessId);
        var conversationId = Guid.NewGuid();
        var inboundId = Guid.NewGuid();

        db.Businesses.Add(new Business
        {
            Id = businessId,
            Name = "Pasukhi Cafe",
            Slug = "pasukhi-cafe",
            Description = "A neighborhood cafe.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.BusinessPrompts.Add(new BusinessPrompt
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            IsAiEnabled = true,
            SystemPrompt = "Use cafe policies only.",
            ToneDescription = "warm and concise",
            EscalationMessage = "Let me connect you with the cafe team.",
            MaxAiTokensPerDay = 1234,
            AiConfidenceThreshold = 0.8,
            FaqConfidenceThreshold = 0.9
        });
        var conversation = new Conversation
        {
            Id = conversationId,
            BusinessId = businessId,
            ChannelConnectionId = Guid.NewGuid(),
            ChannelType = ChannelType.Instagram,
            ExternalCustomerId = "customer-1",
            CustomerDisplayName = "Nino"
        };
        db.Conversations.Add(conversation);

        for (var i = 0; i < 12; i++)
        {
            db.FaqItems.Add(new FaqItem
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Question = $"Shipping question {i}",
                Answer = $"Shipping answer {i}",
                Keywords = "shipping, delivery",
                IsActive = true,
                SortOrder = i
            });
        }
        db.FaqItems.Add(new FaqItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Question = "Inactive shipping question",
            Answer = "Inactive answer",
            Keywords = "shipping",
            IsActive = false
        });

        db.Messages.AddRange(
            NewMessage(businessId, conversationId, MessageDirection.Inbound, "Earlier customer question"),
            NewMessage(businessId, conversationId, MessageDirection.Outbound, "Earlier assistant answer"),
            NewMessage(businessId, conversationId, MessageDirection.Inbound, "No text media", text: null),
            NewMessage(businessId, conversationId, MessageDirection.Inbound, "How does shipping work?", inboundId));
        await db.SaveChangesAsync();

        var inbound = await db.Messages.SingleAsync(m => m.Id == inboundId);
        var builder = new AiPromptBuilder(db);

        var context = await builder.BuildAsync(conversation, inbound, ChannelType.Instagram);

        Assert.NotNull(context);
        Assert.Equal(businessId, context.BusinessId);
        Assert.Equal(conversationId, context.ConversationId);
        Assert.Equal(inboundId, context.InboundMessageId);
        Assert.Equal("Pasukhi Cafe", context.BusinessName);
        Assert.Equal("A neighborhood cafe.", context.BusinessDescription);
        Assert.Equal("Use cafe policies only.", context.SystemPrompt);
        Assert.Equal("warm and concise", context.ToneDescription);
        Assert.Equal("Let me connect you with the cafe team.", context.EscalationMessage);
        Assert.True(context.IsAiEnabled);
        Assert.Equal(1234, context.MaxAiTokensPerDay);
        Assert.Equal(0.8, context.AiConfidenceThreshold);
        Assert.Equal("Nino", context.CustomerDisplayName);
        Assert.Equal("How does shipping work?", context.InboundMessageText);
        Assert.Equal(10, context.RelevantFaqs.Count);
        Assert.All(context.RelevantFaqs, faq => Assert.Contains("Shipping", faq.Question));
        Assert.DoesNotContain(context.RelevantFaqs, faq => faq.Answer == "Inactive answer");
        Assert.Contains(context.ConversationHistory, m => m.Role == "customer" && m.Content == "Earlier customer question");
        Assert.Contains(context.ConversationHistory, m => m.Role == "assistant" && m.Content == "Earlier assistant answer");
        Assert.DoesNotContain(context.ConversationHistory, m => m.Content == "How does shipping work?");
    }

    private static Message NewMessage(
        Guid businessId,
        Guid conversationId,
        MessageDirection direction,
        string externalMessageId,
        Guid? id = null,
        string? text = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            BusinessId = businessId,
            ConversationId = conversationId,
            Direction = direction,
            Source = direction == MessageDirection.Inbound ? MessageSource.Customer : MessageSource.AiAutoReply,
            MessageType = MessageType.Text,
            TextContent = text ?? externalMessageId,
            ExternalSenderId = "sender",
            ExternalMessageId = externalMessageId,
            DeliveryStatus = direction == MessageDirection.Inbound ? DeliveryStatus.Delivered : DeliveryStatus.Sent
        };
}

using MassTransit;
using Microsoft.Extensions.Logging;
using Pasukhi.Application.Messaging;

namespace Pasukhi.Infrastructure.Consumers;

public class InboundMessageConsumer : IConsumer<InboundMessageEvent>
{
    private readonly ILogger<InboundMessageConsumer> _logger;

    public InboundMessageConsumer(ILogger<InboundMessageConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InboundMessageEvent> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "InboundMessage received | Business={BusinessId} | Channel={ChannelType} | Sender={SenderId} | MessageId={MessageId}",
            e.BusinessId, e.ChannelType, e.ExternalSenderId, e.ExternalMessageId);

        // Full processing wired in Phase 4
        return Task.CompletedTask;
    }
}

using Pasukhi.Application.Messaging;

namespace Pasukhi.Application.Interfaces;

public interface IInboundMessageEnqueuer
{
    bool TryEnqueue(InboundMessageEvent evt);
}

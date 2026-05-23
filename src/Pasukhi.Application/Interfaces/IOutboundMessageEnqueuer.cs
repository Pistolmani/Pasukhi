using Pasukhi.Application.Messaging;

namespace Pasukhi.Application.Interfaces;

public interface IOutboundMessageEnqueuer
{
    ValueTask EnqueueAsync(OutboundMessageReadyEvent evt, CancellationToken ct = default);
}

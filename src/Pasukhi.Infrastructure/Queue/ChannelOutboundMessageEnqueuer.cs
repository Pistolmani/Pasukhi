using System.Threading.Channels;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;

namespace Pasukhi.Infrastructure.Queue;

public class ChannelOutboundMessageEnqueuer : IOutboundMessageEnqueuer
{
    private readonly ChannelWriter<OutboundMessageReadyEvent> _writer;

    public ChannelOutboundMessageEnqueuer(ChannelWriter<OutboundMessageReadyEvent> writer)
    {
        _writer = writer;
    }

    public ValueTask EnqueueAsync(OutboundMessageReadyEvent evt, CancellationToken ct = default)
        => _writer.WriteAsync(evt, ct);
}

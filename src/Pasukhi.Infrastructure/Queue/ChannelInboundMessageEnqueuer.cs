using System.Threading.Channels;
using Pasukhi.Application.Interfaces;
using Pasukhi.Application.Messaging;

namespace Pasukhi.Infrastructure.Queue;

public class ChannelInboundMessageEnqueuer : IInboundMessageEnqueuer
{
    private readonly ChannelWriter<InboundMessageEvent> _writer;

    public ChannelInboundMessageEnqueuer(ChannelWriter<InboundMessageEvent> writer)
    {
        _writer = writer;
    }

    public bool TryEnqueue(InboundMessageEvent evt) => _writer.TryWrite(evt);
}

using System.Threading.Channels;

namespace Mortgage.Infrastructure.Queue;

// Trade-off: an in-memory queue is lost on stop (Pending rows are re-queued at startup) and not shared across instances; Service Bus replaces it.
public sealed class ChannelAssessmentQueue : IAssessmentQueue
{
    // Unbounded: an enqueue never waits. SingleReader because exactly one worker drains it.
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid assessmentId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(assessmentId, ct);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

using System.Threading.Channels;

namespace Mortgage.Infrastructure.Queue;

// Trade-off: the queue lives in this process's memory, so anything queued is lost if the process stops
// (the worker re-queues Pending rows at startup) and a second instance would not share it.
// A durable queue such as Service Bus replaces this class.
public sealed class ChannelAssessmentQueue : IAssessmentQueue
{
    // Unbounded: an enqueue never waits. SingleReader because exactly one worker drains it.
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(Guid assessmentId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(assessmentId, ct);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

namespace Mortgage.Infrastructure.Queue;

// The seam for background work. Today it is an in-memory channel (ChannelAssessmentQueue);
// at scale a Service Bus-backed implementation is one new class, and nothing that calls it changes.
public interface IAssessmentQueue
{
    ValueTask EnqueueAsync(Guid assessmentId, CancellationToken ct);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken ct);
}

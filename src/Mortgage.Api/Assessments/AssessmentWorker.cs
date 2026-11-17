using Microsoft.EntityFrameworkCore;
using Mortgage.Domain.Assessments;
using Mortgage.Infrastructure;
using Mortgage.Infrastructure.Queue;

namespace Mortgage.Api.Assessments;

// Runs for the life of the app, taking assessment ids off the queue one at a time.
public sealed class AssessmentWorker(
    IAssessmentQueue queue, IServiceScopeFactory scopeFactory, IConfiguration configuration,
    TimeProvider clock, ILogger<AssessmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            try
            {
                await RequeuePendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Still serve new requests; the old Pending rows wait for the next start.
                logger.LogError(ex, "Could not re-queue pending assessments at startup");
            }

            await foreach (var assessmentId in queue.DequeueAllAsync(stoppingToken))
                await ProcessAsync(assessmentId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown, not an error.
        }
    }

    // Restart recovery: the queue is in memory, so anything queued when the app stopped is gone from it
    // but still Pending in the database. Put those back.
    // Trade-off: a second instance would re-queue and double-process the same rows; a durable queue with delivery tracking fixes that.
    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageDbContext>();
        var ids = await db.Assessments
            .Where(a => a.Status == AssessmentStatus.Pending)
            .Select(a => a.Id)
            .ToListAsync(ct);

        foreach (var id in ids) await queue.EnqueueAsync(id, ct);
        if (ids.Count > 0) logger.LogInformation("Re-queued {Count} pending assessments at startup", ids.Count);
    }

    private async Task ProcessAsync(Guid assessmentId, CancellationToken ct)
    {
        try
        {
            // Read each time so a changed setting applies without a restart. 0 means no delay.
            var delaySeconds = configuration.GetValue<int>("Assessments:WorkerDelaySeconds");
            if (delaySeconds > 0) await Task.Delay(TimeSpan.FromSeconds(delaySeconds), clock, ct);

            // A scope per item: the worker is a singleton for the life of the app, but a DbContext is scoped
            // and must not be shared or kept. Each item gets its own, so no stale tracked state carries over.
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AssessmentService>();
            await service.CompleteAsync(assessmentId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // CompleteAsync handles a failed run itself; this catches what it lets out (a failed read or save).
            // Trade-off: no retry; the row stays Pending until the next start re-queues it. A durable queue would redeliver, then dead-letter.
            logger.LogError(ex, "Assessment {AssessmentId} could not be processed", assessmentId);
        }
    }
}

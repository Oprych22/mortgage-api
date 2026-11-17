using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mortgage.Domain.Applications;
using Mortgage.Domain.Assessments;
using Mortgage.Infrastructure;
using Mortgage.Infrastructure.Queue;

namespace Mortgage.Api.Assessments;

// The one place an assessment is run. The immediate endpoint calls AssessNowAsync; the queued endpoint
// calls QueueAsync, and the background worker later calls CompleteAsync. Both end up in Run, and the same calculator.
public sealed class AssessmentService(
    MortgageDbContext db, IOptions<AffordabilityOptions> options, TimeProvider clock,
    ILogger<AssessmentService> logger, IAssessmentQueue queue)
{
    public async Task<Assessment?> AssessNowAsync(Guid applicationId, CancellationToken ct)
    {
        var application = await db.Applications.FindAsync([applicationId], ct);
        if (application is null) return null;

        var assessment = Assessment.CreatePending(applicationId, clock.GetUtcNow());
        using var scope = BeginScope(applicationId, assessment.Id);
        logger.LogInformation("Assessment {AssessmentId} requested for application {ApplicationId}", assessment.Id, applicationId);

        // Finished in memory and saved once: this path never stores a Pending row, so a dropped request cannot
        // leave one behind with nothing queued to pick it up.
        Run(assessment, application);
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(ct);

        LogCompleted(assessment);
        return assessment;
    }

    public async Task<Assessment?> QueueAsync(Guid applicationId, CancellationToken ct)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == applicationId, ct)) return null;

        var assessment = Assessment.CreatePending(applicationId, clock.GetUtcNow());
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(ct);

        using (BeginScope(applicationId, assessment.Id))
            logger.LogInformation("Assessment {AssessmentId} queued for application {ApplicationId}", assessment.Id, applicationId);

        // Saved first, queued second: if the enqueue is lost, the row is still Pending and a restart re-queues it.
        // CancellationToken.None: the row is saved; the caller leaving must not lose the enqueue.
        await queue.EnqueueAsync(assessment.Id, CancellationToken.None);
        return assessment;
    }

    public async Task CompleteAsync(Guid assessmentId, CancellationToken ct)
    {
        var assessment = await db.Assessments.FindAsync([assessmentId], ct);
        if (assessment is null || assessment.Status != AssessmentStatus.Pending) return;

        // Every log line in this operation carries both ids, so one assessment can be followed through the logs.
        using var scope = BeginScope(assessment.ApplicationId, assessmentId);

        // A failed read or save is not caught: marking Failed needs the same database. The row stays Pending for a restart to re-queue.
        var application = await db.Applications.FindAsync([assessment.ApplicationId], ct);
        Run(assessment, application);
        await db.SaveChangesAsync(ct);

        LogCompleted(assessment);
    }

    // Completes or fails the assessment in memory; the caller saves it.
    private void Run(Assessment assessment, MortgageApplication? application)
    {
        try
        {
            if (application is null)
                throw new InvalidOperationException($"Application {assessment.ApplicationId} not found.");
            var policy = options.Value.ToPolicy();
            assessment.Complete(AffordabilityCalculator.Assess(application, policy), policy, clock.GetUtcNow());
        }
        catch (Exception ex)
        {
            // One bad assessment must not take the caller (or the worker) down: record it and carry on.
            logger.LogError(ex, "Assessment {AssessmentId} failed", assessment.Id);
            // The stored reason is returned to clients, so it is fixed text; the exception detail stays in the log.
            assessment.Fail("The assessment could not be completed.", clock.GetUtcNow());
        }
    }

    // Ids and decision only: never the applicant's figures.
    private void LogCompleted(Assessment assessment)
    {
        if (assessment.Status == AssessmentStatus.Completed)
            logger.LogInformation("Assessment {AssessmentId} completed: {Decision}", assessment.Id, assessment.Decision);
    }

    // A template scope, not a dictionary: the simple console prints it as text, and a structured sink
    // still gets ApplicationId and AssessmentId as separate properties.
    private IDisposable? BeginScope(Guid applicationId, Guid assessmentId) => logger.BeginScope(
        "ApplicationId:{ApplicationId} AssessmentId:{AssessmentId}", applicationId, assessmentId);
}

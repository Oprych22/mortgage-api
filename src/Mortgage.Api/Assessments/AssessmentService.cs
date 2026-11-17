using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mortgage.Domain.Assessments;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Assessments;

// The one place an assessment is run. The endpoint calls AssessNowAsync; the background
// worker calls CompleteAsync. Both end up in the same calculator.
public sealed class AssessmentService(
    MortgageDbContext db, IOptions<AffordabilityOptions> options, TimeProvider clock,
    ILogger<AssessmentService> logger)
{
    public async Task<Assessment?> AssessNowAsync(Guid applicationId, CancellationToken ct)
    {
        // Check first, so an unknown application leaves no orphan Pending row behind.
        if (!await db.Applications.AnyAsync(a => a.Id == applicationId, ct)) return null;

        var assessment = Assessment.CreatePending(applicationId, clock.GetUtcNow());
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(ct);

        await CompleteAsync(assessment.Id, ct);
        return assessment;
    }

    public async Task CompleteAsync(Guid assessmentId, CancellationToken ct)
    {
        var assessment = await db.Assessments.FindAsync([assessmentId], ct);
        if (assessment is null || assessment.Status != AssessmentStatus.Pending) return;

        try
        {
            var application = await db.Applications.FindAsync([assessment.ApplicationId], ct)
                ?? throw new InvalidOperationException($"Application {assessment.ApplicationId} not found.");
            var policy = options.Value.ToPolicy();
            assessment.Complete(AffordabilityCalculator.Assess(application, policy), policy, clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad assessment must not take the caller (or the worker) down: record it and carry on.
            logger.LogError(ex, "Assessment {AssessmentId} failed", assessmentId);
            assessment.Fail(ex.Message, clock.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
    }
}

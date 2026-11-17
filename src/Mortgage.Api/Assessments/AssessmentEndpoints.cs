using Microsoft.AspNetCore.Http.HttpResults;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Assessments;

public static class AssessmentEndpoints
{
    public static RouteGroupBuilder MapAssessmentEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/applications/{id:guid}/assessments", AssessNowAsync);
        api.MapGet("/assessments/{id:guid}", GetAsync);
        return api;
    }

    private static async Task<Results<Created<AssessmentResponse>, NotFound>> AssessNowAsync(
        Guid id, AssessmentService service, CancellationToken ct)
    {
        var assessment = await service.AssessNowAsync(id, ct);
        return assessment is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/assessments/{assessment.Id}", AssessmentResponse.From(assessment));
    }

    private static async Task<Results<Ok<AssessmentResponse>, NotFound>> GetAsync(
        Guid id, MortgageDbContext db, CancellationToken ct)
    {
        var assessment = await db.Assessments.FindAsync([id], ct);
        return assessment is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(AssessmentResponse.From(assessment));
    }
}

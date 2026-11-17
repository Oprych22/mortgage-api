using Microsoft.AspNetCore.Http.HttpResults;
using Mortgage.Domain;
using Mortgage.Domain.Applications;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Applications;

public static class ApplicationEndpoints
{
    public static RouteGroupBuilder MapApplicationEndpoints(this RouteGroupBuilder api)
    {
        var applications = api.MapGroup("/applications");

        applications.MapPost("/", CreateAsync);
        applications.MapGet("/{id:guid}", GetAsync);

        return api;
    }

    private static async Task<Results<Created<ApplicationResponse>, ValidationProblem>> CreateAsync(
        CreateApplicationRequest request, MortgageDbContext db, TimeProvider time, CancellationToken ct)
    {
        MortgageApplication application;
        try
        {
            // Shape was checked at the edge; the domain checks the business rules.
            // The .Value calls are safe: [Required] has already rejected nulls.
            application = MortgageApplication.Create(
                request.AnnualIncome!.Value, request.MonthlyExpenses!.Value, request.DepositAmount!.Value,
                request.PropertyValue!.Value, request.TermYears!.Value, time.GetUtcNow());
        }
        catch (DomainException ex)
        {
            // Same 400 shape as the edge validation, so clients handle one error format.
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [ex.Field] = [ex.Message] });
        }

        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/applications/{application.Id}", ApplicationResponse.From(application));
    }

    private static async Task<Results<Ok<ApplicationResponse>, NotFound>> GetAsync(
        Guid id, MortgageDbContext db, CancellationToken ct)
    {
        var application = await db.Applications.FindAsync([id], ct);
        return application is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ApplicationResponse.From(application));
    }
}

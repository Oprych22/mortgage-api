using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mortgage.Domain.Applications;
using Mortgage.Domain.Assessments;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Tests;

public class PersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Application_with_completed_assessment_round_trips()
    {
        // One open connection keeps the in-memory database alive and shared by both contexts.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MortgageDbContext>().UseSqlite(connection).Options;

        var application = MortgageApplication.Create(60_000m, 500m, 50_000m, 300_000m, 25, Now);
        var policy = new AffordabilityPolicy(0.90m, 0.50m, 4.5m, 0.07m);
        var result = AffordabilityCalculator.Assess(application, policy);
        var assessment = Assessment.CreatePending(application.Id, Now);
        assessment.Complete(result, policy, Now);

        await using (var writer = new MortgageDbContext(options))
        {
            await writer.Database.EnsureCreatedAsync();
            writer.Applications.Add(application);
            writer.Assessments.Add(assessment);
            await writer.SaveChangesAsync();
        }

        await using var reader = new MortgageDbContext(options);
        var loaded = await reader.Applications.Include(a => a.Assessments).SingleAsync(a => a.Id == application.Id);
        var loadedAssessment = Assert.Single(loaded.Assessments);

        Assert.Equal(250_000m, loaded.LoanAmount);
        // 250000/300000 has 28 decimal places, so a double/REAL column would fail this.
        Assert.Equal(result.Ltv, loadedAssessment.Ltv);
        Assert.Equal(result.Decision, loadedAssessment.Decision);
        Assert.Equal(result.Rules.Select(r => r.Message), loadedAssessment.Explanation);
        Assert.Equal(0.50m, loadedAssessment.PolicyMaxDti);
        Assert.Equal(AssessmentStatus.Completed, loadedAssessment.Status);
    }
}

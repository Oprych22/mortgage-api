using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mortgage.Api.Assessments;
using Mortgage.Domain.Applications;
using Mortgage.Domain.Assessments;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Tests;

public class QueuedAssessmentTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // Polls every 100 ms for up to 5 s, returning the first response whose status is not Pending.
    private static async Task<JsonElement> WaitForCompletion(HttpClient client, string url)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        JsonElement body;
        do
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            body = await ReadJson(response);
            if (body.GetProperty("status").GetString() != "Pending") return body;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);
        return body;
    }

    [Fact]
    public async Task Queued_assess_returns_202_pending_then_completes()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var created = await client.PostAsJsonAsync("/api/applications", new
        {
            annualIncome = 60_000m, monthlyExpenses = 500m, depositAmount = 50_000m, propertyValue = 300_000m, termYears = 25,
        });
        var applicationId = (await ReadJson(created)).GetProperty("id").GetGuid();

        var response = await client.PostAsync($"/api/applications/{applicationId}/assessments/queued", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        var id = body.GetProperty("id").GetGuid();
        Assert.EndsWith($"/api/assessments/{id}", response.Headers.Location!.ToString());

        var done = await WaitForCompletion(client, response.Headers.Location.ToString());

        Assert.Equal("Completed", done.GetProperty("status").GetString());
        var result = done.GetProperty("result");
        Assert.Equal("Approved", result.GetProperty("decision").GetString());
        Assert.Equal(83.3m, result.GetProperty("ltvPercent").GetDecimal());
        Assert.Equal(45.3m, result.GetProperty("dtiPercent").GetDecimal());
        Assert.Equal(270_000.00m, result.GetProperty("maxBorrowing").GetDecimal());
    }

    [Fact]
    public async Task Queued_assess_unknown_application_returns_404_and_creates_nothing()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsync($"/api/applications/{Guid.NewGuid()}/assessments/queued", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageDbContext>();
        Assert.Equal(0, await db.Assessments.CountAsync());
    }

    [Fact]
    public async Task Pending_assessment_left_from_before_start_is_completed()
    {
        var now = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        var application = MortgageApplication.Create(60_000m, 500m, 50_000m, 300_000m, 25, now);
        var pending = Assessment.CreatePending(application.Id, now);
        using var factory = new ApiFactory(db =>
        {
            db.Applications.Add(application);
            db.Assessments.Add(pending);
        });
        using var client = factory.CreateApiClient();

        var done = await WaitForCompletion(client, $"/api/assessments/{pending.Id}");

        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal("Approved", done.GetProperty("result").GetProperty("decision").GetString());
    }

    [Fact]
    public async Task Failing_assessment_is_marked_failed_and_the_worker_carries_on()
    {
        var now = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        // An assessment whose application does not exist: running it throws, so it must end up Failed.
        var orphan = Assessment.CreatePending(Guid.NewGuid(), now);
        using var factory = new ApiFactory(db =>
        {
            db.Database.OpenConnection();
            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
            db.Assessments.Add(orphan);
        });
        using var client = factory.CreateApiClient();

        var failed = await WaitForCompletion(client, $"/api/assessments/{orphan.Id}");

        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        // Client-safe text only: the exception message stays in the log.
        Assert.Equal("The assessment could not be completed.", failed.GetProperty("failureReason").GetString());

        // The same worker loop still serves new work.
        var created = await client.PostAsJsonAsync("/api/applications", new
        {
            annualIncome = 60_000m, monthlyExpenses = 500m, depositAmount = 50_000m, propertyValue = 300_000m, termYears = 25,
        });
        var applicationId = (await ReadJson(created)).GetProperty("id").GetGuid();
        var queued = await client.PostAsync($"/api/applications/{applicationId}/assessments/queued", null);
        var done = await WaitForCompletion(client, queued.Headers.Location!.ToString());
        Assert.Equal("Completed", done.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Completing_a_missing_assessment_does_not_throw()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AssessmentService>();

        await service.CompleteAsync(Guid.NewGuid(), CancellationToken.None);
    }
}

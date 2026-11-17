using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mortgage.Api.Assessments;
using Mortgage.Domain.Assessments;
using Mortgage.Infrastructure;

namespace Mortgage.Api.Tests;

public class AssessmentEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AssessmentEndpointTests() => _client = _factory.CreateApiClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<Guid> CreateApplication(
        decimal propertyValue, decimal depositAmount = 50_000m, decimal annualIncome = 60_000m)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new
        {
            annualIncome, monthlyExpenses = 500m, depositAmount, propertyValue, termYears = 25,
        });
        return (await ReadJson(response)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Assess_approve_example_returns_201_approved()
    {
        var applicationId = await CreateApplication(300_000m);

        var response = await _client.PostAsync($"/api/applications/{applicationId}/assessments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJson(response);
        Assert.EndsWith($"/api/assessments/{body.GetProperty("id").GetGuid()}", response.Headers.Location!.ToString());
        Assert.Equal("Completed", body.GetProperty("status").GetString());
        var result = body.GetProperty("result");
        Assert.Equal("Approved", result.GetProperty("decision").GetString());
        Assert.Equal(83.3m, result.GetProperty("ltvPercent").GetDecimal());
        Assert.Equal(45.3m, result.GetProperty("dtiPercent").GetDecimal());
        Assert.Equal(270_000.00m, result.GetProperty("maxBorrowing").GetDecimal());
        Assert.Equal(3, result.GetProperty("explanation").GetArrayLength());
        Assert.Equal(50m, result.GetProperty("policy").GetProperty("maxDtiPercent").GetDecimal());
    }

    [Fact]
    public async Task Assess_decline_example_returns_two_failed_reasons()
    {
        var applicationId = await CreateApplication(300_000m, depositAmount: 20_000m);

        var response = await _client.PostAsync($"/api/applications/{applicationId}/assessments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await ReadJson(response)).GetProperty("result");
        Assert.Equal("Declined", result.GetProperty("decision").GetString());
        Assert.Equal(93.3m, result.GetProperty("ltvPercent").GetDecimal());
        var lines = result.GetProperty("explanation").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains("exceeds", lines.Single(l => l.StartsWith("LTV")));
        Assert.Contains("exceeds", lines.Single(l => l.StartsWith("Loan of")));
        Assert.Contains("within", lines.Single(l => l.StartsWith("Debt-to-income")));
    }

    [Fact]
    public async Task Ltv_just_over_the_limit_reads_the_same_in_the_number_and_the_explanation()
    {
        // LTV is 90.00003%: declined, so neither the number nor the text may show it as 90.0 against a 90.0 limit.
        var applicationId = await CreateApplication(300_000m, depositAmount: 29_999.99m);

        var result = (await ReadJson(await _client.PostAsync($"/api/applications/{applicationId}/assessments", null)))
            .GetProperty("result");

        Assert.Equal("Declined", result.GetProperty("decision").GetString());
        Assert.Equal(90.1m, result.GetProperty("ltvPercent").GetDecimal());
        Assert.Contains("LTV 90.1% exceeds the 90% limit", result.GetProperty("explanation").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Max_borrowing_is_rounded_down_so_a_declined_loan_never_equals_it()
    {
        // 4.5 x 60,000.01 is 270,000.045 and the loan is 270,000.05: over the limit by half a penny.
        var applicationId = await CreateApplication(300_000.05m, depositAmount: 30_000m, annualIncome: 60_000.01m);

        var result = (await ReadJson(await _client.PostAsync($"/api/applications/{applicationId}/assessments", null)))
            .GetProperty("result");

        Assert.Equal("Declined", result.GetProperty("decision").GetString());
        Assert.Equal(270_000.05m, result.GetProperty("loanAmount").GetDecimal());
        Assert.Equal(270_000.04m, result.GetProperty("maxBorrowing").GetDecimal());
    }

    [Fact]
    public async Task Assess_now_saves_once_and_never_stores_a_pending_row()
    {
        // If the immediate path saved a Pending row first, a dropped request would leave it behind with nothing queued.
        var saves = new RecordAssessmentSaves();
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.ConfigureDbContext<MortgageDbContext>(options => options.AddInterceptors(saves))));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        var applicationId = await CreateApplication(300_000m);

        var response = await client.PostAsync($"/api/applications/{applicationId}/assessments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([AssessmentStatus.Completed], saves.Statuses);
    }

    private sealed class RecordAssessmentSaves : SaveChangesInterceptor
    {
        public List<AssessmentStatus> Statuses { get; } = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Statuses.AddRange(eventData.Context!.ChangeTracker.Entries<Assessment>().Select(e => e.Entity.Status));
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Assess_unknown_application_returns_404_and_creates_nothing()
    {
        var response = await _client.PostAsync($"/api/applications/{Guid.NewGuid()}/assessments", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MortgageDbContext>();
        Assert.Equal(0, await db.Assessments.CountAsync());
    }

    [Fact]
    public async Task Get_assessment_returns_same_result()
    {
        var applicationId = await CreateApplication(300_000m);
        var created = await ReadJson(await _client.PostAsync($"/api/applications/{applicationId}/assessments", null));
        var id = created.GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/assessments/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await ReadJson(response);
        var (before, after) = (created.GetProperty("result"), fetched.GetProperty("result"));
        // Compared by value: the JSON text can differ in trailing zeros (270000.00 vs 270000.0).
        foreach (var name in new[] { "loanAmount", "ltvPercent", "monthlyRepayment", "dtiPercent", "maxBorrowing" })
            Assert.Equal(before.GetProperty(name).GetDecimal(), after.GetProperty(name).GetDecimal());
        Assert.Equal(before.GetProperty("decision").GetString(), after.GetProperty("decision").GetString());
        Assert.Equal(before.GetProperty("explanation").GetRawText(), after.GetProperty("explanation").GetRawText());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/assessments/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Assessing_twice_creates_two_assessments()
    {
        var applicationId = await CreateApplication(300_000m);

        var first = await ReadJson(await _client.PostAsync($"/api/applications/{applicationId}/assessments", null));
        var second = await ReadJson(await _client.PostAsync($"/api/applications/{applicationId}/assessments", null));

        Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal("Completed", first.GetProperty("status").GetString());
        Assert.Equal("Completed", second.GetProperty("status").GetString());
    }

    [Fact]
    public void Zero_stress_rate_fails_startup_validation()
    {
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Affordability:StressRate"] = "0" })));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}

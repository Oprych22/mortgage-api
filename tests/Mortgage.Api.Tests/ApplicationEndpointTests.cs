using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Mortgage.Api.Tests;

public class ApplicationEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ApplicationEndpointTests() => _client = _factory.CreateApiClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object Approve(
        decimal? annualIncome = 60_000m, decimal? monthlyExpenses = 500m, decimal? depositAmount = 50_000m,
        decimal? propertyValue = 300_000m, int? termYears = 25) =>
        new { annualIncome, monthlyExpenses, depositAmount, propertyValue, termYears };

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Post_valid_application_returns_201_with_location_and_body()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", Approve());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJson(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.EndsWith($"/api/applications/{id}", response.Headers.Location!.ToString());
        Assert.Equal(250_000m, body.GetProperty("loanAmount").GetDecimal());
    }

    [Fact]
    public async Task Get_returns_created_application()
    {
        var created = await ReadJson(await _client.PostAsJsonAsync("/api/applications", Approve()));
        var id = created.GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/applications/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(60_000m, body.GetProperty("annualIncome").GetDecimal());
        Assert.Equal(25, body.GetProperty("termYears").GetInt32());
    }

    [Fact]
    public async Task Get_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/api/applications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_non_guid_id_returns_404()
    {
        var response = await _client.GetAsync("/api/applications/abc");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_missing_monthlyExpenses_returns_400_not_zero()
    {
        var json = """{"annualIncome":60000,"depositAmount":50000,"propertyValue":300000,"termYears":25}""";

        var response = await _client.PostAsync("/api/applications", new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertValidationError(response, "monthlyExpenses");
    }

    [Fact]
    public async Task Post_term_41_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", Approve(termYears: 41));
        await AssertValidationError(response, "termYears");
    }

    [Fact]
    public async Task Post_income_over_upper_bound_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", Approve(annualIncome: 10_000_001m));
        await AssertValidationError(response, "annualIncome");
    }

    [Fact]
    public async Task Post_deposit_equal_to_property_returns_400_with_depositAmount_error()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", Approve(depositAmount: 300_000m));
        await AssertValidationError(response, "depositAmount");
    }

    [Fact]
    public async Task Post_malformed_json_returns_400()
    {
        var response = await _client.PostAsync("/api/applications",
            new StringContent("""{"annualIncome":"lots"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task AssertValidationError(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await ReadJson(response)).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), e => string.Equals(e.Name, field, StringComparison.OrdinalIgnoreCase));
    }
}

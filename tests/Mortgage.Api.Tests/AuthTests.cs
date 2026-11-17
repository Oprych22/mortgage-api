using System.Net;
using System.Net.Http.Json;

namespace Mortgage.Api.Tests;

public class AuthTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [Fact]
    public async Task Api_without_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_with_tampered_token_returns_401()
    {
        using var client = _factory.CreateClient();
        var token = await GetTokenAsync(client);
        // Flip the last character of the signature so it no longer matches.
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        client.DefaultRequestHeaders.Authorization = new("Bearer", tampered);

        var response = await client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Dev_token_endpoint_returns_usable_token()
    {
        using var client = _factory.CreateClient();
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync("/dev/token", new { name = "demo" });
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.True(body.ExpiresAt > before.AddMinutes(59));

        client.DefaultRequestHeaders.Authorization = new("Bearer", body.AccessToken);
        var api = await client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        // 404, not 401: the token got past authentication and the route found no such application.
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
    }

    [Fact]
    public async Task Dev_token_endpoint_works_without_a_body()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/dev/token", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_stays_anonymous()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> GetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/dev/token", new { name = "demo" });
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }
}

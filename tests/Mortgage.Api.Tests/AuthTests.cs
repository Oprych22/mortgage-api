using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

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
        // Change a character in the middle of the signature so it no longer matches. The middle, not the end:
        // the last base64url character carries unused bits, so changing it may not change the decoded bytes.
        var i = token.Length - 10;
        var tampered = token[..i] + (token[i] == 'A' ? 'B' : 'A') + token[(i + 1)..];
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

    private const string ProductionKey = "a-production-key-of-at-least-32-bytes-long!";

    [Fact]
    public async Task Dev_token_endpoint_does_not_exist_in_production()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Auth:SigningKey", ProductionKey);
        });
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/dev/token", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Host_refuses_to_start_with_a_signing_key_under_32_bytes()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:SigningKey", "too-short-key"));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("at least 32 bytes", ex.Message);
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

using System.Net;

namespace Mortgage.Api.Tests;

public class HealthTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Health_returns_200_Healthy()
    {
        // Plain CreateClient, not CreateApiClient: health must stay reachable without a token.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

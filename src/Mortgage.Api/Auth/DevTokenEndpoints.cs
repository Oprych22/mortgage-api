using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mortgage.Api.Auth;

public static class DevTokenEndpoints
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public sealed record TokenRequest(string? Name);

    public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    // Mapped only in Development (see Program.cs): it hands out tokens to anyone who asks.
    public static void MapDevTokenEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost("/dev/token", (TokenRequest? request, AuthOptions auth, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            var expiresAt = now + Lifetime;
            var name = string.IsNullOrWhiteSpace(request?.Name) ? "demo" : request.Name;

            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = auth.Issuer,
                Audience = auth.Audience,
                Subject = new System.Security.Claims.ClaimsIdentity([new(JwtRegisteredClaimNames.Sub, name)]),
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)), SecurityAlgorithms.HmacSha256),
            });

            return Results.Ok(new TokenResponse(token, expiresAt));
        }).AllowAnonymous();
}

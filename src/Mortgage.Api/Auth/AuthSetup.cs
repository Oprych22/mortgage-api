using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Mortgage.Api.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Read here rather than through IOptions because the bearer setup needs the key straight away.
        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        if (string.IsNullOrWhiteSpace(auth.SigningKey))
            throw new InvalidOperationException("Auth:SigningKey is not configured");

        // HS256 needs a key of at least 256 bits (32 bytes); a shorter one would fail later, on the first request.
        if (Encoding.UTF8.GetByteCount(auth.SigningKey) < 32)
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes (256 bits) for HS256");

        services.AddSingleton(auth);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Trade-off: a symmetric key shared between the token endpoint and the validator, held in
                // development settings only. In production, tokens come from Entra ID and are validated
                // against its authority and signing keys (JWKS), so there is no shared secret to look after.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
                    // ClockSkew is left at its default of 5 minutes, which tolerates small clock
                    // differences between whoever issues a token and the API.
                };
            });
        services.AddAuthorization();
        return services;
    }
}

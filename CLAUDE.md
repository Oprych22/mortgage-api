# Mortgage API

ASP.NET Core 10 minimal API to create, retrieve and assess mortgage applications.

## Commands

- Build: `dotnet build` (warnings are errors)
- Test: `dotnet test`
- Run: `dotnet run --project src/Mortgage.Api` (http://localhost:8080)

## Structure and dependency rule

- `src/Mortgage.Domain`: entities, affordability rules and calculator. Plain C#, no PackageReference.
- `src/Mortgage.Infrastructure`: EF Core, SQLite, in-process queue. References Domain only.
- `src/Mortgage.Api`: minimal API endpoints, validation, services, background worker. References Domain and Infrastructure.
- `tests/Mortgage.Domain.Tests`, `tests/Mortgage.Api.Tests` (WebApplicationFactory).
- Dependencies point inward: Domain never references Infrastructure or Api.

## Conventions

- Money and ratios are `decimal`, never `double`. Ratios are fractions (0.9 = 90%).
- Rounding happens only at the edge (response mapping, explanation text).
- Minimal-API style: endpoint groups per feature, no controllers.
- Any logic change needs a test in the same commit.
- Mark each deliberate simplification with a one-line `// Trade-off:` comment (what is given up, what replaces it at scale).
- Add "why" comments only where a reviewer would ask; no comments that restate the code.
- Commits are imperative, one logical change each.

# Mortgage Application API

An ASP.NET Core 10 JSON API to create, retrieve and assess mortgage applications. An assessment works out
loan-to-value (LTV), debt-to-income (DTI), an approved or declined decision, a maximum borrowing estimate and a
one-line explanation per rule. It can run immediately or be queued for a background worker. The API uses JWT bearer
auth, structured logging and a health check, and comes with a Dockerfile and GitHub Actions CI.

```
src/Mortgage.Domain           entities, affordability rules and calculator (plain C#, no packages)
src/Mortgage.Infrastructure   EF Core on SQLite, in-process assessment queue
src/Mortgage.Api              minimal API endpoints, validation, AssessmentService, worker, auth, logging
tests/Mortgage.Domain.Tests   calculator, rules and domain validation
tests/Mortgage.Api.Tests      endpoints end to end (WebApplicationFactory)
```

## Prerequisites

- .NET 10 SDK (`global.json` pins 10.0.100 or a later feature band)
- Optional: Docker, to run in a container; `jq`, used by the curl examples below to pick out the token and ids

## Setup and run

```bash
git clone https://github.com/Oprych22/mortgage-api.git
cd mortgage-api
dotnet run --project src/Mortgage.Api
```

The API listens on http://localhost:8080 in the Development environment (from `launchSettings.json`). There is
nothing to set up: the SQLite file `mortgage.db` and its schema are created on first run, and Development has a
built-in signing key for tokens. To use another port, skip the launch profile:
`ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5000 dotnet run --project src/Mortgage.Api --no-launch-profile`.

With Docker instead (the compose file runs the Development environment so the token endpoint is available, and keeps
the database on a volume):

```bash
docker compose up --build            # http://localhost:8080; HOST_PORT=9000 docker compose up --build for another port
docker compose down -v               # stop and remove the volume
```

## Try it

Rider users can run `src/Mortgage.Api/Mortgage.Api.http`, which chains the token and ids between requests. With curl:

```bash
H=http://localhost:8080

# Health (anonymous)
curl -s $H/health                                                  # Healthy

# Get a token (Development only; the body is optional: {"name":"alice"})
TOKEN=$(curl -s -X POST $H/dev/token | jq -r .accessToken)
AUTH="Authorization: Bearer $TOKEN"

# Create an application -> 201 with Location and the application (loanAmount 250000)
APP=$(curl -s -X POST $H/api/applications -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"annualIncome":60000,"monthlyExpenses":500,"depositAmount":50000,"propertyValue":300000,"termYears":25}')
APP_ID=$(echo "$APP" | jq -r .id)

# Retrieve it -> 200
curl -s $H/api/applications/$APP_ID -H "$AUTH" | jq

# Assess it now -> 201, Completed: LTV 83.3%, DTI 45.3%, Approved, max borrowing 270000
curl -s -X POST $H/api/applications/$APP_ID/assessments -H "$AUTH" | jq

# A declined case: deposit 20000 -> LTV 93.3% and loan 280000 > 4.5x income, two reasons
DEC_ID=$(curl -s -X POST $H/api/applications -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"annualIncome":60000,"monthlyExpenses":500,"depositAmount":20000,"propertyValue":300000,"termYears":25}' | jq -r .id)
curl -s -X POST $H/api/applications/$DEC_ID/assessments -H "$AUTH" | jq .result.explanation

# Queue an assessment -> 202 Pending; the worker completes it (after 3 s in Development, to make Pending visible)
ASSESSMENT_ID=$(curl -s -X POST $H/api/applications/$APP_ID/assessments/queued -H "$AUTH" | jq -r .id)
curl -s $H/api/assessments/$ASSESSMENT_ID -H "$AUTH" | jq .status          # "Pending"
sleep 4; curl -s $H/api/assessments/$ASSESSMENT_ID -H "$AUTH" | jq .status # "Completed"

# Validation: a missing field is a 400, not a 0
curl -s -X POST $H/api/applications -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"annualIncome":60000,"depositAmount":50000,"propertyValue":300000,"termYears":25}' | jq .errors
# {"monthlyExpenses": ["The MonthlyExpenses field is required."]}

# No token -> 401
curl -s -o /dev/null -w '%{http_code}\n' $H/api/applications/$APP_ID
```

| Method and path | Result |
|---|---|
| `POST /dev/token` | 200 `{accessToken, expiresAt}`; Development only, anonymous |
| `POST /api/applications` | 201 + Location; 400 ValidationProblem on bad input |
| `GET /api/applications/{id}` | 200, or 404 (also for an id that is not a Guid) |
| `POST /api/applications/{id}/assessments` | 201 + Location, the completed assessment; 404 if no application |
| `POST /api/applications/{id}/assessments/queued` | 202 + Location, status Pending; 404 if no application |
| `GET /api/assessments/{id}` | 200 with status (Pending, Completed, Failed) and the result once completed, or 404 if unknown |
| `GET /health` | 200 Healthy when the database is reachable; anonymous |

## Run the tests

```bash
dotnet test
```

Domain tests check the calculator against hand-worked figures (including the two examples above), every decline
reason, and the exact boundaries (90% LTV, 50% DTI, 4.5x income). API tests run the real app in memory with a fresh
SQLite database per test: validation 400s, 404s, 401s, the immediate and queued paths, and restart recovery. CI runs
the same on every push.

## Affordability logic and assumptions

The figures are policy, held in the `Affordability` section of `appsettings.json` and copied onto each assessment,
so any decision can be explained later.

- Loan = property value - deposit. LTV = loan / property value.
- Monthly repayment uses the standard repayment-mortgage formula at a **7% stress rate** over the term: it asks "could
  they still pay if rates rose", not what today's rate would cost.
- **Monthly expenses means committed outgoings** (loans, cards, childcare), not day-to-day living costs.
- DTI = (monthly expenses + stressed repayment) / gross monthly income. The new mortgage payment is included because
  it is the commitment being assessed.
- Three rules, all must pass: **LTV <= 90%**, **DTI <= 50%**, **loan <= 4.5 x annual income**. Each rule adds one line
  to the explanation, such as "LTV 93.3% exceeds the 90% limit".
- Maximum borrowing is an estimate, not a rule: the lower of 4.5 x income and the largest loan whose stressed
  repayment keeps DTI at 50%, floored at zero. It ignores the LTV cap, which depends on the deposit, not the borrower.
- Money is `decimal` throughout. Rounding is half-up and happens only in responses and explanation text (2 dp for
  money, 1 dp for percentages); rules compare exact values. The one exception is a failing percentage that would
  round down to its limit: it is rounded up, so the text never says "90.0% exceeds the 90% limit".
- Input bounds: income > 0 and <= 10m; expenses 0 to 1m; deposit 0 to 100m; property value > 0 and <= 100m; term
  5 to 40 years; deposit must be less than the property value.
  Money values may have at most 2 decimal places (pounds and pence); more is a 400.
- A failed assessment returns a fixed, client-safe `failureReason`; the underlying exception is only in the log.

## Design & Reflection

### Key Design Decisions

1. **The affordability logic is plain C# with no web or database code.** `AffordabilityCalculator` and three named
   rules live in `Mortgage.Domain`, which has no package references, so the compiler keeps EF Core and ASP.NET out.
   The rules are testable on their own, readable, and can change without touching the API.
2. **An assessment is its own entity, linked to its application (one application, many assessments).** It records
   the figures it computed and the policy thresholds it used. Applications cannot be changed after creation, so the
   inputs can't move underneath an assessment, and any decision can be explained later. The same record carries a
   status (Pending, Completed, Failed), which is what lets the queued path reuse it: both paths go through
   `AssessmentService` and the same calculator.
3. **Validation in two layers.** .NET 10's built-in minimal API validation (data annotations) rejects bad input
   shapes: missing fields (nullable properties plus `[Required]`, so a missing number is a 400, not a 0), negatives,
   out-of-range terms. The domain rejects combinations that make no sense, such as a deposit at or above the property
   value. Both return the same 400 ProblemDetails shape with camelCase field names, so clients handle one format.

### Scaling Consideration

The first change is the database. A local SQLite file can't be shared, so a second API instance would have its own
data; nothing scales out until the database moves to a server (on Azure I'd use Azure SQL; PostgreSQL works too). That
is a connection string and the provider in `AddInfrastructure`, plus column precision for the decimals. After that the
endpoints are stateless; the in-process worker is not (each instance would re-queue Pending rows at startup), which is
why the queue is the second change. The queue is in memory, so it is lost on a crash and tied to one process. It sits
behind `IAssessmentQueue`, so a durable queue (Azure Service Bus, say) is one new class, and workers can then scale
separately from the API. It's the same pattern I've used with Kafka.

### Trade-offs

- SQLite and `EnsureCreated` instead of a database server and migrations: nothing to install, but one instance only.
- In-process queue: lost on a crash, mitigated by re-queueing Pending assessments at startup; single instance only.
- Development-only token endpoint and a signing key in `appsettings.Development.json`; the app refuses to start in
  Production without a configured key.
- Simplified underwriting: three rules, one stress rate, no credit data.
- Authentication only, no ownership: any valid token can read any application. The next step is storing the caller's
  `sub` as the owner and filtering on it, or role/scope policies for staff.
- No pagination, idempotency keys, or update/delete of applications.
- Where a simplification lives in the code, it is marked with a `// Trade-off:` comment saying what replaces it at scale.

### Next Steps

- A server database with EF migrations, and a durable queue.
- OpenTelemetry tracing and metrics on top of the existing structured logs.
- List an application's assessments (`GET /api/applications/{id}/assessments`); the relationship is stored but not
  yet exposed.
- Idempotency keys on the POST endpoints, so a retried request doesn't create a duplicate.
- Versioned policy history, so a policy change is recorded rather than only copied onto each assessment.
- A real identity provider (Entra ID) in place of the development token endpoint.
- A deployment pipeline: CI already builds and tests; the next step is building the image once and promoting it
  through environments.

## How AI was used

I built this with Claude Code. I set the design and the conventions (see `CLAUDE.md`), reviewed every change, and can
explain every line. Before any code was written, the design was reviewed by AI twice, each time with no shared
context; the findings changed it (the DTI cap, a duplicated rule, missing fields binding to 0, and the scaling answer).
A separate AI review of the finished code, with what was fixed, is in `REVIEW.md`.

**[FILL]**

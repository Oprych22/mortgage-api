# Independent AI code review

After the build, a separate AI pass reviewed the whole repository against the brief. It had no access to the design
notes or the decision log, so it read the code the way a new reviewer would. It also ran the app and sent about 30
requests, including bad bodies, overflow numbers and boundary figures. Result: no critical issues, one important
(the README was unfinished), eleven minor. Each finding was then fixed or deliberately left, below.

| # | Finding | Decision | Commit or reason |
|---|---|---|---|
| F1 | README unfinished: no REVIEW.md, placeholder text in the AI section | Fixed (this file); the AI section was finished and each claim in it confirmed by the author | this commit, then the next |
| F2 | Explanation printed "LTV 90.0% exceeds the 90% limit" just over a limit | Fixed: a failing percentage that would round down to its limit is rounded up | 5b68ec4 |
| F3 | Any valid token can read any application (no ownership) | Documented in Trade-offs; next step is owner by `sub` or role policies | 857c339 |
| F4 | Nothing tested that `/dev/token` is Development only, or the 32-byte key check | Fixed: two tests | f61bd69 |
| F5 | `failureReason` returned the raw exception message | Fixed: fixed client-safe text, exception stays in the log | e7145a6 |
| F6 | Money accepted unlimited decimal places | Fixed: at most 2 decimal places, 400 otherwise | 3ff5339 |
| F7 | Malformed JSON gave a bare 400 with no reason | Fixed: generic `detail` on such 400s | aa05367 |
| F8 | An application's assessments cannot be listed | Left: listed under Next Steps; the relationship is stored, not exposed | not needed for the brief |
| F9 | README said the API is stateless after the database move; the worker is not | Fixed in the README | 857c339 |
| F10 | EF Core SQLite version floated (`10.0.*`) | Fixed: pinned to 10.0.15 | c8ee0a5 |
| F11 | Inconsistent trailing zeros in JSON numbers (`250000.0`) | Left: cosmetic, same value to any JSON client, not worth a risky change | n/a |
| F12 | Test fixture used rule names and messages the code never produces | Fixed: real names and messages | 17cd934 |

Smaller items from earlier task reviews:

| Item | Decision | Commit or reason |
|---|---|---|
| `Assessment.Explanation` was a public mutable list | Fixed: `IReadOnlyList<string>`, private set; the persistence round trip still passes | 1c915c5 |
| EF Core SQL logging buried the demo log lines | Fixed: `Microsoft.EntityFrameworkCore` at Warning | 6b05384 |
| README endpoint table omitted the 404 on `GET /api/assessments/{id}` | Fixed | 857c339 |
| README scaling line missed column precision | Fixed | 857c339 |
| BOMs in two csproj files | Left: invisible, churn only | n/a |
| `CreatePending` accepts `Guid.Empty` | Left: only called after the application is confirmed to exist | n/a |
| Ids printed twice on the "requested" log line | Left: harmless | n/a |
| No length cap on the dev-token `name` | Left: Development only | n/a |
| Sync-over-async in the test factory's `CreateApiClient` | Left: test helper only | n/a |
| Line-ending churn in `appsettings.Development.json` | Left | n/a |

One note on F2: the review suggested always rounding failures up. That would change an existing correct figure
("LTV 93.3%" would become 93.4%), so the fix rounds up only when ordinary rounding would land on or below the limit.

## Second review

A second AI pass, again with no shared context, read all of `src` and `tests` after the fixes above. It ran nothing.
Nothing critical; ten findings.

| # | Finding | Decision | Commit or reason |
|---|---|---|---|
| S1 | The F2 fix covered percentages only: a loan half a penny over the income multiple printed as equal to its limit | Fixed: borrowing limits round down to the penny | 2714c67 |
| S2 | Response numbers had their own rounding, so `ltvPercent` could read 90.0 beside "LTV 90.1% exceeds" | Fixed: the response and the explanation share `DisplayRounding` | 2714c67 |
| S3 | The immediate endpoint saved a Pending row, then completed it in a second save; a dropped request left it orphaned | Fixed: completed in memory, saved once | db85dc7 |
| S4 | A failed database read while completing was recorded as a permanent Failed | Fixed with S3: only the calculation is caught; a failed read leaves the row Pending | db85dc7 |
| S5 | `Assessments:WorkerDelaySeconds` is not validated at startup | Left: a demo-only setting; a bound options class with `ValidateOnStart` is the fix | n/a |
| S6 | Maximum borrowing ignores the LTV cap | Left: intended and stated in the README (it depends on the deposit, not the borrower) | n/a |
| S7 | `Auth:Issuer` and `Auth:Audience` are not validated at startup | Left: development-only token setup, replaced by Entra ID in production | n/a |
| S8 | Field bounds are declared in both the request attributes and the domain | Left: the attributes report every bad field in one 400, the domain stops at the first and stays the authority | n/a |
| S9 | Several `// Trade-off:` comments ran past one line; the worker's no-retry had no marker | Fixed | 1cff535 |
| S10 | A seeded test factory cannot be combined with `WithWebHostBuilder` | Left: no test does this | n/a |

One part of S2 was left: policy percentages in the response are still rounded to 1 dp, so a policy value such as
87.25% would read 87.3 there and 87.25 in the text. The default policy has no such value.

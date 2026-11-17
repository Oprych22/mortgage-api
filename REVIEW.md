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

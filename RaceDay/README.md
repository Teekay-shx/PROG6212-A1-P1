# RaceDay API — Part 2: RESTful API Development

This is the ASP.NET Core Web API that implements the plan submitted in Part 1
(`/docs/raceday_erd.png`, `/docs/api-endpoint-plan.md`, `/docs/raceday_schema.sql`). It covers
authentication, user profiles, events, categories, enrolments, results, and payments, built
Code-First with Entity Framework Core against SQL Server.

## Project structure

```
RaceDay/
├── RaceDay.sln
├── src/
│   └── RaceDay.Api/
│       ├── Controllers/      7 controllers — Auth, Users, Events, Categories, Enrolments, Results, Payments
│       ├── Models/            EF Core entities + status "enum" classes (matches the Part 1 ERD)
│       ├── DTOs/               Request/response shapes, grouped by resource
│       ├── Data/                RaceDayDbContext (Fluent API constraints) + DbSeeder
│       ├── Services/            ICurrentUserService — reads the logged-in user off the session cookie
│       ├── Program.cs           DI, EF Core, cookie auth, Swagger, migration-on-startup
│       └── appsettings*.json
├── tests/
│   └── RaceDay.Api.Tests/      xUnit + WebApplicationFactory integration tests (EF Core InMemory)
└── .github/workflows/
    └── dotnet-ci.yml           Builds + runs all tests on every push/PR
```

## Before you run it: generate the EF Core migration

This solution was written in a sandboxed environment with no internet access to NuGet, so the
code below could be carefully written and reviewed line-by-line, but **could not actually be
compiled or run here** — `dotnet restore` fails outright (no route to `api.nuget.org`). You must
do the first build, the migration, and the test run yourself, in Visual Studio or the CLI, on a
machine with normal internet access. This also means one thing is deliberately **not** included:
the EF Core Migrations folder, since scaffolding a migration requires compiling the project first.

Once you can restore packages, generate it with:

```bash
cd RaceDay
dotnet tool install --global dotnet-ef   # if you don't already have it
dotnet ef migrations add InitialCreate --project src/RaceDay.Api --startup-project src/RaceDay.Api
```

This produces a `Migrations/` folder inside `src/RaceDay.Api`. Commit it — the schema it generates
must match `/docs/raceday_schema.sql` (it does, by design: every model, `[Column(TypeName=...)]`
attribute, and Fluent API check constraint in `Data/RaceDayDbContext.cs` was written to mirror that
script table-for-table).

## Running it

1. Point `appsettings.json` / `appsettings.Development.json` at your SQL Server instance (defaults
   to LocalDB in Development).
2. `dotnet ef migrations add InitialCreate ...` (above), if you haven't already.
3. `dotnet run --project src/RaceDay.Api`. On startup the app runs `Database.Migrate()` and then
   seeds the same sample data as the Part 1 SQL script (2 organisers, 2 participants, 3 events,
   5 categories, 4 enrolments, 1 result, 4 payments) — but only if `Users` is empty, so it's safe
   to restart the app without duplicating data.
4. Open `/swagger` (the launch profile does this automatically). Every endpoint is listed there
   with its purpose, request body, and response codes, pulled from the XML doc comments on each
   controller action.

Seeded login for trying things out in Swagger: `naledi.khumalo@raceday.co.za` (Organiser) or
`thabo.nkosi@example.com` (Participant), password `Password123!` for all seeded users.

## How authentication and roles work

The brief asks for "session management to maintain the user's authenticated state and role for
all subsequent requests" rather than a stateless token scheme, so this uses **cookie
authentication** (`Microsoft.AspNetCore.Authentication.Cookies`): `/api/auth/login` and
`/api/auth/register` issue an HttpOnly session cookie carrying the user's id and role as claims;
every subsequent request on the same client sends that cookie back automatically, and ASP.NET
Core's authentication middleware re-establishes `HttpContext.User` from it on every request — that
*is* server-recognised session state, just implemented through the cookie rather than
`HttpContext.Session`'s in-memory dictionary.

- `[Authorize]` on a controller/action requires any logged-in user; a missing/invalid cookie
  returns **401 Unauthorized** (configured explicitly in `Program.cs`, since this is an API with no
  login page to redirect to).
- `[Authorize(Roles = "Organiser")]` / `[Authorize(Roles = "Participant")]` / `[Authorize(Roles =
  "Admin")]` restrict an endpoint to one role; a logged-in user of the wrong role gets
  **403 Forbidden**.
- Beyond role, several endpoints also check **ownership** in the handler itself — e.g. an
  Organiser can only edit/delete their *own* events, categories and results, and a Participant can
  only cancel their *own* enrolment. These checks live in the controllers (`ICurrentUserService`
  exposes the caller's id/role) since role-based attributes alone can't express "owns this row."
- Passwords are hashed with ASP.NET Core Identity's `PasswordHasher<User>` (PBKDF2 under the hood)
  — the plaintext password is never stored or logged.

## How the implementation maps to the Part 1 plan

Every route in `/docs/api-endpoint-plan.md` is implemented as planned, with one explained addition:

**`Events.Distance` and `Events.Type`** — Part 2's functional requirements state "each event must
capture a name, description, date, location, distance, and event type (run, walk, or cycle)",
which the Part 1 ERD didn't include (distance only existed per-category there). Rather than let the
implementation silently drift from the submitted plan, Part 1's ERD, SQL script and endpoint plan
have been updated to add these two columns to `Events`, with this note added as the explanation the
brief asks for. No other table changed — Payments, Results, Enrolments, Categories and Users are
unchanged from Part 1.

**Payments** was already in the Part 1 plan as an addition beyond the required minimum (needed
because `Categories.EntryFee` implies something has to track whether that fee was paid), and is
implemented here the same way: `POST`/`GET /api/enrolments/{id}/payments`.

## Tests

`tests/RaceDay.Api.Tests` runs real HTTP requests against the full pipeline (controllers, cookie
auth, authorization, EF Core) via `WebApplicationFactory<Program>`, swapping SQL Server for the EF
Core InMemory provider so no database is needed to run them. Covers, with both success and failure
cases:

| File | Covers |
|---|---|
| `AuthTests.cs` | Registration (valid, duplicate email, disallowed self-registered role), login (correct/wrong password, unknown email), accessing a protected route while logged out |
| `RoleAccessTests.cs` | Every role boundary rejected: Participant creating an event, anonymous creating an event, Organiser enrolling in their own category, non-Admins listing all users, Participant recording a result |
| `EventManagementTests.cs` | Organiser create/update/delete on their own event, a different organiser blocked from editing it, public read access, invalid event type rejected |
| `EnrolmentsTests.cs` | Enrol → bib number assigned, shows up in "my enrolments", duplicate enrolment rejected, category-full rejected, non-existent category rejected, organiser roster view, cancel by owner vs. by a different participant |
| `UserProfileAndResultsTests.cs` | Profile update persists, result capture shows on the public leaderboard, duplicate result rejected, a participant's own results are scoped to them |

Run them with:

```bash
dotnet test RaceDay.sln
```

## CI/CD

`.github/workflows/dotnet-ci.yml` runs on every push and pull request to any branch: restores,
builds in Release, runs the full test suite, and uploads the `.trx` test results as a build
artifact. Once you've pushed this to GitHub, **add a screenshot of the green Actions run here**
(Actions tab → the workflow run → the green checkmark), since it couldn't be captured from this
sandboxed environment:

> _[Insert screenshot of a successful GitHub Actions run here once pushed]_

## Known gaps to close before submitting

- [ ] Run `dotnet ef migrations add InitialCreate` and commit the generated `Migrations/` folder
      (see above — not included for the reason explained there).
- [ ] Confirm `dotnet build` and `dotnet test` pass locally — they were reviewed file-by-file but
      never compiled, since this sandbox has no NuGet access.
- [ ] Push with a minimum of 20 meaningful commits on your own GitHub account, in a clearly named
      branch or subfolder for Part 2, per the brief's Section 6.
- [ ] Paste the green-build screenshot into this README once CI has run on GitHub.

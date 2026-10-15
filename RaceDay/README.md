 RaceDay

A REST API for running, walking and cycling events, built with ASP.NET Core, Entity Framework Core (Code-First) and SQL Server.

Organisers create events and categories. Participants enrol in categories. Organisers record results.

## Project parts

- **Part 1 (planning):** the `docs/` folder holds the ERD (`raceday_erd.png` and `.pdf`), the API endpoint plan (`api-endpoint-plan.md`) and the SQL script (`raceday_schema.sql`).
- **Part 2 (API):** the `src/RaceDay.Api` project, with tests in `tests/RaceDay.Api.Tests`.

## How to run

You need the .NET 8 SDK, SQL Server Express and Visual Studio 2022 (or a terminal).

1. **Connection string.** In `src/RaceDay.Api/appsettings.Development.json`:

   ```
   Server=LabVM2049939\\SQLEXPRESS;Database=RaceDayDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;
   ```

   On another machine, change `Server=` to your own server name.

2. **Create the migration** (once), then commit the `Migrations` folder.

   In Visual Studio's Package Manager Console, with `RaceDay.Api` as the default project:

   ```
   Add-Migration InitialCreate
   Update-Database
   ```

   Or in a terminal:

   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project src/RaceDay.Api --startup-project src/RaceDay.Api
   dotnet ef database update --project src/RaceDay.Api --startup-project src/RaceDay.Api
   ```

3. **Start the API:**

   ```
   dotnet run --project src/RaceDay.Api
   ```

4. **Open Swagger** at `/swagger` to view and test every endpoint in the browser.

If you already ran the Part 1 SQL script, drop `RaceDayDB` first (`DROP DATABASE RaceDayDB;`) or use a different database name, otherwise the migration fails with "table already exists".

## Demo logins

Sample data is added on first run. The password for all of them is `Password123!`.

| Role | Email |
|---|---|
| Organiser | naledi.khumalo@raceday.co.za |
| Organiser | johan.vdm@raceday.co.za |
| Participant | thabo.nkosi@example.com |
| Participant | lerato.dlamini@example.com |

## Authentication and roles

Users register as an Organiser or Participant (Admin accounts cannot self-register). Passwords are hashed. Logging in sets a session cookie that holds the user's id and role for every later request.

- Not logged in: `401 Unauthorized`
- Wrong role: `403 Forbidden`
- Right role but not your event or enrolment: `403 Forbidden`

| Role | Can do |
|---|---|
| Public | Browse events, categories and results |
| Participant | Edit own profile, enrol in categories, cancel own enrolment, pay, view own enrolments and results |
| Organiser | Edit own profile, manage own events and categories, view rosters, record results |
| Admin | List, view and delete users |

## Endpoints

The full list with request bodies and responses is in `docs/api-endpoint-plan.md` and in Swagger.

| Area | Routes |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout` |
| Profile | `GET` and `PUT /api/users/me` |
| Users (Admin) | `GET /api/users`, `GET` and `DELETE /api/users/{id}` |
| Events | `GET /api/events`, `GET /api/events/{id}`, `POST /api/events`, `PUT` and `DELETE /api/events/{id}` |
| Categories | `GET` and `POST /api/events/{eventId}/categories`, `PUT` and `DELETE /api/categories/{id}` |
| Enrolments | `POST` and `GET /api/categories/{categoryId}/enrolments`, `GET /api/users/me/enrolments`, `PUT` and `DELETE /api/enrolments/{id}` |
| Results | `GET /api/categories/{categoryId}/results`, `GET /api/users/me/results`, `POST /api/enrolments/{enrolmentId}/results`, `PUT /api/results/{id}` |
| Payments | `POST` and `GET /api/enrolments/{enrolmentId}/payments` |

## Database

Six tables, matching the Part 1 ERD:

- **Users**: every account, with a `Role` column
- **Events**: owned by an organiser (name, date, location, description, distance, type, status)
- **Categories**: belong to an event (distance or age group, fee, maximum participants)
- **Enrolments**: link a participant to a category (bib number, status); one enrolment per participant per category
- **Results**: one optional result per enrolment (finish time, position, status)
- **Payments**: one optional payment per enrolment (amount, method, status)

## Changes from the original Part 1 plan

Part 2 requires every event to have a distance and an event type (run, walk or cycle). The original ERD did not include these, so `DistanceKm` and `EventType` were added to the `Events` table in the ERD, SQL script and endpoint plan. Nothing else changed.

A `Payments` table was added beyond the six-table minimum, because categories have an entry fee that needs to be tracked.

## Testing

```
dotnet test RaceDay.sln
```

The xUnit tests send real requests through the full API (authentication, roles, database) using an in-memory database, so SQL Server is not needed. They cover success and failure cases for:

- Registration and login
- Event management by organisers
- Role rejection (wrong role, not logged in, not the owner)
- Enrolments being recorded correctly
- User profile and results

## CI/CD

The GitHub Actions workflow `.github/workflows/dotnet-ci.yml` builds the solution and runs all tests on every push and pull request.

Green build evidence: see the Actions tab of this repository (link the latest passing run here).

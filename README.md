# RaceDay System — Part 1: System Planning and Database

This README documents every step taken to complete Part 1 of the RaceDay project: designing the
data model, planning the API, and building the database schema — all **before** any application
code was written, as required by the brief.

## Repository structure

```
/docs
  ├── raceday_erd.png          Section A — Entity Relationship Diagram (image)
  ├── raceday_erd.pdf          Section A — Entity Relationship Diagram (PDF)
  ├── api-endpoint-plan.md     Section B — API Endpoint Plan
  └── raceday_schema.sql       Section C — SQL database script
README.md                      This file
```

---

## Step 1 — Understanding the domain

Before designing anything, the system's purpose was defined: RaceDay manages running events.
**Organisers** create **Events** (e.g. a marathon), each event offers one or more **Categories**
(e.g. "10km", "21km Half Marathon") that **Participants** can **enrol** in. Once an event has run,
organisers capture each participant's **Result**, and each enrolment carries an associated
**Payment** for the entry fee. This walkthrough of the domain is what shaped every entity in the
ERD below.

## Step 2 — Designing the Entity Relationship Diagram (Section A)

**File:** `docs/raceday_erd.png` / `docs/raceday_erd.pdf`

The ERD was built entity-by-entity, starting from the nouns in the domain description above, then
adding the attributes each one needs and the relationships between them:

| Entity | Purpose | Key relationships |
|---|---|---|
| **Users** | Single table for everyone who can log in — Admins, Organisers, and Participants — distinguished by a `Role` column. One table was used instead of three, since all three roles share the same authentication fields (email, password, name), and a role only adds a handful of extra behaviours rather than extra data. | 1 organiser → many Events; 1 participant → many Enrolments |
| **Events** | A race meeting created by an Organiser. | 1 event → many Categories |
| **Categories** | A specific distance/race within an event (e.g. "5km Fun Run"), each with its own fee and age limits. | 1 category → many Enrolments |
| **Enrolments** | The link between a Participant and the Category they've entered. This is the many-to-many resolver between Users and Categories, holding enrolment-specific data (bib number, status). | 1 enrolment → 0..1 Result; 1 enrolment → 0..1 Payment |
| **Results** | The finishing outcome for one enrolment (time, position, DNF/DSQ status). Kept separate from Enrolments because a result only exists after the race happens, and not every enrolment finishes. | belongs to 1 Enrolment |
| **Payments** | The entry-fee payment tied to one enrolment. Kept separate from Enrolments so payment status/history can change independently of the enrolment itself. | belongs to 1 Enrolment |

**Design decisions:**
- Every relationship is **one-to-many** at the database level; the only conceptual many-to-many
  (Participants ↔ Categories) is resolved through the **Enrolments** junction table, which is
  standard relational practice and lets us attach enrolment-specific fields (bib number,
  enrolment date, status).
- **Results** and **Payments** are modelled as **one-to-one (optional)** with Enrolments — an
  enrolment can exist without a result yet (race hasn't happened) or without payment yet
  (payment pending), so both foreign keys are `UNIQUE` but not the primary key itself.
- Primary keys (PK) and foreign keys (FK) are labelled directly on each entity box in the diagram,
  and cardinality (1, M, 0..1) is labelled on every relationship line.
- This gives **6 entities** (Users, Events, Categories, Enrolments, Results, Payments), meeting
  the assignment's minimum.

## Step 3 — Planning the API endpoints (Section B)

**File:** `docs/api-endpoint-plan.md`

Once the data model was settled, every piece of functionality the system needs to expose was
listed out **before writing any code**, grouped by resource:

1. **Authentication** — register and login, since every other endpoint depends on knowing who's
   calling.
2. **User Profile** — viewing/editing your own profile, plus admin-only user management.
3. **Events** — public browsing (GET) and organiser-only creation/editing (POST/PUT/DELETE),
   matching the rule that only the organiser who owns an event can change it.
4. **Categories** — nested under Events, since a category never exists without a parent event.
5. **Event Enrolments** — nested under Categories for signing up, plus a "my enrolments" route so
   participants can see their own race history without exposing other people's data.
6. **Results** — public leaderboards per category, plus organiser-only endpoints to record and
   correct results.
7. **Payments** *(added beyond the required minimum)* — needed because Categories have an
   `EntryFee`, so a way to record that an enrolment has been paid for is required for the data
   model to make sense.

For every endpoint the table records: **HTTP method**, **route**, **description**, **role
required**, **request body**, and **expected response** (including relevant error codes), so that
Part 2's implementation can be built directly from this table without redesigning anything on the
fly.

**Role rules applied consistently across the plan:**
- `Public` — no login needed (browsing events, categories, results).
- `Participant` — enrolling, paying, viewing your own data.
- `Organiser` — creating/editing their own events, categories, and results.
- `Admin` — managing user accounts.

## Step 4 — Writing the SQL script (Section C)

**File:** `docs/raceday_schema.sql`

The script was written to **match the ERD exactly**, table for table, column for column — there
are **no deliberate differences** between Section A and Section C in this submission.

The script is organised in this order so it runs cleanly on a blank SQL Server instance:

1. **Create database** — creates `RaceDayDB` if it doesn't already exist, then switches to it.
2. **Drop tables** (if they exist) in **foreign-key-safe order** — Payments and Results first
   (they depend on Enrolments), then Enrolments (depends on Users/Categories), then Categories
   (depends on Events), then Events (depends on Users), then Users last. This lets the script be
   re-run repeatedly without manual cleanup.
3. **Create tables** in **dependency order** (Users → Events → Categories → Enrolments → Results →
   Payments), each with:
   - An `IDENTITY(1,1)` primary key.
   - `NOT NULL` constraints on every field that the system cannot function without (e.g. `Email`,
     `EventDate`).
   - `UNIQUE` constraints where the business rule demands it (`Email` on Users; one enrolment per
     participant per category; one result and one payment per enrolment).
   - `CHECK` constraints to restrict status/role columns to a fixed set of valid values, instead
     of leaving them as free text.
   - `DEFAULT` values for timestamps (`GETDATE()`) and sensible starting statuses
     (`'Planned'`, `'Registered'`, `'Pending'`, etc.).
   - `FOREIGN KEY` constraints linking each table back to its parent, matching the ERD's
     relationship lines exactly.
4. **Seed data**, inserted in the same dependency order, providing:
   - 5 Users (2 Organisers, 2 Participants, 1 Admin for completeness).
   - 3 Events, owned by the two organisers.
   - 5 Categories spread across the 3 events.
   - 4 sample Enrolments linking participants to categories.
   - 1 sample Result for a completed race.
   - 4 sample Payments, in different states (`Completed`, `Pending`), to demonstrate the status
     workflow.

**How to run it:**
1. Open SQL Server Management Studio (SSMS) and connect to a SQL Server instance.
2. Open `docs/raceday_schema.sql`.
3. Execute the whole script (`F5`). It will create the `RaceDayDB` database, all six tables, and
   the seed rows in one pass.
4. Re-running the script is safe — the `DROP TABLE IF EXISTS` block at the top clears out any
   previous run first.

## Step 5 — Verifying the plan against the brief

Before submission, the three sections were cross-checked against each other:

- Every entity in the ERD has a matching `CREATE TABLE` in the SQL script, with the same columns,
  types, and keys.
- Every resource named in the brief's functional requirements (Authentication, User Profile,
  Events, Categories, Event Enrolments, Results) has a corresponding block in the endpoint plan.
- The seed data satisfies the brief's minimums: at least 2 Organisers, 2 Participants, 3 Events,
  categories for each event, and sample enrolments.

  Youtube link - https://youtu.be/8Ytx1Zy7Vt8


  # RaceDay Part 2

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



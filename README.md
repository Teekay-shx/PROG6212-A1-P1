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

## Assumptions made

- "RaceDay" was interpreted as a **running/road-race event management system** (registration,
  categories, results, payments), since the brief's Part 1 document doesn't itself state the
  domain in detail.
- A single `Users` table with a `Role` column was used rather than separate `Organisers` and
  `Participants` tables, since login and profile fields are identical across roles.
- A `Payments` entity was added beyond the six-entity minimum because `Categories.EntryFee`
  implies paid entries need to be tracked somewhere.

## Next step

Part 2 will implement the REST API exactly as specified in `docs/api-endpoint-plan.md`, backed by
the schema in `docs/raceday_schema.sql`.

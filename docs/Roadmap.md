# PursuitHQ — Roadmap

Phases are ordered by dependency: authentication comes first because nearly every entity has a `UserId` foreign key pointing at it. Each phase should end with working, tested, committed code.

## Phase 0 — Setup ✅

- [x] Install .NET 10, Node.js, PostgreSQL, pgAdmin, Visual Studio, VS Code
- [x] Create solution and `PursuitHQ.API` Web API project
- [x] Swagger UI working at `/swagger`
- [x] Git repository initialized, `.gitignore` in place, pushed to GitHub
- [x] Documentation set written (this `docs` folder)

## Phase 1 — Data foundation ✅

- [x] Install `Npgsql.EntityFrameworkCore.PostgreSQL` and EF Core tooling
- [x] Create `ApplicationDbContext` and connect to local PostgreSQL
- [x] Create every entity class from `DatabaseDesign.md`
- [x] First migration and `database update`
- [x] Verify tables in pgAdmin — 30 tables live

## Phase 2 — Authentication

- [x] Add ASP.NET Core Identity with `ApplicationUser`
- [x] JWT issuing and validation configured in `Program.cs`
- [x] `AuthController`: register, login, me, update profile, change password, delete account
- [x] `[Authorize]` on protected endpoints; Swagger configured to send the bearer token
- [x] Registration verified end to end — hashed password stored in PostgreSQL
- [x] Lockout policy wired (5 attempts / 15 minutes)
- [ ] Password reset flow — deferred until email sending exists in Phase 3b
- [ ] Delete account also removing uploaded files — deferred until file storage exists in Phase 4

**Note on Swashbuckle 10 + Microsoft.OpenApi 2.x:** the JWT Swagger config uses
`OpenApiSecuritySchemeReference` inside a document lambda, not the older
`OpenApiReference` pattern, and the namespace is `Microsoft.OpenApi` (not
`Microsoft.OpenApi.Models`). See `Program.cs` if this ever needs revisiting.

**Lockout has no escape hatch yet.** Five wrong passwords locks the account for
15 minutes, and with no reset flow the only ways back in are waiting or clearing
`LockoutEnd` and `AccessFailedCount` in pgAdmin by hand. This has already cost
real time once. A development-only reset endpoint would fix it in an afternoon;
the proper flow waits on email.

## Phase 3 — Academic planner ✅

- [x] `CoursesController` with full CRUD
- [x] `ClassSchedule` endpoints nested under courses
- [x] `AssignmentsController` with filters (course, status, due before/after)
- [x] `PATCH /api/assignments/{id}/status` — flips only the status, so a caller
      holding a partial record cannot overwrite other fields with stale values
- [x] Courses carry a first and last day of class, bounding how far weekly class
      times expand onto the calendar
- [x] Ownership checks verified on every endpoint
- [x] `ApiControllerBase` established: `[Authorize]` by default, `CurrentUserId`
      read from the JWT, 404 (not 403) for records the caller does not own

## Phase 3a — Calendar ✅

- [x] `CalendarEventsController` — CRUD for non-class activities
- [x] `CalendarController` — `GET /api/calendar?from=&to=` merging four sources
      into one sorted list: class meetings, assignment due dates, study sessions,
      and student-created events
- [x] Weekly recurrence (`FREQ=WEEKLY;BYDAY=...`), all-day events, multi-day spans
- [x] Per-source error isolation: one failing query names itself in the response
      instead of taking down the whole calendar
- [x] Month, week, and day views; the week and day views share one hour grid
- [x] Overlapping events laid out side by side; a live current-time line
- [x] Click any empty slot to open an event dialog pre-filled with that time
- [x] Assignments appear in the all-day strip with a working checkbox
- [x] Clicking a class opens that course's materials
- [x] Saved color palette per student (`/api/preferences/colors`), shared by the
      event dialog and the course form

**The decision that matters most in this phase: stored `DateTime` values are
wall-clock times, not instants.** Every `DateTime` column is PostgreSQL
`timestamp with time zone`, and Npgsql refuses to write a `DateTime` whose `Kind`
is `Unspecified` — which is exactly what a `datetime-local` input and
`DateOnly.ToDateTime()` both produce. That was the cause of the calendar
returning 500 on every request.

The fix is a single value converter in `ApplicationDbContext.ConfigureConventions`
that stamps `Kind` on the way in and strips it back to `Unspecified` on the way
out. Stripping it on the way out is the half that matters to the UI: a `Utc`
`DateTime` serializes with a trailing `Z`, the browser reads that as an instant
and shifts it into local time, and an assignment due at 11:59 PM lands on the
wrong day. Nothing converts a time in either direction — 9 AM is 9 AM.

**The one rough edge this leaves:** "overdue" compares a due date against
`DateTime.Now`, the server's clock. That is correct while the app runs on your
own laptop and wrong once it is deployed to a UTC server, at which point it needs
`ApplicationUser.TimeZone`. Noted in `Deployment.md`.

## Phase 3b — Email reminders

- [ ] `NotificationPreference` created with defaults at registration; settings page
- [ ] `IEmailService` with Resend; verified sending domain
- [ ] `NotificationService` with duplicate-safe sending via `Notification` records
- [ ] Secured `/api/jobs/run` endpoint + external cron every 15 minutes
- [ ] Assignment reminders, event reminders, daily and weekly digests
- [ ] Time-zone-correct delivery verified

This phase is mostly account setup and external infrastructure rather than code,
and parts of it cannot be tested locally.

## Phase 4 — Study materials ✅

- [x] `IFileStorageService` + `LocalFileStorageService`
- [x] Folder CRUD with nesting, plus loop prevention when moving a folder
- [x] Upload endpoint with size, type, and quota validation before anything
      touches disk
- [x] Authorized download streaming with the original file name
- [x] Notes CRUD
- [x] Folder delete removes descendants and their stored files, not just rows
- [x] Storage usage endpoint (`GET /api/storage/usage`)
- [x] Frontend page at `/courses/[id]/materials`: breadcrumb folder
      navigation, multi-file upload, download, notes editor, storage bar

**Security decisions in this phase:** files are stored with GUID names outside
`wwwroot`, so a client file name can never reach the filesystem and nothing is
served statically. Downloads go through an authorized endpoint with
`Content-Disposition: attachment`. Extension *and* MIME type must both be on the
allow-list.

**Frontend notes:** uploads bypass the shared API helper because `FormData`
requires the browser to set its own `Content-Type` boundary. Downloads fetch
with the bearer token, then trigger a save via a temporary blob URL, since a
plain link cannot send an Authorization header.

**Added beyond the original scope:**

- [x] Drag-and-drop upload, with a drop overlay
- [x] Drag a file onto a folder row to move it; a Move dropdown as the
      keyboard-friendly alternative
- [x] Inline preview: images and PDFs render from a blob URL, text files show
      their content
- [x] Course-wide search across files and notes. Searching deliberately ignores
      the current folder — the whole point is finding something whose folder you
      have forgotten. Debounced 300ms
- [x] `ITextExtractionService` (PDF via PdfPig, DOCX/PPTX/XLSX via the Open XML
      SDK) powering a text preview for formats no browser can render. PowerPoint
      extraction includes speaker notes

## Phase 5 — Internship & job tracker — **removed**

Built, then removed by decision in September 2026. The tracker worked, but the
search half never did: every Adzuna posting that looked promising turned out to
be closed by the time you clicked through, and the alternatives were worse.
Chasing a job board that stays accurate was pulling time away from the study
features, which are the reason the app exists.

**What was deleted:** `ApplicationsController`, `JobSearchController`,
`AdzunaJobSearchService`, `AtsJobSearchService`, `IJobSearchService`,
`JobSearchOptions`, `CompanyBoardOptions`, the `Applications` and `JobSearch`
DTO folders, and the `/jobs` and `/applications` pages.

**What was kept on purpose:** the `JobApplication` entity and its `DbSet`. It
costs nothing to leave the table in place, no migration was needed to remove the
feature, and none will be needed to bring it back.

**Worth remembering before rebuilding it:** the problem was never the tracker. If
this comes back, it should be a tracker you type into yourself, with search added
only if a source proves it keeps its listings current.

## Phase 6 — Frontend foundation ✅

Built ahead of Phases 4 and 5 so the app could be seen and clicked sooner.
Lives at `Frontend/pursuithq-web` (lowercase folder name required by npm).
Next.js 16 · React 19 · Tailwind v4 · App Router · JavaScript.

- [x] `create-next-app` scaffolded
- [x] Tailwind configured, root layout and top navigation
- [x] Login and register pages
- [x] `AuthProvider` holding session state, redirecting unauthenticated users
- [x] `lib/api.js` API client attaching the bearer token to every request
- [x] Dashboard with stat cards, upcoming assignments, and course list
- [x] Courses page: create, edit, delete, meeting times, term dates, colors
- [x] Assignments page: create, delete, filter, checkbox and status cycling
- [x] Study materials page — folders, uploads, downloads, notes
- [x] Calendar page — month, week, and day views
- [x] Shared `ColorPicker` and the saved-colors palette
- [ ] Settings page (profile, notification preferences)

**Known hardening item:** the JWT is kept in `localStorage`, which is readable
by any script on the page. Move to an httpOnly cookie before other students
use PursuitHQ. See `Security.md`.

**A bug worth remembering:** `body ? JSON.stringify(body) : undefined` in the API
client silently dropped a body of `0`, because `0` is falsy. Truthiness checks
and valid zero values do not mix — the check is `body !== undefined`.

**Running it locally needs two terminals:**
`dotnet run` in `PursuitHQ.API` (port 5051) and `npm run dev` in
`Frontend/pursuithq-web` (port 3000).

## Phase 7 — Dashboard

- [ ] Aggregated `/api/dashboard` endpoint, so the page makes one request
      rather than six
- [ ] Dashboard page with widgets and empty states: due this week, today's
      classes, per-course progress, what is scheduled

## Phase 8 — Career growth

- [ ] Goals, skills, and certifications endpoints and pages

## Phase 9 — AI features — **in progress**

The groundwork is done; the features on top of it are not.

- [x] Gemini API key from Google AI Studio; stored in user-secrets locally
- [x] `IAiService` + `GeminiAiService` with a typed HttpClient and a 90-second timeout
- [x] `ITextExtractionService` for PDF, DOCX, PPTX — **built early in Phase 4** for
      file previews, deliberately, so it would be ready to reuse here
- [ ] Structured JSON output with schema validation; reject malformed items
- [ ] 429 handling surfaced as a readable "service busy" message
- [ ] Per-user rate limiting on every AI endpoint
- [ ] `StudyToolAiService`: flashcards, quizzes, study guides
- [ ] Flashcard review mode and quiz-taking UI with scored attempts
- [ ] Chunking/truncation for large documents, with the user told what was used
- [ ] Graceful failure everywhere: no partial decks or quizzes
- [ ] `StudyPlannerAiService` and study session endpoints
- [ ] `ResumeAiService`: content suggestions + format check, and the resume editor

**Study tools are the next thing being built.** The entities already exist
(`FlashcardDeck`, `Flashcard`, `Quiz`, `QuizQuestion`, `QuizAttempt`,
`QuizAnswer`, `StudyGuide`), text extraction already works, and the Gemini client
is already registered — so this is prompt writing, JSON validation, and the
review UI rather than new infrastructure.

**A decision worth remembering:** Gemini with Google Search grounding was tried
during the job-search work and abandoned. Grounding is **not available on the
Gemini free tier at all** — it requires billing to be enabled, after which 5,000
search requests a month are free. Plain Gemini completions *are* free, and plain
completions are all the study tools need.

## Phase 9a — Internship & job search — **removed**

Removed alongside Phase 5. See that section for what was deleted and why.

## Phase 10 — Analytics & polish

- [ ] Analytics endpoints and charts
- [ ] Accessibility and mobile pass
- [ ] Study sessions get a UI — they already render on the calendar and are
      already a `DbSet`, but nothing in the app can create one yet

## Phase 11 — Deployment (final phase)

Deliberately last: build and test everything locally first, then ship it once.

- [ ] Neon PostgreSQL project created; production connection string set
- [ ] Cloud file storage configured (Cloudflare R2 or similar)
- [ ] API deployed to Render; all environment variables set
- [ ] Frontend deployed to Vercel; `NEXT_PUBLIC_API_URL` pointed at the live API
- [ ] CORS updated from `localhost:3000` to the real Vercel origin
- [ ] Migrations applied to the production database
- [ ] JWT stored in an httpOnly cookie rather than `localStorage`
- [ ] "Overdue" switched from `DateTime.Now` to the student's saved time zone
- [ ] Complete the security checklist in `Security.md`
- [ ] Privacy policy covering AI data handling, if AI features are live
- [ ] Backups enabled; a restore rehearsed once
- [ ] End-to-end smoke test against the live URL
- [ ] **Milestone: another student can register and use PursuitHQ**

Full instructions in `Deployment.md`. Expect environment problems rather than
code problems: connection strings, CORS, HTTPS, and environment variables are
where the time goes.

## Definition of Done (every phase)

1. Endpoints work and are documented in Swagger
2. Ownership enforced and verified
3. Validation errors return the standard error shape
4. Frontend handles loading, empty, and error states
5. Code committed to a feature branch and merged via pull request
6. `docs` updated if the design changed

## A habit worth keeping

When a change touches both C# and the database, run the three steps separately
and read the output of each before moving on:

```powershell
dotnet build                                  # does it compile?
dotnet ef migrations add <Name>               # generate the migration
dotnet ef database update                     # apply it
```

Stacking them into one paste hides which step failed. `dotnet ef` builds the
project first, so a compile error shows up as a confusing migration failure —
and a migration that was never created means the API silently keeps running
yesterday's code.

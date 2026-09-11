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
- [x] Password reset flow — request a link, set a new password, lockout cleared
      on success. Identity tokens, one hour lifespan. **The email itself is the
      only missing piece**: until Phase 3b the link is written to the API log,
      and returned in the response in Development only
- [ ] Delete account also removing uploaded files — deferred until file storage exists in Phase 4

**Note on Swashbuckle 10 + Microsoft.OpenApi 2.x:** the JWT Swagger config uses
`OpenApiSecuritySchemeReference` inside a document lambda, not the older
`OpenApiReference` pattern, and the namespace is `Microsoft.OpenApi` (not
`Microsoft.OpenApi.Models`). See `Program.cs` if this ever needs revisiting.

**Lockout now has an escape hatch.** A successful password reset clears
`LockoutEnd` and the failed-attempt count, because someone resetting their
password is very often someone who just locked themselves out guessing at it.
The pgAdmin unlock is no longer the only way back in.

**On telling callers whether an account exists:** `forgot-password` answers the
same way for a known and an unknown email in every environment except
Development, where it says plainly that there is no such account. A silent
success is indistinguishable from a broken endpoint while you are testing; in
production the same message would let anyone discover who has signed up, one
address at a time.

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
- [x] Study section: per-course tutor, sessions, guides, flashcards, tests
- [x] Settings page: appearance, profile, change password, delete account
- [x] Dark mode
- [ ] Notification preferences — waiting on Phase 3b

**Known hardening item:** the JWT is kept in `localStorage`, which is readable
by any script on the page. Move to an httpOnly cookie before other students
use PursuitHQ. See `Security.md`.

**A bug worth remembering:** `body ? JSON.stringify(body) : undefined` in the API
client silently dropped a body of `0`, because `0` is falsy. Truthiness checks
and valid zero values do not mix — the check is `body !== undefined`.

**How dark mode works, and its limit.** Every page was written with literal
colors — `bg-white`, `text-slate-900` — from one small palette, so `globals.css`
remaps that palette when `.dark` is on `<html>` rather than adding a `dark:`
variant to several hundred class names across fourteen files. This assumes
`bg-white` always means "a surface" and `text-slate-900` always means "primary
text", which is true today. **A page that ever needs something to stay white in
dark mode cannot use this mechanism** and needs its own explicit colors. The
trade-off is written into the top of `globals.css` so it is not rediscovered.

An inline script in `layout.js` applies the saved theme before the first paint.
React cannot: its first render happens after the browser has drawn, so a
dark-mode user would see a white flash on every page load.

**Running it locally needs two terminals:**
`dotnet run` in `PursuitHQ.API` (port 5051) and `npm run dev` in
`Frontend/pursuithq-web` (port 3000).

## Phase 7 — Dashboard — **the next obvious gap**

The home page is still the stat cards from the first week. It does not know the
calendar, the study tools, or the tests exist.

- [ ] Aggregated `/api/dashboard` endpoint, so the page makes one request
      rather than six
- [ ] Dashboard page with widgets and empty states: due this week, today's
      classes, per-course progress, recent decks and test scores

## Phase 8 — Career growth

- [ ] Goals, skills, and certifications endpoints and pages

## Phase 9 — AI features ✅ (study tools)

- [x] Gemini API key from Google AI Studio; stored in user-secrets locally
- [x] `IAiService` + `GeminiAiService` with a typed HttpClient and a 90-second timeout
- [x] `ITextExtractionService` for PDF, DOCX, PPTX — **built early in Phase 4** for
      file previews, deliberately, so it would be ready to reuse here
- [x] Structured output with validation; malformed items are dropped, not shown
- [x] Retry and rate-limit handling, surfaced as sentences a student can act on
- [x] Per-user daily cap (`IAiUsageLimiter`, `Ai:RequestsPerUserPerDay`)
- [x] `StudyToolAiService`: flashcards and practice tests
- [x] `StudyChatService`: a per-course tutor that answers from attached material
      and writes study guides on request
- [x] Flashcard review with self-grading and per-card counters
- [x] Practice tests taken in the app, with AI grading for written answers
- [x] Truncation for large documents (40k characters of source per request)
- [x] Graceful failure: no partial decks, no unsaved-but-shown guides
- [ ] `StudyPlannerAiService` and study session endpoints
- [ ] `ResumeAiService`: content suggestions + format check, and the resume editor

**How the three tools reach the student**

| Tool | Made from | Where |
|---|---|---|
| Flashcards | one file or note | `/courses/{id}/flashcards`, reviewed at `/decks/{id}` |
| Practice tests | one file or note, or asked for in chat | `/courses/{id}/tests`, taken at `/tests/{id}` |
| Study guides | asked for in chat | saved to `/courses/{id}/guides` |

**Decisions worth remembering**

*Artifacts are wrapped in text markers, not JSON.* A study guide is long markdown
full of quotes and newlines, and asking a model to escape all of that inside a
JSON string is exactly where these things break. Finding two markers in a text
stream cannot fail the same way. Practice tests use JSON inside those markers,
because questions are short and structure matters more there.

*Nothing is saved unless it is usable and wanted.* A deck with no valid cards is
not saved at all — a deck of three broken cards is worse than none, because it
looks finished. A study guide stays in the conversation until the student presses
Save, so the library only holds things that were deliberately kept.

*Grading never guesses in the student's favour.* An answer the model returns no
verdict for is marked wrong with an explanation. If grading fails outright, or the
daily allowance is gone, the test is still scored and returned with those answers
flagged — rather than throwing the submission away.

*Question types are sent as a sentence, not flags.* The UI toggles build a phrase
like "Use only these question types: multiple choice and free response", and a
free-text box sits beside it. A model reads "mostly multiple choice with two
written ones at the end" perfectly well; a dropdown never could.

*429 is retried, 500 is retried, and the difference matters.* The free tier limits
requests per **minute**, and Gemini reports how long to wait — so a 429 now costs
about 18 seconds instead of a failure. An earlier version excluded 429 on the
theory that a quota will not clear in seconds; that was wrong. Waits longer than
30 seconds still fail fast, because those are real daily quotas.

*Grounding was tried and abandoned.* Gemini with Google Search grounding is **not
available on the free tier at all** — it needs billing enabled. Plain completions
are what the study tools use.

**Billing is now enabled** with $10 of credit, which lifted the 20-requests-per-
minute ceiling. Roughly a penny per request; see `START-HERE.md` §5a.

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

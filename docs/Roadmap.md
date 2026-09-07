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
- [ ] Password reset flow — deferred until email sending exists in Phase 3a
- [ ] Delete account also removing uploaded files — deferred until file storage exists in Phase 4

**Note on Swashbuckle 10 + Microsoft.OpenApi 2.x:** the JWT Swagger config uses
`OpenApiSecuritySchemeReference` inside a document lambda, not the older
`OpenApiReference` pattern, and the namespace is `Microsoft.OpenApi` (not
`Microsoft.OpenApi.Models`). See `Program.cs` if this ever needs revisiting.

## Phase 3 — Academic planner ✅

- [x] `CoursesController` with full CRUD
- [x] `ClassSchedule` endpoints nested under courses
- [x] `AssignmentsController` with filters (course, status, due before/after)
- [x] Ownership checks verified on every endpoint
- [x] `ApiControllerBase` established: `[Authorize]` by default, `CurrentUserId`
      read from the JWT, 404 (not 403) for records the caller does not own

## Phase 3a — Calendar events & email reminders

- [ ] `CalendarEvent` CRUD for non-class activities
- [ ] `NotificationPreference` created with defaults at registration; settings page
- [ ] `IEmailService` with Resend; verified sending domain
- [ ] `NotificationService` with duplicate-safe sending via `Notification` records
- [ ] Secured `/api/jobs/run` endpoint + external cron every 15 minutes
- [ ] Assignment reminders, event reminders, daily and weekly digests
- [ ] Time-zone-correct delivery verified

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
      the current folder - the whole point is finding something whose folder you
      have forgotten. Debounced 300ms
- [x] `ITextExtractionService` (PDF via PdfPig, DOCX/PPTX/XLSX via the Open XML
      SDK) powering a text preview for formats no browser can render. PowerPoint
      extraction includes speaker notes

## Phase 5 — Internship & job tracker

- [ ] `ApplicationsController` with status pipeline
- [ ] Filters and search

## Phase 6 — Frontend foundation (partially done — taken early)

Built ahead of Phases 4 and 5 so the app could be seen and clicked sooner.
Lives at `Frontend/pursuithq-web` (lowercase folder name required by npm).
Next.js 16 · React 19 · Tailwind v4 · App Router · JavaScript.

- [x] `create-next-app` scaffolded
- [x] Tailwind configured, root layout and top navigation
- [x] Login and register pages
- [x] `AuthProvider` holding session state, redirecting unauthenticated users
- [x] `lib/api.js` API client attaching the bearer token to every request
- [x] Dashboard with stat cards, upcoming assignments, and course list
- [x] Courses page: create, edit, delete, plus add/remove meeting times
- [x] Assignments page: create, delete, filter, click-to-cycle status
- [x] Study materials page — folders, uploads, downloads, notes
- [ ] Job applications page — waiting on Phase 5
- [ ] Calendar view
- [ ] Settings page (profile, notification preferences)

**Known hardening item:** the JWT is kept in `localStorage`, which is readable
by any script on the page. Move to an httpOnly cookie before other students
use PursuitHQ. See `Security.md`.

**Running it locally needs two terminals:**
`dotnet run` in `PursuitHQ.API` (port 5051) and `npm run dev` in
`Frontend/pursuithq-web` (port 3000).

## Phase 7 — Dashboard

- [ ] Aggregated `/api/dashboard` endpoint
- [ ] Dashboard page with widgets and empty states

## Phase 8 — Career growth

- [ ] Goals, skills, and certifications endpoints and pages

## Phase 9 — AI features

- [ ] Gemini API key from Google AI Studio; stored in user-secrets locally
- [ ] `IAiService` + `GeminiAiService` with a typed HttpClient, timeout, and retry
- [ ] Structured JSON output with schema validation; reject malformed items
- [ ] 429 handling surfaced as a readable "service busy" message
- [ ] Per-user rate limiting on every AI endpoint
- [ ] `ResumeAiService`: content suggestions + ATS format check
- [ ] Resume editor UI with suggestion accept/dismiss
- [ ] `StudyPlannerAiService` and study session endpoints
- [ ] Study planner UI with generated-plan review
- [x] `ITextExtractionService` for PDF, DOCX, PPTX — **built early in Phase 4** for file previews; ready to reuse
- [ ] `StudyToolAiService`: flashcards, quizzes, study guides
- [ ] Flashcard review mode and quiz-taking UI with scored attempts
- [ ] Chunking/truncation for large documents, with the user told what was used
- [ ] Graceful failure everywhere: no partial decks, quizzes, or edits

## Phase 9a — Internship & job search

- [ ] Job-board API account and credentials
- [ ] `IJobSearchService` with short-lived response caching
- [ ] Search UI: keyword, location, type filters
- [ ] "Match my resume" keyword extraction and ranking
- [ ] Apply deep-links to the original posting
- [ ] Save-to-tracker creating a `JobApplication` with source URL

## Phase 10 — Analytics & polish

- [ ] Analytics endpoints and charts
- [ ] Deadline reminder emails
- [ ] Accessibility and mobile pass

## Phase 11 — Deployment (final phase)

Deliberately last: build and test everything locally first, then ship it once.

- [ ] Neon PostgreSQL project created; production connection string set
- [ ] Cloud file storage configured (Cloudflare R2 or similar)
- [ ] API deployed to Render; all environment variables set
- [ ] Frontend deployed to Vercel; `NEXT_PUBLIC_API_URL` pointed at the live API
- [ ] CORS updated from `localhost:3000` to the real Vercel origin
- [ ] Migrations applied to the production database
- [ ] JWT stored in an httpOnly cookie rather than `localStorage`
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

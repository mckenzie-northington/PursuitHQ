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

## Phase 4 — Study materials

- [ ] `IFileStorageService` + `LocalFileStorageService`
- [ ] Folder CRUD with nesting
- [ ] Upload endpoint with size, type, and quota validation
- [ ] Authorized download streaming with the original file name
- [ ] Notes CRUD
- [ ] Cascade delete removing files from storage, not just rows

## Phase 5 — Internship & job tracker

- [ ] `ApplicationsController` with status pipeline
- [ ] Filters and search

## Phase 6 — Frontend foundation

- [ ] `npx create-next-app@latest` in `Frontend`
- [ ] Tailwind configured, base layout and navigation
- [ ] Auth pages, token handling, protected routes
- [ ] API client wrapper attaching the bearer token
- [ ] Courses, assignments, materials, and applications pages
- [ ] Calendar view

## Phase 7 — Dashboard

- [ ] Aggregated `/api/dashboard` endpoint
- [ ] Dashboard page with widgets and empty states

## Phase 8 — MVP release

- [ ] Deploy API, database, and frontend (see `Deployment.md`)
- [ ] Complete the security checklist in `Security.md`
- [ ] End-to-end smoke test in production
- [ ] **Milestone: another student can register and use the app**

## Phase 9 — Career growth

- [ ] Goals, skills, and certifications endpoints and pages

## Phase 10 — AI features

- [ ] Gemini API key from Google AI Studio; stored in user-secrets locally
- [ ] `IAiService` + `GeminiAiService` with a typed HttpClient, timeout, and retry
- [ ] Structured JSON output with schema validation; reject malformed items
- [ ] 429 handling surfaced as a readable "service busy" message
- [ ] Per-user rate limiting on every AI endpoint
- [ ] `ResumeAiService`: content suggestions + ATS format check
- [ ] Resume editor UI with suggestion accept/dismiss
- [ ] `StudyPlannerAiService` and study session endpoints
- [ ] Study planner UI with generated-plan review
- [ ] `ITextExtractionService` for PDF, DOCX, PPTX
- [ ] `StudyToolAiService`: flashcards, quizzes, study guides
- [ ] Flashcard review mode and quiz-taking UI with scored attempts
- [ ] Chunking/truncation for large documents, with the user told what was used
- [ ] Graceful failure everywhere: no partial decks, quizzes, or edits

## Phase 10a — Internship & job search

- [ ] Job-board API account and credentials
- [ ] `IJobSearchService` with short-lived response caching
- [ ] Search UI: keyword, location, type filters
- [ ] "Match my resume" keyword extraction and ranking
- [ ] Apply deep-links to the original posting
- [ ] Save-to-tracker creating a `JobApplication` with source URL

## Phase 11 — Analytics & polish

- [ ] Analytics endpoints and charts
- [ ] Deadline reminder emails
- [ ] Accessibility and mobile pass

## Definition of Done (every phase)

1. Endpoints work and are documented in Swagger
2. Ownership enforced and verified
3. Validation errors return the standard error shape
4. Frontend handles loading, empty, and error states
5. Code committed to a feature branch and merged via pull request
6. `docs` updated if the design changed

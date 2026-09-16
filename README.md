# PursuitHQ

An all-in-one platform that empowers college students to manage coursework, study
materials, career documents, and the classmates they work with.

## What it does

PursuitHQ helps students:

- Track courses, class meeting times, and assignments
- See everything on one calendar — classes, due dates, events, reminders
- Keep study materials per course in nested folders, with typed notes and search
- Turn those materials into flashcards, practice tests, and study guides with AI,
  and ask a per-course tutor questions about them
- Build or import a resume, get AI review on it, and score it against a job posting
- Find classmates, connect with them, and message them one to one or in groups
- Get email reminders for deadlines, plus daily and weekly digests
- (Planned) Track career goals, skills, and certifications, and see analytics

The internship tracker and job search were built and then removed in September
2026 — see `docs/Roadmap.md` §5. The resume-to-posting matcher and saved jobs
survived and still work.

## Tech stack

**Backend:** C#, ASP.NET Core Web API (.NET 10 LTS), Entity Framework Core, ASP.NET Core Identity + JWT, PostgreSQL

**Frontend:** Next.js 16 (App Router), React 19, JavaScript, Tailwind CSS 4

**Database:** PostgreSQL, pgAdmin

**Email:** Resend, with a console fallback so the whole flow runs locally

**AI:** Google Gemini, behind an `IAiService` interface

## Project structure

```
PursuitHQ
├── Backend/PursuitHQ/PursuitHQ.API   # ASP.NET Core Web API (PursuitHQ.slnx alongside it)
├── Frontend/pursuithq-web            # Next.js app
├── docs                              # Requirements, features, database, API, architecture,
│                                     #   security, deployment, roadmap
├── start-dev.ps1                     # Starts the API and the website together (Windows)
└── README.md
```

## Getting started (backend)

The API reads its secrets from .NET user-secrets, so nothing sensitive is in the
repo. From `Backend/PursuitHQ/PursuitHQ.API`:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=pursuithq;Username=postgres;Password=<yours>"
dotnet user-secrets set "Jwt:Key" "<32+ character random string>"
dotnet restore
dotnet ef database update
dotnet run
```

`Jwt:Key` is required — the API refuses to start without it. `Ai:ApiKey` and
`Email:ApiKey` are optional: without them the AI features report themselves as
unconfigured and emails print to the terminal instead of sending.

`dotnet run` listens on `http://localhost:5051`. The API docs are at
`http://localhost:5051/swagger` (Development only). The root URL has no page and
returns 404, which is normal for an API.

## Getting started (frontend)

```
cd Frontend/pursuithq-web
npm install
npm run dev
```

Then visit `http://localhost:3000`. No env file is needed locally — the frontend
falls back to `http://localhost:5051` when `NEXT_PUBLIC_API_URL` is not set.

Both have to be running. The website is a face on top of the API.

On Windows, `.\start-dev.ps1` from the repo root starts both and opens the browser.

## Documentation

`docs/README.md` is the index. Start with `docs/START-HERE.md` for how to run
everything and what is built; `docs/Roadmap.md` for what is next.

There is no automated test suite yet.

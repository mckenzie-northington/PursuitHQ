# PursuitHQ

An all-in-one platform that empowers college students to manage coursework, internships, networking, and career planning.

## What it does

PursuitHQ helps students:

- Track courses and assignments
- Manage internship applications (saved, applied, interview, offer, rejected)
- Set and track career goals
- Monitor skills and certifications
- Organize networking contacts
- (Planned) Receive AI-powered career guidance

## Tech stack

**Backend:** C#, ASP.NET Core Web API (.NET 10 LTS), Entity Framework Core, PostgreSQL

**Frontend:** Next.js, JavaScript, Tailwind CSS

**Database:** PostgreSQL, pgAdmin

## Project structure

```
PursuitHQ
├── Backend/PursuitHQ/PursuitHQ.API   # ASP.NET Core Web API
├── Frontend                          # Next.js app
└── docs                              # Requirements, roadmap, database design, features
```

## Getting started (backend)

```
cd Backend/PursuitHQ/PursuitHQ.API
dotnet restore
dotnet build
dotnet run
```

Then visit `https://localhost:7136/swagger` for the API docs.

## Getting started (frontend)

```
cd Frontend
npm install
npm run dev
```

Then visit `http://localhost:3000`.

## Roadmap

See `docs/Roadmap.md` for the full sprint plan (auth, dashboard, academic planner, internship tracker, career growth, networking hub, analytics, and AI features).

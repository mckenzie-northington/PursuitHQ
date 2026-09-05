# PursuitHQ Documentation

The full plan for PursuitHQ — what it does, how it's built, and what it takes to run it for real users.

## Read in this order

| Document | What's in it |
|---|---|
| [Requirements.md](Requirements.md) | Purpose, target users, scope, user stories, functional and non-functional requirements, success criteria |
| [Features.md](Features.md) | Every feature in detail: behavior, acceptance criteria, entities involved, and which release it lands in |
| [DatabaseDesign.md](DatabaseDesign.md) | Every entity with its fields, the UML class diagram, the DbContext, and the build order |
| [ApiDesign.md](ApiDesign.md) | Every REST endpoint, conventions, status codes, error shape, pagination |
| [Architecture.md](Architecture.md) | System diagram, backend layers, project structure, file storage design, AI service design, frontend routes, configuration |
| [Security.md](Security.md) | Authentication, data isolation, upload security, secrets, privacy, pre-launch checklist |
| [Deployment.md](Deployment.md) | Hosting, environment variables, migrations, CI/CD, local setup, monitoring, launch checklist |
| [Roadmap.md](Roadmap.md) | Phased build plan from setup through analytics, with a definition of done |

## Quick summary

PursuitHQ is a student success platform: courses and a calendar covering classes and other activities, assignments, per-course study materials (files, folders, and typed notes), AI study tools that turn those materials into flashcards, practice quizzes, and study guides, an AI study planner, email reminders for deadlines and schedules, internship and job search plus application tracking, an AI-assisted resume builder, goals/skills/certifications, and student-to-student networking with messaging.

**Stack:** ASP.NET Core Web API on .NET 10 · Entity Framework Core · PostgreSQL · Next.js · Tailwind CSS

## Open decisions

These are recorded so they don't get lost. Update this list as they're settled.

1. **AI provider and budget** — which LLM API powers resume review, study planning, and study-tool generation, and what the hard monthly spend cap is. This is the only part of the stack that cannot be free; study-tool generation sends whole documents and is the main cost driver. See the cost-control rules in `Deployment.md`.
2. **Job-board API** — Adzuna is the leading candidate (free app id and key, documented API). LinkedIn and Indeed do not offer open job-search APIs and must not be scraped.
3. **External networking contacts** — keep `NetworkingContact` for recruiters and alumni alongside student-to-student networking, or drop it? *(Currently: planned for the Later phase.)*
4. **Email sending domain** — Resend's free tier covers the volume; a custom sending domain needs DNS records on a domain you control.
5. **Cold starts** — accept Render free's ~1 minute wake-up, keep the instance warm with the reminder cron, or pay ~$7/month once real users are on it.

## Keeping these current

If the design changes, update the doc before or alongside the code. These files are the reference another developer reads to understand the project — stale docs are worse than no docs.

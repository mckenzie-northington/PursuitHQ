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

PursuitHQ is a student success platform: courses and calendar, assignments, per-course study materials (files, folders, and typed notes), an AI study planner, an internship and job application tracker, an AI-assisted resume builder, goals/skills/certifications, and student-to-student networking with messaging.

**Stack:** ASP.NET Core Web API on .NET 10 · Entity Framework Core · PostgreSQL · Next.js · Tailwind CSS

## Open decisions

These are recorded so they don't get lost. Update this list as they're settled.

1. **External networking contacts** — keep `NetworkingContact` for tracking recruiters and alumni alongside student-to-student networking, or drop it? *(Currently: planned for the Later phase.)*
2. **AI provider** — which LLM API to use for resume review and study planning, and what the monthly spend cap should be.
3. **Email delivery** — which provider sends password resets and (later) deadline reminders.

## Keeping these current

If the design changes, update the doc before or alongside the code. These files are the reference another developer reads to understand the project — stale docs are worse than no docs.

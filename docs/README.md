# PursuitHQ Documentation

The full plan for PursuitHQ — what it does, how it's built, and what it takes to run it for real users.

## Read in this order

| Document | What's in it |
|---|---|
| [START-HERE.md](START-HERE.md) | **Read this first when picking the project back up** — how to start everything, what works, what's next, and fixes for problems already hit |
| [Requirements.md](Requirements.md) | Purpose, target users, scope, user stories, functional and non-functional requirements, success criteria |
| [Features.md](Features.md) | Every feature in detail: behavior, acceptance criteria, entities involved, and which release it lands in |
| [DatabaseDesign.md](DatabaseDesign.md) | Every entity with its fields, the UML class diagram, the DbContext, and the build order |
| [ApiDesign.md](ApiDesign.md) | Every REST endpoint, conventions, status codes, error shape, pagination |
| [Architecture.md](Architecture.md) | System diagram, backend layers, project structure, file storage design, AI service design, frontend routes, configuration |
| [Security.md](Security.md) | Authentication, data isolation, upload security, secrets, privacy, pre-launch checklist |
| [Deployment.md](Deployment.md) | Hosting, environment variables, migrations, CI/CD, local setup, monitoring, launch checklist |
| [Roadmap.md](Roadmap.md) | Phased build plan from setup through analytics, with a definition of done |

## Quick summary

PursuitHQ is a student success platform: courses and a calendar covering classes and other activities, assignments, per-course study materials (files, folders, and typed notes), AI study tools that turn those materials into flashcards, practice tests and study guides plus a per-course tutor you can ask questions, an AI study planner, email reminders for deadlines and schedules, an AI-assisted resume builder, and goals/skills/certifications.

**Stack:** ASP.NET Core Web API on .NET 10 · Entity Framework Core · PostgreSQL · Next.js · Tailwind CSS

## Open decisions

These are recorded so they don't get lost. Update this list as they're settled.

1. ~~**AI provider**~~ — **Decided: Google Gemini** (`gemini-3.8-flash`), behind an `IAiService` interface. **Billing is now enabled**, which lifted the free tier's 20-requests-per-minute ceiling; cost is roughly a penny per request. See `Architecture.md` §5d and `START-HERE.md` §5a. **Open sub-item:** check whether the paid tier still trains on submitted content before anyone else uses the AI features — that was true of the free tier and is a launch blocker either way, needing a privacy disclosure.
2. ~~**Job-board API**~~ — **Closed: no longer needed.** Job search and the application tracker were built and then removed in September 2026. Adzuna's listings were reliably stale — postings had closed by the time you clicked through — and maintaining a job feed was taking time from the study features. `Roadmap.md` §5 records what was deleted and what was deliberately kept so it can return.
3. **Time zones** — **Decided for now: stored `DateTime` values are wall-clock times, not instants.** Nothing is converted in either direction; a 9 AM class is 9 AM. This keeps the calendar correct while the app runs on one laptop. It leaves one known gap: "overdue" compares against the server's clock and will be wrong on a UTC host, so `ApplicationUser.TimeZone` has to be wired in before deployment. See `Roadmap.md` §3a.
4. ~~**Account recovery**~~ — **Built.** Request a link, set a new password, lockout cleared on success; tokens expire after an hour. The only missing piece is sending the email, which Phase 3b supplies — until then the link is logged by the API and shown on screen in development. Related decision: `forgot-password` reveals whether an account exists **only** in Development, because in production that would let anyone enumerate who has signed up.
5. **Email sending domain** — Resend's free tier covers the volume; a custom sending domain needs DNS records on a domain you control.
6. **Cold starts** — accept Render free's ~1 minute wake-up, keep the instance warm with the reminder cron, or pay ~$7/month once real users are on it.
7. **Dark mode is palette remapping, not per-element theming.** One CSS file rewrites the app's small colour palette when `.dark` is set, rather than a `dark:` variant on every class in fourteen files. It holds as long as `bg-white` always means "a surface". The first page that needs something to stay white in dark mode is the signal to revisit this. See the note at the top of `globals.css`.

## Keeping these current

If the design changes, update the doc before or alongside the code. These files are the reference another developer reads to understand the project — stale docs are worse than no docs.

These files drifted badly once already: the roadmap still showed the job tracker as finished and the calendar as not started, weeks after the opposite was true. Updating a doc in the same sitting as the code costs a few minutes; reconstructing what happened later costs much more.

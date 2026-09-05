# PursuitHQ — Deployment & Operations

This covers what it takes to run PursuitHQ somewhere other people can actually use it.

## 1. Environments

| Environment | Purpose | Database | Storage |
|---|---|---|---|
| Local | Day-to-day development | Local PostgreSQL via pgAdmin | Local folder |
| Production | Public, real users | Managed PostgreSQL | Cloud object storage |

A staging environment is optional early on; add one before the first release that has real users on it.

## 2. Hosting — the free plan

**Goal: $0/month.** This is achievable for everything except AI, with one real tradeoff. Verified September 2026 — free tiers change, so re-check before launch.

### The all-free stack

| Piece | Service | Free allowance | Catch |
|---|---|---|---|
| Frontend | **Vercel Hobby** | Unlimited personal projects, custom domain, HTTPS | Non-commercial use only |
| Database | **Neon Free** | 0.5 GB storage, 100 compute-hours/month, 10 branches | Scales to zero after 5 min idle (first query wakes it, ~1s). Permanent plan, not a trial |
| Backend API | **Render Free web service** | 512 MB RAM, 750 instance-hours/month | **Spins down after 15 minutes of no traffic; the next request waits ~1 minute** |
| Email | **Resend Free** | 3,000 emails/month, 100/day | Enough for reminders at small scale |
| CI/CD | **GitHub Actions** | 2,000 minutes/month on free accounts | — |
| File storage | Render disk is ephemeral on free | — | Use Cloudflare R2 (10 GB free, no egress fees) or Supabase Storage (1 GB free) |

**Total: $0/month**, excluding AI.

### The one real problem: cold starts

Render's free web service sleeps after 15 minutes idle, and the next visitor waits roughly a minute for it to wake. For a public app that's a bad first impression.

Two ways around it, both free:

1. **Keep it warm.** A free cron service (cron-job.org, UptimeRobot) pings `/health` every 10 minutes. A month is ~730 hours and the free allowance is 750 instance-hours, so an always-on service *just* fits — with no margin. Watch the hours.
2. **Accept it.** Fine while you're the main user and during development.

### Important: do NOT use Render's free PostgreSQL

**Free Render Postgres databases are deleted 30 days after creation** (with a 14-day grace period to upgrade). Neon's free plan is permanent. Use Render for the API and Neon for the database.

### AI: Gemini free tier

**Decision: Google Gemini.** Its Flash models have a free tier with no credit card required, which covers development and personal use at $0.

Two things to know:

1. **Free tier content is used to improve Google's products.** Paid tiers exclude this. Since study tools and resume review send students' notes and resumes, disclose it in the privacy policy or move to a paid tier before opening the app to other people. See Security.md.
2. **Free tier quotas are per-project and change.** Check your live limits in Google AI Studio rather than trusting any number written down here.

**Upgrade path when free isn't appropriate anymore:** Azure OpenAI paid by the **Azure for Students** credit ($100/year, renewable while enrolled, no credit card). That removes the training clause without costing you money. Because everything goes through `IAiService`, switching is a configuration change — see Architecture.md.

### What is not free at scale: AI

LLM APIs bill per request, and PursuitHQ's AI features (resume review, study plans, flashcard/quiz/study-guide generation) are the expensive kind — study-tool generation sends whole documents as input.

Cost control, in order of importance:
- Per-user daily caps on AI endpoints (`Ai:RequestsPerUserPerDay`)
- Truncate or chunk uploads before sending; never send a 200-page PDF whole
- Cache generated output — regenerate only when the student asks
- Use a smaller/cheaper model for flashcards and quizzes; save the stronger model for resume review
- Set a hard billing cap with the provider

Budget a few dollars a month for personal use. Opening AI features to public signups without caps is how a student project produces a surprise bill.

### If free isn't good enough

| Upgrade | Cost | What it fixes |
|---|---|---|
| Render Starter | ~$7/month | No spin-down, no cold starts — the single highest-value upgrade |
| Neon Launch | ~$19/month | More storage and compute when 0.5 GB gets tight |
| **Azure for Students** | **$100 credit/year, no credit card** | You qualify as a full-time student: $100/year in credit, renewable annually while enrolled, plus 65+ always-free services. Also check the GitHub Student Developer Pack for additional hosting credits |

Recommended path: start entirely free, and if cold starts become annoying once other people are using it, $7/month for Render Starter is the one upgrade worth making first.

## 3. Environment Variables

Set these on the host; never commit them.

**Backend**

| Variable | Example / notes |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=...;Database=pursuithq;Username=...;Password=...` |
| `Jwt__Key` | 32+ character random string |
| `Jwt__Issuer` / `Jwt__Audience` | `https://api.pursuithq.app` |
| `Jwt__ExpiryMinutes` | `60` |
| `FileStorage__Provider` | `Cloud` in production |
| `FileStorage__ConnectionString` | Blob/S3 credentials |
| `FileStorage__MaxFileSizeBytes` | `26214400` (25 MB) |
| `Ai__ApiKey` | LLM provider key |
| `Ai__Provider` | `Gemini` |
| `Ai__ApiKey` | Gemini API key from Google AI Studio |
| `Ai__FlashcardModel` / `Ai__QuizModel` / `Ai__ResumeModel` | Model id per task |
| `Ai__RequestsPerUserPerDay` | `20` |
| `Email__Provider` | `Resend` |
| `Email__ApiKey` | Resend API key |
| `Email__FromAddress` | `reminders@yourdomain.com` |
| `JobSearch__Provider` | `Adzuna` |
| `JobSearch__AppId` / `JobSearch__AppKey` | Job-board API credentials |
| `Jobs__SchedulerSecret` | Shared secret the external cron sends to trigger reminder jobs |
| `Cors__AllowedOrigins` | `https://pursuithq.vercel.app` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

**Frontend**

| Variable | Notes |
|---|---|
| `NEXT_PUBLIC_API_URL` | Public base URL of the API |

Anything prefixed `NEXT_PUBLIC_` is visible in the browser — never put a secret there.

## 4. Database Migrations

EF Core migrations are the only way the schema changes. Never edit the database by hand.

```bash
# create a migration after changing entities
dotnet ef migrations add AddStudyMaterials

# apply locally
dotnet ef database update

# generate a script to apply in production
dotnet ef migrations script --idempotent --output migrate.sql
```

**Rules**
- Migrations are committed to git alongside the entity changes that caused them.
- Production migrations run as a deliberate deploy step, not automatically at app startup.
- Back up the production database before applying a migration that drops or renames anything.

## 5. CI/CD (GitHub Actions)

Suggested pipeline, triggered on push to `main`:

1. **Build & test API** — `dotnet restore`, `dotnet build`, `dotnet test`
2. **Build frontend** — `npm ci`, `npm run build`
3. **Security check** — `dotnet list package --vulnerable`, `npm audit`
4. **Deploy** — publish the API to the host; Vercel deploys the frontend from the same push
5. **Migrate** — apply the migration script as a gated step

Protect `main` so it can only be updated through a pull request that passes CI. Work on feature branches named `feature/study-materials`, `fix/upload-limit`, etc.

## 6. Local Setup (for a new developer)

```bash
git clone https://github.com/mckenzie-northington/PursuitHQ.git
cd PursuitHQ

# Backend
cd Backend/PursuitHQ/PursuitHQ.API
dotnet restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=pursuithq;Username=postgres;Password=..."
dotnet user-secrets set "Jwt:Key" "<32+ char random string>"
dotnet ef database update
dotnet run          # https://localhost:7136/swagger

# Frontend
cd ../../../Frontend
npm install
npm run dev         # http://localhost:3000
```

Requires: .NET 10 SDK, Node.js LTS, PostgreSQL running locally.

## 7. Monitoring & Operations

| Concern | Approach |
|---|---|
| Logging | Structured logging (Serilog) to the host's log stream; no sensitive content logged |
| Errors | Global exception handler returning the standard error shape; alerting via the host or Sentry |
| Uptime | Health endpoint at `/health`; external uptime check pinging it |
| Backups | Daily automated database backup with 7-day retention; restore tested at least once before launch |
| File durability | Cloud storage handles redundancy; local disk storage is development-only |

## 8. Launch Checklist

- [ ] Production database provisioned, migrations applied
- [ ] All environment variables set on both hosts
- [ ] Cloud file storage configured and an upload/download verified end to end
- [ ] Custom domain with HTTPS on API and frontend
- [ ] CORS pointing at the real frontend origin
- [ ] Security checklist in `Security.md` fully completed
- [ ] Backups enabled and a restore rehearsed
- [ ] Health check and error alerting live
- [ ] Registration, login, upload, and one AI feature smoke-tested in production
- [ ] Privacy policy and terms published if the app takes public signups

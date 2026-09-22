# PursuitHQ — Deployment & Operations

What it takes to run PursuitHQ somewhere other people can actually use it.

Last verified against the code: 16 September 2026. Free-tier allowances below were checked in September 2026 and change often — re-check before launch rather than trusting this table.

## 1. Environments

| Environment | Purpose | Database | Storage |
|---|---|---|---|
| Local | Day-to-day development | Local PostgreSQL via pgAdmin | Local folder |
| Production | Public, real users | Managed PostgreSQL | Cloud object storage |

A staging environment is optional early; add one before the first release with real users on it.

## 2. Hosting — the free plan

**Goal: $0/month.** Achievable for everything except AI, with one real tradeoff.

| Piece | Service | Free allowance | Catch |
|---|---|---|---|
| Frontend | **Vercel Hobby** | Unlimited personal projects, custom domain, HTTPS | Non-commercial use only |
| Database | **Neon Free** | 0.5 GB storage, 100 CU-hours/month, 5 GB transfer | Permanently free, no overage charges. Scales to zero after 5 min idle. **See §2a — polling breaks that assumption.** |
| Backend API | **Render Free web service** | 512 MB RAM, 750 instance-hours/month | Spins down after 15 minutes idle; the next request waits ~1 minute |
| Email | **Resend Free** | 3,000 emails/month, 100/day | Enough at small scale |
| CI/CD | **GitHub Actions** | 2,000 minutes/month | — |
| File storage | Cloudflare R2 | 10 GB, no egress fees | **Not yet implemented — see §7** |

**Do NOT use Render's free PostgreSQL.** Free Render databases are deleted 30 days after creation. Neon's free plan is permanent. Render for the API, Neon for the database.

### Cold starts

Render's free service sleeps after 15 idle minutes. Either keep it warm with a free cron pinging `/health` every 10 minutes (see §8), or accept it while you are the main user. A month is ~730 hours against 750 free instance-hours, so an always-warm service just fits, with no margin.

## 2a. Polling versus the Neon free tier — read before launch

This is the most important operational fact about the current architecture, and it is not obvious, because each decision is sensible on its own.

Messaging has no SignalR. It polls. One open conversation produces:

| Every | Call | Database write? |
|---|---|---|
| 2.5s | `GET /presence` (typing + read receipts) | no |
| 5s | `GET /messages` | no |
| 5s | `POST /read` | **yes** |
| 15s | `GET /conversations` | no |
| ≤2.5s while typing | `POST /typing` | **yes** |
| 30s | unread dot | no |

All of it pauses when the browser tab is hidden.

Neon's free plan gives **100 CU-hours** a month and scales the compute to zero after five idle minutes. That allowance is generous *because* it assumes the database is idle most of the time. Polling every 2.5 seconds means it never is.

A CU-hour is compute size multiplied by time, and the free plan autoscales from 0.25 CU. So a database kept permanently awake at the smallest size burns 0.25 CU-hours per hour, and a month of roughly 730 hours costs about **183 CU-hours against an allowance of 100**. In practice **one person leaving one tab open exhausts the month somewhere around day 16** — sooner if queries push the compute above its minimum size.

**It does not bill you for going over.** The free plan has no overages: the compute suspends, open connections drop and new ones fail until the next billing cycle. Your data is never deleted. So the failure mode is the app going down mid-month, not a surprise invoice — which is the right trade for a student project, but it is still the app going down.

**The largest of these is already fixed.** `POST /read` used to fire on every poll whether or not anything had arrived — a database write per person per open tab every five seconds. It now writes only when the newest message id actually changed, which removes most of the write traffic. It also made "mark as unread" stick on the conversation you are looking at; the unconditional write used to undo it within five seconds.

Two mitigations remain, neither urgent at one user:

1. **Back off when idle.** Widen the poll interval after a minute with no new messages; snap back on activity.
2. **SignalR.** The real answer, and what the code was structured for — `lib/useUnread.js` is the seam. Replaces nearly all the polling with one connection.

The reads still cost compute even though they no longer write, so watch the Neon dashboard for the first week rather than assuming the problem is gone.

### AI cost

**Google Gemini**, **paid plan — billing enabled**. Three things to know:

1. **This must stay on the paid plan.** On the free tier Google uses submitted content to improve its products; on a paid plan it does not. Study tools and resume review send students' notes and resumes, so the free tier stopped being an option the moment anyone else had an account. Roughly a penny per request.
2. **An API key in a project without billing silently reverts to free-tier terms.** No error, no log line, no failed request — just different terms on somebody else's coursework. If you rotate the key or move projects, confirm billing is enabled on the *same* project.
3. Paid-tier quotas are per-project and change. Check Google AI Studio, not this document.

Cost control, in order: the per-user daily cap (`Ai:RequestsPerUserPerDay`, already built as `AiUsageLimiter`), truncating documents before sending, caching generated output, cheaper models for flashcards and quizzes, and a hard billing cap with the provider.

**Upgrade path:** Azure for Students gives $100/year in credit, renewable while enrolled, no credit card. Because everything goes through `IAiService`, switching providers is a configuration change.

### If free is not good enough

| Upgrade | Cost | What it fixes |
|---|---|---|
| Render Starter | ~$7/month | No spin-down, no cold starts — the highest-value single upgrade |
| Neon Launch | ~$19/month | More storage and compute |

## 3. Building the API — Docker

`PursuitHQ.API/Dockerfile` builds and runs the API. Hosts build it with the **API folder** as the context, so set the root directory to `Backend/PursuitHQ/PursuitHQ.API`.

It listens on port **8080** (`ASPNETCORE_URLS`).

**Two things in that Dockerfile must not be "optimised" away.** It installs `tzdata` and uses the Debian image rather than Alpine.

PursuitHQ stores wall-clock times and converts them with IANA zone ids (see `Architecture.md`). That needs both ICU and the zone database. Alpine ships without ICU, and setting `InvariantGlobalization=true` strips the zone data. Either one turns every due date, reminder and class time into the wrong hour — silently, with no error anywhere. If you change the base image, test a time zone conversion before believing it works.

`.dockerignore` excludes `bin/` and `obj/`, which hold absolute paths from whichever machine built them last and break the container build with an error about a path that does not exist.

## 4. Environment Variables

Set these on the host. Never commit them.

**Backend (Render)**

| Variable | Notes |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production`. Gates Swagger and the `forgot-password` account disclosure — see `Security.md` §1 |
| `PORT` | `8080`, matching the Dockerfile |
| `ConnectionStrings__DefaultConnection` | Use Neon's **.NET** connection string; it includes the SSL settings |
| `Jwt__Key` | 32+ random characters. Generate fresh: `openssl rand -base64 48`. Never the development value |
| `Jwt__Issuer` | e.g. `https://api.yourdomain.com` |
| `Jwt__Audience` | **`PursuitHQClient`** — must differ from the 2FA audience or two-step verification silently stops working. `Security.md` §1 |
| `Jwt__ExpiryMinutes` | `60`. Lowered from 480 because the token still lives in `localStorage` |
| `Cors__AllowedOrigins` | Comma-separated: `https://yourdomain.com,https://www.yourdomain.com`. Indexed keys (`__0`, `__1`) also work |
| `Ai__ApiKey` | Gemini key |
| `Ai__RequestsPerUserPerDay` | e.g. `20` |
| `Email__ApiKey` | Resend key |
| `Email__FromAddress` | `noreply@send.yourdomain.com` — the Resend **subdomain**, see §10 |
| `Email__AppUrl` | Public frontend URL — used for links inside emails |
| `FileStorage__Provider` | `S3` in production. Anything else means local disk |
| `FileStorage__ServiceUrl` | `https://<account-id>.r2.cloudflarestorage.com` |
| `FileStorage__Bucket` | R2 bucket name |
| `FileStorage__AccessKeyId` | From an R2 API token |
| `FileStorage__SecretAccessKey` | From an R2 API token — a secret |

**Frontend (Vercel)**

| Variable | Notes |
|---|---|
| `NEXT_PUBLIC_API_URL` | Public base URL of the API |

Anything prefixed `NEXT_PUBLIC_` is visible in the browser. Never put a secret there.

**Not real — do not set these.** Earlier versions of this document listed `FileStorage__ConnectionString`, `JobSearch__AppId`/`AppKey` and `Jobs__SchedulerSecret`. No code reads any of them. The job-board feature was removed in September 2026, and reminders run in-process (§6), not via an external cron with a shared secret.

## 5. Database Migrations

EF Core migrations are the only way the schema changes. Never edit the database by hand.

```bash
# create a migration after changing entities
dotnet ef migrations add AddStudyMaterials

# apply locally
dotnet ef database update
```

To apply to production from your machine, pass the connection string for that one command so it never touches your user-secrets:

```bash
ConnectionStrings__DefaultConnection="<production string>" dotnet ef database update
```

Or generate a script to run as a gated deploy step:

```bash
dotnet ef migrations script --idempotent --output migrate.sql
```

**Rules**
- Migrations are committed alongside the entity changes that caused them.
- Production migrations are a deliberate step you run, never automatic at startup. With more than one instance, auto-migration means two processes racing the same schema change.
- Back up before applying anything that drops or renames.

**Read every generated migration before applying it.** EF's ordering has been wrong twice in this project, both times when a migration both added and dropped a column: it generated the `DropColumn` before the `AddColumn`, which would have destroyed the data being copied. Both were caught by reading the file. It also ignores C# property initialisers in some cases, so a column meant to default to `true` can be generated as `false` and silently opt every existing row out.

## 6. Background Work

Reminders run **in-process**, on a timer inside the API (`ReminderBackgroundService`, every 5 minutes). There is no external scheduler and no `/api/jobs/run` endpoint.

Two consequences:

- On a host that sleeps, the timer sleeps too. A reminder due during a nap goes out late, when the next request wakes the service. The keep-warm ping in §8 also keeps reminders punctual.
- Email is queued in memory (`EmailQueue`) and sent by a background worker. **Anything still queued when the process stops is lost.** That was an acceptable trade when the queue only carried "you added a course" confirmations; it now also carries message notifications and connection requests, which matter more. Revisit when the API stops sleeping.

## 7. File Storage

Two implementations behind `IFileStorageService`:

| Provider | When | Notes |
|---|---|---|
| `Local` | Development, and the fallback | Writes under `FileStorage:LocalPath`, outside `wwwroot`. **Loses every file when the host restarts**, which on Render's free plan is every time it sleeps |
| `S3` | Production | Cloudflare R2, or anything else speaking the S3 API |

The provider is chosen at startup from `FileStorage:Provider`, and **falls back to local disk when any S3 credential is missing** rather than refusing to start — a half-configured bucket should degrade to something that works while you fix it. The startup log says which was chosen; check it, because silently writing to the wrong place is the bad outcome.

The stored key format is identical between the two, so a row written by one works unchanged with the other. Only the bytes need moving.

### Setting up R2

1. Cloudflare dashboard → R2 → create a bucket.
2. Create an R2 API token with **Object Read & Write** on that bucket. You get an access key id and a secret.
3. Set the five `FileStorage__*` variables in §4. `ServiceUrl` is `https://<account-id>.r2.cloudflarestorage.com` — the account id is on the R2 overview page.

Keep the bucket **private**. Nothing serves files directly from it; every download goes through an authorized endpoint, which is what keeps one student's attachments away from another's.

## 8. Health Checks

Two endpoints, deliberately different:

| Endpoint | Checks | Use for |
|---|---|---|
| `/health` | Nothing — answers immediately | Keep-warm pings and uptime monitoring |
| `/health/ready` | Database connectivity | Diagnosing a deploy |

**The keep-warm cron must hit `/health`, never `/health/ready`.** A database-touching ping every 10 minutes would keep Neon permanently awake and spend the whole monthly compute allowance proving the app is alive — see §2a. That is the only reason there are two.

Set Render's own Health Check Path to `/health` too.

## 9. Custom Domain

Add the domain in the Render and Vercel dashboards first; each tells you the exact record to create. Then create them at your DNS provider, using the values those dashboards give you rather than any written here — they change.

Typically: apex and `www` point at Vercel, and an `api` subdomain points at Render.

**If your DNS is behind Cloudflare, two settings will break this.**

- **Set the records to DNS only (grey cloud), not proxied (orange).** With the proxy on, Vercel and Render frequently cannot complete their certificate challenge, and you sit on "pending certificate" with no useful error. Both provide their own TLS. You can enable the proxy later, once certificates have issued.
- **SSL/TLS mode must be Full (strict).** On **Flexible**, Cloudflare talks to the origin over plain HTTP, the app redirects to HTTPS, Cloudflare retries over HTTP, and you get an infinite redirect loop — `ERR_TOO_MANY_REDIRECTS` on a site that works perfectly at its `.onrender.com` address. This is the most common way this setup fails.

**Put the API on a subdomain of the same registrable domain as the frontend** (`api.yourdomain.com`, not the Render hostname). Beyond looking right, it makes the planned move of the JWT into an httpOnly cookie far simpler: a cookie scoped to the parent domain with `SameSite=Lax`, instead of the fragile cross-site `SameSite=None` that unrelated domains would force.

## 10. Email — sending and receiving

Two different jobs, and they must be kept apart or they fight over the same DNS records.

| Job | Tool | Where its records live |
|---|---|---|
| **Sending** the app's mail (reminders, notifications, password resets) | Resend | a **subdomain**, `send.yourdomain.com` |
| **Receiving** mail sent to you (`support@yourdomain.com`) | Cloudflare Email Routing (free) | the **root** domain |

### Verify a subdomain in Resend, not the root

Resend's own guidance is to use a subdomain, and here it is not optional — it is what stops the two halves colliding.

Both Resend and Cloudflare Email Routing want to put **MX records** and an **SPF TXT record** on whatever hostname you give them. A hostname can only have one valid SPF record: two is not "more secure", it is a permanent hard failure that makes receiving mail servers reject or quarantine everything you send. Point both at the root and you get exactly that.

Verify `send.yourdomain.com` instead and the records land on different hostnames, so there is no conflict at all. As a bonus, a problem with transactional mail never damages the reputation of your main domain.

`Email__FromAddress` then becomes something like `noreply@send.yourdomain.com`.

### Receiving

Cloudflare dashboard → **Compute → Email Service → Email Routing** → onboard the domain. Verify a destination inbox (your ordinary personal address), then add a rule routing `support` to it. Cloudflare adds its own MX, SPF and DKIM records on the root automatically.

It **forwards only** — it receives mail and delivers it to your inbox, it cannot send as that address. That is fine: the app sends through Resend, and this exists so somebody can reach a human.

### DMARC

One record, on the root, covering both:

| Type | Name | Content |
|---|---|---|
| TXT | `_dmarc` | `v=DMARC1; p=none; rua=mailto:support@yourdomain.com` |

DMARC at the root applies to subdomains too, so this covers `send.yourdomain.com` without a second record.

Until SPF, DKIM and DMARC all align, reminder and message email lands in spam. Every email feature will appear to do nothing, with no error anywhere — the queue reports success either way.

## 11. CI/CD (GitHub Actions)

Suggested pipeline on push to `main`:

1. **Build API** — `dotnet restore`, `dotnet build`
2. **Build frontend** — `npm ci`, `npm run build`
3. **Security check** — `dotnet list package --vulnerable`, `npm audit`
4. **Deploy** — Render and Vercel both deploy from the push
5. **Migrate** — apply the migration script as a gated step

**There is no `dotnet test` step because there are no tests.** The solution contains only `PursuitHQ.API`. Add the step when the first test project exists; until then a green pipeline means "it compiles", nothing more.

Protect `main` so it only changes through a pull request that passes CI.

## 12. Local Setup (for a new developer)

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
cd ../../../Frontend/pursuithq-web
npm install
npm run dev         # http://localhost:3000
```

Requires .NET 10 SDK, Node.js LTS, PostgreSQL running locally. On Linux or WSL, `libicu` must be installed or every time zone conversion fails.

## 13. Monitoring & Operations

| Concern | State |
|---|---|
| Health endpoint | **Built** — `/health` and `/health/ready` |
| Logging | Default ASP.NET logging to the host's stream. Structured logging (Serilog) is not set up |
| Errors | `UseExceptionHandler` returns the standard shape. **No error monitoring — a 500 in production is invisible unless someone reports it.** Sentry's free tier would fix this |
| Uptime | External check pinging `/health` |
| Backups | Neon provides them. Untested until you rehearse a restore |
| File durability | R2 when `FileStorage__Provider=S3`; otherwise local disk, which does not survive a restart — see §7 |

## 14. Launch Checklist

- [ ] Production database provisioned, migrations applied
- [ ] All environment variables set on both hosts
- [ ] `ASPNETCORE_ENVIRONMENT=Production` confirmed
- [ ] `Jwt__Audience` is `PursuitHQClient`, distinct from the 2FA audience
- [ ] Custom domain with HTTPS on API and frontend; DNS-only records; SSL mode Full (strict)
- [ ] `Cors__AllowedOrigins` matches the real frontend origins exactly, `www` included
- [ ] `/health` and `/health/ready` both return Healthy
- [ ] Keep-warm cron pointed at `/health`, not `/health/ready`
- [ ] SPF, DKIM and DMARC verified; a real email received
- [ ] Security checklist in `Security.md` §9 worked through
- [ ] Backups enabled and one restore rehearsed
- [ ] Error monitoring live
- [ ] Register, log in, send a message, and one AI feature smoke-tested in production
- [ ] Privacy policy and terms published if taking public signups
- [ ] `FileStorage__Provider=S3` with R2 credentials set, and an upload verified to survive a redeploy (§7)
- [ ] Startup log checked — it names which storage provider was chosen

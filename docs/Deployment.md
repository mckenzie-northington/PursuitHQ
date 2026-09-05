# PursuitHQ — Deployment & Operations

This covers what it takes to run PursuitHQ somewhere other people can actually use it.

## 1. Environments

| Environment | Purpose | Database | Storage |
|---|---|---|---|
| Local | Day-to-day development | Local PostgreSQL via pgAdmin | Local folder |
| Production | Public, real users | Managed PostgreSQL | Cloud object storage |

A staging environment is optional early on; add one before the first release that has real users on it.

## 2. Hosting Options

| Piece | Recommended | Alternatives |
|---|---|---|
| Frontend (Next.js) | Vercel — free tier, built for Next.js, deploys from GitHub | Netlify, Azure Static Web Apps |
| Backend (.NET API) | Azure App Service — first-class .NET support | Render, Railway, Fly.io, AWS App Runner |
| Database | Neon or Supabase (managed Postgres, free tier) | Azure Database for PostgreSQL, Railway Postgres |
| File storage | Azure Blob Storage | AWS S3, Cloudflare R2 |

Rough cost at low usage: frontend free, database free tier, backend ~$0–15/month, storage under $1/month, plus AI API usage (the main variable — hence the per-user rate limit).

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
| `Ai__Model` | Model identifier |
| `Ai__RequestsPerUserPerDay` | `20` |
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

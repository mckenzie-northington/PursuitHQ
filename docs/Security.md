# PursuitHQ — Security & Privacy

Because PursuitHQ holds students' coursework, resumes, job applications, private notes, and direct messages, security is a feature requirement, not an afterthought.

## 1. Authentication

ASP.NET Core Identity handles password hashing and account management. The API issues a JWT on successful login.

**Password policy**
- Minimum 8 characters
- At least one uppercase letter, one lowercase letter, and one digit
- Rejected if it matches a common-password list
- Hashed by Identity (PBKDF2 with salt) — plaintext passwords are never stored or logged

**Token handling**
- Access token lifetime: 60 minutes
- Refresh token (v1.1): 7 days, rotated on use, revocable on logout
- Tokens are signed with a key from configuration, minimum 32 characters, never committed to source control
- The frontend stores tokens in memory or an httpOnly cookie — not in `localStorage`, which is readable by any injected script

**Account protection**
- Lockout after 5 failed login attempts within 15 minutes
- `forgot-password` always returns 200 whether or not the email exists, so the endpoint can't be used to discover accounts
- Password reset tokens are single-use and expire in 1 hour

## 2. Authorization & Data Isolation

This is the single most important rule in the codebase:

> Every database query is filtered by the user id from the JWT. The client never supplies a user id.

```csharp
// Correct
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
var course = await _db.Courses
    .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
if (course is null) return NotFound();

// Wrong — trusts the client
var course = await _db.Courses.FindAsync(id);
```

**Rules**
- Requesting a record you don't own returns `404`, not `403`, so existence isn't leaked.
- Nested resources verify ownership of the parent too (a material request checks that the course belongs to the caller).
- Messages are readable only by their sender and receiver.
- Student search returns public profile fields only — never email, records, or materials.

## 3. File Upload Security

Uploads are the highest-risk surface in the app.

| Control | Implementation |
|---|---|
| Size limit | Rejected at 25 MB per file before the body is read (413) |
| Per-user quota | 1 GB total; checked before accepting an upload |
| Type allow-list | Extension **and** MIME type must both be on the list (415 otherwise) |
| No executables | `.exe`, `.dll`, `.bat`, `.sh`, `.js`, `.html` are never accepted |
| Path traversal | Client file names are never used on disk; storage names are GUID-based |
| Static serving | Storage folder lives outside `wwwroot`; downloads only through an authorized endpoint |
| Download headers | `Content-Disposition: attachment` so files download rather than render in the browser |
| Original name | Sanitized before being stored or echoed back |
| Virus scanning | Not in MVP; noted as a production hardening item if the app takes public signups at scale |

## 4. API Security

- **HTTPS only** in production; HTTP redirects to HTTPS, with HSTS enabled.
- **CORS** restricted to the deployed frontend origin — never `AllowAnyOrigin` with credentials.
- **Input validation** on every DTO with data annotations or FluentValidation; invalid input returns 400 with field-level detail.
- **SQL injection** is prevented by EF Core parameterization; no string-concatenated SQL.
- **Rate limiting:** general endpoints 100 requests/minute per user; AI endpoints capped per day per user (config: `Ai:RequestsPerUserPerDay`) to bound both abuse and cost.
- **Error responses** never include stack traces or SQL in production; the developer exception page is development-only.
- **Logging** never records passwords, tokens, resume content, note content, or message bodies.

## 5. Secrets Management

| Environment | Where secrets live |
|---|---|
| Local development | .NET user-secrets (`dotnet user-secrets set ...`) — outside the repo |
| Production | Environment variables / the host's secret store |
| Never | `appsettings.json`, source control, frontend code, screenshots |

The `.gitignore` already excludes `*.user` and environment files. If a secret is ever committed, rotate it — deleting the commit is not enough.

## 6. Privacy & Data Handling

- A student's materials, notes, resumes, applications, and goals are visible only to them.
- Messages are visible only to the two participants.
- **Account deletion** removes all owned rows and all stored files, not just the login record.
- **Data export** returns the user's records as JSON on request.
- AI prompts contain only the requesting user's own data, and no data from other users is ever included in a prompt.
- **AI provider data handling.** PursuitHQ uses Google Gemini. On Gemini's **free** tier, Google uses submitted content to improve their products; on paid tiers it does not. Because study tools and resume review submit students' notes, coursework, and resumes, this must be disclosed in the privacy policy before anyone else uses those features — or the app must move to a paid tier first. Track this as a launch blocker, not a nice-to-have.
- The AI API key lives only in server configuration. The frontend never calls an AI provider directly; doing so would expose the key to every visitor.
- If the app is opened to public signups, add: a privacy policy stating what is stored and that content is sent to an AI provider when AI features are used, terms of service, and a contact address.

## 7. Pre-Launch Security Checklist

- [ ] HTTPS enforced, HSTS on
- [ ] CORS locked to the real frontend origin
- [ ] JWT signing key is a strong value from environment variables
- [ ] No secrets present anywhere in git history
- [ ] Developer exception page disabled in production
- [ ] Rate limiting active on auth and AI endpoints
- [ ] Upload size, quota, and type allow-list enforced and tested
- [ ] Ownership checks verified by tests on every endpoint that takes an id
- [ ] Account deletion verified to remove both database rows and stored files
- [ ] Database backups configured and a restore tested at least once
- [ ] Dependencies updated; `dotnet list package --vulnerable` and `npm audit` clean
- [ ] AI data-handling disclosed in the privacy policy, or a paid AI tier in use before public signups
- [ ] AI API key present only in server-side configuration, never in frontend bundles

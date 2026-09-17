# PursuitHQ — Security & Privacy

Because PursuitHQ holds students' coursework, resumes, private notes and — since September 2026 — their conversations with each other, security is a feature requirement rather than an afterthought.

**How to read this document.** Everything below describes what the code actually does today. Where something is not built, it says so in the text rather than being quietly omitted. Section 9 lists the known gaps in one place. If you ever find this document claiming something the code does not do, fix the document first — a checklist that lies to you is worse than no checklist, because you stop looking.

Last verified against the code: 16 September 2026.

## 1. Authentication

ASP.NET Core Identity handles password hashing and account management. The API issues a JWT on successful login.

**Minimum age: 16.** Checked at registration against a date of birth, which is stored (the rules that apply to somebody depend on their age and change as they get older, so "passed the check on the day they joined" answers the wrong question a year later). It is never shown to another student. Sixteen rather than thirteen because children's privacy rules are strict, vary by jurisdiction, and are a poor fit for a project maintained by one person — and PursuitHQ is aimed at college students anyway. `EducationLevel` still offers "High school", which is why the check exists at all.

**Terms acceptance** is recorded as `TermsAcceptedAt` at sign-up. The checkbox is required and the form will not submit without it.

**Password policy**
- Minimum 8 characters
- At least one uppercase letter, one lowercase letter, and one digit
- Non-alphanumeric characters are not required
- Hashed by Identity (PBKDF2 with salt) — plaintext passwords are never stored or logged

**Account protection**
- Lockout after 5 failed attempts, for 15 minutes (`Program.cs`)
- `forgot-password` returns the same response whether or not the account exists. It reveals the difference **only** when `ASPNETCORE_ENVIRONMENT=Development`, so that a developer can test the flow. Getting that variable wrong in production turns the endpoint into an account-discovery tool.
- Password reset tokens are single-use and time-limited

**Token handling — read this carefully**

| | Current state |
|---|---|
| Lifetime | **60 minutes** (`Jwt:ExpiryMinutes`). Lowered from 480 on 16 September 2026: an eight-hour token is a much larger prize while it still lives in `localStorage` |
| Storage | **`localStorage`** in the browser |
| Signing key | From configuration, never committed |
| Refresh tokens | Not built |

The storage line is the single biggest security gap in the project. `localStorage` is readable by any script that runs on the page, so one cross-site-scripting hole anywhere in the app is a full account takeover for the life of the token. Shortening the lifetime to an hour reduces the prize; it does not close the hole.

React escapes rendered text by default and there is no known XSS hole today. But the app now displays a great deal of text written by *other people* — messages, group names and descriptions, connection-request notes, sender names — and the point of an httpOnly cookie is that it survives the hole you did not know about.

The fix is planned and the domain layout already supports it: with the API on a subdomain of the same registrable domain as the frontend, the cookie can be scoped to the parent domain with `SameSite=Lax` rather than needing the more fragile cross-site `SameSite=None`. See `docs/Roadmap.md`.

**Two-step verification** (built)
- TOTP via any authenticator app, using Identity's authenticator token provider
- Ten single-use recovery codes, shown once and stored only as hashes
- Wrong codes call `AccessFailedAsync`, so the 5-attempt lockout covers the second factor too, not just the password
- The QR code is rendered in the browser. The `otpauth://` URI contains the shared secret and must never be sent to an image-generation service

**The audience split is load-bearing.** Between password and code, the API issues a *pending* token with the audience `PursuitHQ2FA` and a five-minute life. Ordinary tokens carry `PursuitHQClient`. Every normal endpoint validates the ordinary audience, so the pending token is rejected everywhere except the code-verification endpoint.

If those two audiences are ever made equal, or `ValidateAudience` is relaxed, the pending token becomes a fully valid session and **the second factor silently turns into decoration** — the app will look and behave exactly as if 2FA works. Treat `Jwt:Audience` in production configuration as a security setting, not a label.

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
- Requesting a record you do not own returns `404`, not `403`, so existence is not leaked.
- Nested resources verify ownership of the parent too.

### Messaging is a different kind of risk

Everywhere else in PursuitHQ, a missing filter shows someone their own empty page. In messaging, a missing filter shows them **a conversation between two other people**.

Every endpoint in `ConversationsController` therefore starts by proving active membership through one helper:

```csharp
private Task<ConversationMember?> MemberAsync(int conversationId, CancellationToken ct) =>
    _db.ConversationMembers.FirstOrDefaultAsync(
        m => m.ConversationId == conversationId
             && m.UserId == CurrentUserId
             && m.Status == MembershipStatus.Active, ct);
```

Nothing reads or writes a message without going through it. Four different situations — the conversation does not exist, you were never in it, you were invited but have not joined, you left — all return the same `404`, so none of them confirms that a conversation exists.

When adding an endpoint to that controller, call `MemberAsync` first. It is the entire security model for the feature.

## 3. The Social Layer

A connection is the gate on everything social: full profile, direct messages, and being added to a group. Nothing else decides who may talk to whom.

| Control | Behaviour |
|---|---|
| Discoverability | **Off by default.** A student is not in the directory until they opt in. |
| Name search | Matches names and school. **Never matches on or returns email.** |
| Email lookup | Exact match only, and rate-limited to 20 per hour per user, so the directory cannot be walked to test which addresses have accounts. |
| Profile visibility | Three levels — `None`, `Card`, `Full` — decided in one place, `ConnectionService`. Extra fields are added only at `Full`. |
| Blocking | Deliberately asymmetric: the blocker keeps a `Card` view so they can unblock; the blocked person gets `None` and cannot tell the difference between being blocked and the account not existing. |
| Group invitations | Everyone invited must already be connected to the inviter. Without this, a group is a way to put a message in front of somebody who never accepted you. |
| Group ownership | Exactly one owner, who cannot be removed and is the only one who can change roles. This is what stops a group reaching a state nobody can administer. |

## 3a. Reporting

Blocking answers "leave me alone". Reporting answers "somebody should look at this" — and a platform carrying private messages with no way to raise the second leaves a student being harassed with nowhere to go, and the operator with no record that they were ever told.

`POST /api/reports` is write-only. There is no endpoint that lists reports, because there is no administrator role, and adding one that any signed-in account could reach by guessing a URL would be worse than having none. Reports are read straight from the database by whoever runs PursuitHQ.

| Control | Behaviour |
|---|---|
| Message reports | The reporter must be an active member of that conversation — the same gate as everywhere else in messaging. A message id they were never shown looks exactly like one that does not exist. |
| Attribution | The reported student must actually be the sender of the reported message, so a report cannot attach somebody else's words to their name. |
| Evidence | The message text is copied at report time. A message can be edited or deleted seconds later, and "look at message 4821" is useless if 4821 now says nothing. This is the only place PursuitHQ copies message content, and only when a student asks for it to be looked at. |
| Abuse of reporting | Capped at 20 per reporter per day. The report button is itself something that can be used to harass somebody. |
| Leakage | The response says nothing about the other account — not whether they have been reported before, not what happens next. Otherwise the button becomes a way of probing somebody. |
| Retention | `Report` has no foreign keys, only ids. A safety record should not vanish in a cascade when an account is deleted, and equally must not be the thing that blocks somebody from deleting their account. No copy of anyone's name is kept. |

## 4. File Uploads & Attachments

Uploads are the highest-risk surface in the app.

| Control | Implementation |
|---|---|
| Size limit | 15 MB per chat attachment, 25 MB for study materials, refused before the body is read |
| Count limit | 10 files per message; 60 MB per upload request |
| Per-user quota | 1 GB total for study materials |
| Type gate | **Extension allow-list.** The client's content type is a claim, not a fact, and is never the deciding factor. |
| Path traversal | Client file names are never used on disk; storage names are GUID-based |
| Static serving | Storage lives outside `wwwroot`; downloads only through an authorized endpoint |
| Download headers | Non-images always serve as `Content-Disposition: attachment` |
| Virus scanning | Not built |

**The re-encode rule.** Every uploaded image is decoded and re-encoded server-side. This does two jobs: it strips the EXIF block, so a phone photo cannot carry the GPS coordinates of where it was taken into a group of people who were not there; and surviving the decode is the *only* thing that proves a file is really an image.

`MessageAttachment.IsImage` is set by the server, only for files it re-encoded itself, and it is the only flag that allows something to render inline. Never set it from anything the client sent.

**Licensing note.** The re-encode is done by SixLabors.ImageSharp 4.x, which is dual-licensed: free for personal, educational and open-source use, **paid for commercial use**. It is not an optional dependency — it is what makes an uploaded image safe to display at all. While PursuitHQ is a student project this is fine. Before it becomes commercial, either buy a licence or swap to SkiaSharp (MIT), which would be a contained change inside `ProfilePhotoService`. Every build prints a warning about this.

## 5. API Security

| Control | State |
|---|---|
| HTTPS | `UseHttpsRedirection` always; HSTS in non-Development |
| Proxy awareness | Forwarded-headers middleware runs first, so the app sees the real scheme behind a TLS-terminating host |
| CORS | Read from `Cors:AllowedOrigins` (comma-separated or indexed). Falls back to `http://localhost:3000` only when nothing is configured. Never `AllowAnyOrigin` with credentials. |
| Error responses | `UseExceptionHandler("/error")` in production returns the standard error shape and never a stack trace. The developer exception page is Development-only. |
| Swagger | Development-only |
| Input validation | Data annotations on DTOs; invalid input returns 400 with field-level detail |
| SQL injection | Prevented by EF Core parameterisation; no string-concatenated SQL |
| AI rate limiting | **Built** — per-user daily cap via `AiUsageLimiter` (`Ai:RequestsPerUserPerDay`) |
| Email lookup limiting | **Built** — 20 per hour per user |
| General rate limiting | **Built** — 300 requests/minute, counted per signed-in user and per IP for anonymous requests |
| Auth rate limiting | **Built** — 20 attempts per 15 minutes per IP on login, registration, forgot-password and reset-password (`"auth"` policy) |

**Server-side fetching (SSRF).** `JobDescriptionFetcher` retrieves a URL the user supplied, which is a request your server makes on a stranger's instruction. It follows redirects manually and re-checks every hop against private, loopback and link-local address ranges, and caps the response size. Any future feature that fetches a user-supplied URL must do the same — checking only the first URL is no protection, because a redirect is the attack.

**Why the general limit is 300 and not 100.** Messaging polls. One user with one chat open makes roughly 24 requests a minute without doing anything unusual, and four tabs is near a hundred (see `docs/Architecture.md`). A limit tuned for a normal REST app would spend its time rejecting ordinary use — and a limit that fires on ordinary use gets raised until it means nothing. Lower it once SignalR replaces the polling.

Identity's 5-attempt lockout protects *one account* from a guessing attack. The `"auth"` policy protects *every account at once* from somebody working through a list, which is why it partitions by IP rather than by user.

## 6. Secrets Management

| Environment | Where secrets live |
|---|---|
| Local development | .NET user-secrets (`dotnet user-secrets set ...`) — outside the repo |
| Production | The host's environment variables |
| Never | `appsettings.json`, source control, frontend code, screenshots, chat messages |

Secrets in use: `ConnectionStrings:DefaultConnection`, `Jwt:Key`, `Ai:ApiKey`, `Email:ApiKey`.

`Jwt:Key` must be at least 32 characters and should be generated randomly (`openssl rand -base64 48`). Production must not reuse the development key.

Anything prefixed `NEXT_PUBLIC_` in the frontend is visible to every visitor. `NEXT_PUBLIC_API_URL` is correct there; a key never would be.

If a secret is ever committed, **rotate it first**. Removing the commit does not help — assume it was read.

## 7. Privacy & Data Handling

- A student's materials, notes, resumes, goals and conversations are visible only to them and, for conversations, to the other members.
- **Data export** returns the user's records as JSON on request.
- AI prompts contain only the requesting user's own data. No other user's data ever enters a prompt.
- The AI API key lives only in server configuration. The frontend never calls an AI provider directly; doing so would hand the key to every visitor.

**AI provider data handling.** PursuitHQ uses Google Gemini. On Gemini's **free** tier, Google uses submitted content to improve their products; paid tiers do not. Because study tools and resume review submit students' notes, coursework and resumes, this must be disclosed in the privacy policy before anyone else uses those features — or the app must move to a paid tier first. This is a launch blocker, not a nice-to-have.

**What messaging discloses to other people.** Two behaviours reveal something about you to others and neither can currently be switched off:

- **Read receipts.** Other members learn when you opened a conversation, because the app records how far you have read and reports it.
- **Typing indicators.** Presence data leaves your machine while you type.

Most messaging apps make read receipts a setting, with the fair trade that turning yours off also stops you seeing theirs. Until that exists, both belong in the privacy policy.

**Email content policy — a deliberate decision, easy to undo by accident.** Notification emails name the sender and the group and **never quote the message**. Request emails name the requester and never include the note they wrote.

The reasoning: an email hands content to a third-party mail provider, leaves it sitting in an inbox that may be read on a shared screen, and survives after the sender deletes the message — which would make "delete for everyone" a lie. If you ever add message text to these emails, make it an explicit opt-in setting and say so here.

**Published policies.** `/privacy` and `/terms` exist, are reachable without an account (linked from the sign-in page), and are linked from the checkbox on the registration form — at the point the data is actually collected, which is the part that matters. Both were drafted from what the code does and **both still need review by somebody qualified before real users rely on them**. The contact address in them is a placeholder (`support@pursuit-hq.com`) and must be replaced with a monitored mailbox.

The privacy policy states the Gemini free-tier training clause plainly, in its own section, because that is the single most significant thing PursuitHQ does with somebody else's data.

## 8. Known Gaps

Ordered by how much they matter. These are real, current, and none of them is secretly handled somewhere else.

1. **JWT in `localStorage`.** §1. The top item on the deployment list, and the only one on this list that is a genuine hole rather than a rough edge.
2. **No automated tests.** There is no test project in the solution. The highest-value first test is conversation isolation: that user A cannot read user B's messages.
3. **No error monitoring.** In production a 500 is invisible unless somebody reports it.
4. **No virus scanning on uploads.**
5. **Read receipts and typing cannot be switched off.** §7.
5a. **No data export.** Deletion works; `FR-9` also promises export and that half does not exist. A request would have to be handled by hand.
6. **Rate limiting is in-memory**, so limits are per-instance and reset on restart. The same is true of `AiUsageLimiter`. Fine on one instance; both would need a shared store behind more than one.
7. **Attachment orphans.** If a file fails to decode partway through a multi-file upload, files already stored are left behind with no message referencing them.

Closed on 16 September 2026: general and auth rate limiting, account deletion leaving files behind, and file storage being local-disk-only.

## 9. Pre-Launch Security Checklist

Checked items are verified in the code today. Unchecked items are genuinely not done.

- [x] HTTPS redirection; HSTS outside Development
- [x] Forwarded headers handled, so HTTPS detection works behind a proxy
- [x] CORS read from configuration rather than hardcoded
- [x] Production exception handler; no stack traces; Swagger Development-only
- [x] Ownership filtering on every query; unowned records return 404
- [x] Membership gate on every messaging endpoint
- [x] Upload size, count and extension allow-list enforced
- [x] Images re-encoded server-side; `IsImage` set only by the server
- [x] SSRF guard on server-side URL fetching, re-checked per redirect hop
- [x] Two-step verification with a separate token audience
- [x] Email lookup rate-limited; discoverability off by default
- [x] AI key server-side only; per-user daily AI cap
- [x] General rate limiting (300/min) and auth rate limiting (20 per 15 min per IP)
- [x] Account deletion removes stored files as well as rows
- [ ] **JWT moved out of `localStorage` into an httpOnly cookie**
- [ ] **`ASPNETCORE_ENVIRONMENT=Production` confirmed on the host** (gates Swagger and the `forgot-password` disclosure)
- [ ] **`Jwt__Audience` set to `PursuitHQClient`, distinct from the 2FA audience**
- [ ] **Strong `Jwt__Key` from the environment, not the development value**
- [ ] No secrets anywhere in git history
- [ ] Ownership and conversation-isolation covered by tests
- [ ] `FileStorage__Provider=S3` set on the host, so files survive a restart
- [ ] Database backups configured, and one restore actually rehearsed
- [ ] Error monitoring live
- [ ] `dotnet list package --vulnerable` and `npm audit` clean
- [ ] AI data handling disclosed in the privacy policy, or a paid tier in use
- [ ] Read receipts disclosed in the privacy policy
- [ ] ImageSharp licence resolved if the project becomes commercial

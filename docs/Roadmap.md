# PursuitHQ — Roadmap

Phases are ordered by dependency: authentication comes first because nearly every entity has a `UserId` foreign key pointing at it. Each phase should end with working, tested, committed code.

## Phase 0 — Setup ✅

- [x] Install .NET 10, Node.js, PostgreSQL, pgAdmin, Visual Studio, VS Code
- [x] Create solution and `PursuitHQ.API` Web API project
- [x] Swagger UI working at `/swagger`
- [x] Git repository initialized, `.gitignore` in place, pushed to GitHub
- [x] Documentation set written (this `docs` folder)

## Phase 1 — Data foundation ✅

- [x] Install `Npgsql.EntityFrameworkCore.PostgreSQL` and EF Core tooling
- [x] Create `ApplicationDbContext` and connect to local PostgreSQL
- [x] Create every entity class from `DatabaseDesign.md`
- [x] First migration and `database update`
- [x] Verify tables in pgAdmin — 30 tables live

## Phase 2 — Authentication

- [x] Add ASP.NET Core Identity with `ApplicationUser`
- [x] JWT issuing and validation configured in `Program.cs`
- [x] `AuthController`: register, login, me, update profile, change password, delete account
- [x] `[Authorize]` on protected endpoints; Swagger configured to send the bearer token
- [x] Registration verified end to end — hashed password stored in PostgreSQL
- [x] Lockout policy wired (5 attempts / 15 minutes)
- [x] Password reset flow — request a link, set a new password, lockout cleared
      on success. Identity tokens, one hour lifespan. The email really sends
      now — Phase 3b's queue and Resend carry it — and in Development the link
      is still logged and returned in the response, so a reset can be tested
      without a mailbox
- [x] Delete account also removing uploaded files — `DeleteMe` now collects the
      user's study materials, message attachments and profile photo and deletes
      them from storage *before* removing the Identity row, since deleting the
      row first would cascade away the only record of which files were theirs

**Note on Swashbuckle 10 + Microsoft.OpenApi 2.x:** the JWT Swagger config uses
`OpenApiSecuritySchemeReference` inside a document lambda, not the older
`OpenApiReference` pattern, and the namespace is `Microsoft.OpenApi` (not
`Microsoft.OpenApi.Models`). See `Program.cs` if this ever needs revisiting.

**Lockout now has an escape hatch.** A successful password reset clears
`LockoutEnd` and the failed-attempt count, because someone resetting their
password is very often someone who just locked themselves out guessing at it.
The pgAdmin unlock is no longer the only way back in.

**On telling callers whether an account exists:** `forgot-password` answers the
same way for a known and an unknown email in every environment except
Development, where it says plainly that there is no such account. A silent
success is indistinguishable from a broken endpoint while you are testing; in
production the same message would let anyone discover who has signed up, one
address at a time.

## Phase 2a — Two-step verification ✅

Codes from an authenticator app (TOTP), built on Identity's own authenticator
token provider rather than anything hand-rolled.

- [x] `POST /api/auth/2fa/setup` — returns the shared secret, grouped in fours
      for anyone typing it by hand, and the `otpauth://` URI every authenticator
      app understands
- [x] `POST /api/auth/2fa/enable` — turning it on requires a working code first,
      and answers with ten single-use recovery codes
- [x] Login stops short when 2FA is on: it returns `requiresTwoFactor` and a
      five-minute two-factor token instead of a JWT, and
      `POST /api/auth/2fa/verify` trades that for the real one
- [x] A recovery code is accepted anywhere a code is;
      `POST /api/auth/2fa/recovery-codes` issues a fresh ten
- [x] Disabling asks for the account password and resets the authenticator key
- [x] `GET /api/auth/2fa` reports whether it is on and how many recovery codes
      are left; the settings page suggests a new set at three or fewer
- [x] `TwoFactorSection` on the settings page: the QR code rendered in the
      browser from the URI, the secret underneath it for anyone who cannot scan,
      and the recovery codes shown once

**Why the half-signed-in state gets its own token.** A login that has passed the
password but not the code must not be handed a JWT — that is precisely what the
second step exists to withhold. It gets a separate short-lived token with its own
audience (`PursuitHQ2FA`), and `2fa/verify` is the only endpoint that will accept
it. Nothing else in the API takes it, so a stolen half-token buys five minutes of
being allowed to submit a code, and nothing more.

**Recovery codes are shown once and never re-read.** `2fa/enable` returns them in
its response and no endpoint will show them again; the only way back is to
regenerate the set, which invalidates the old one. How many are left is the one
thing the status endpoint can still answer.

## Phase 3 — Academic planner ✅

- [x] `CoursesController` with full CRUD
- [x] `ClassSchedule` endpoints nested under courses
- [x] `AssignmentsController` with filters (course, status, due before/after)
- [x] `PATCH /api/assignments/{id}/status` — flips only the status, so a caller
      holding a partial record cannot overwrite other fields with stale values
- [x] Courses carry a first and last day of class, bounding how far weekly class
      times expand onto the calendar
- [x] Ownership checks verified on every endpoint
- [x] `ApiControllerBase` established: `[Authorize]` by default, `CurrentUserId`
      read from the JWT, 404 (not 403) for records the caller does not own

## Phase 3a — Calendar ✅

- [x] `CalendarEventsController` — CRUD for non-class activities
- [x] `CalendarController` — `GET /api/calendar?from=&to=` merging four sources
      into one sorted list: class meetings, assignment due dates, study sessions,
      and student-created events
- [x] Weekly recurrence (`FREQ=WEEKLY;BYDAY=...`), all-day events, multi-day spans
- [x] Per-source error isolation: one failing query names itself in the response
      instead of taking down the whole calendar
- [x] Month, week, and day views; the week and day views share one hour grid
- [x] Overlapping events laid out side by side; a live current-time line
- [x] Click any empty slot to open an event dialog pre-filled with that time
- [x] Assignments appear in the all-day strip with a working checkbox
- [x] Clicking a class opens that course's materials
- [x] Saved color palette per student (`/api/preferences/colors`), shared by the
      event dialog and the course form

**The decision that matters most in this phase: stored `DateTime` values are
wall-clock times, not instants.** Every `DateTime` column is PostgreSQL
`timestamp with time zone`, and Npgsql refuses to write a `DateTime` whose `Kind`
is `Unspecified` — which is exactly what a `datetime-local` input and
`DateOnly.ToDateTime()` both produce. That was the cause of the calendar
returning 500 on every request.

The fix is a single value converter in `ApplicationDbContext.ConfigureConventions`
that stamps `Kind` on the way in and strips it back to `Unspecified` on the way
out. Stripping it on the way out is the half that matters to the UI: a `Utc`
`DateTime` serializes with a trailing `Z`, the browser reads that as an instant
and shifts it into local time, and an assignment due at 11:59 PM lands on the
wrong day. Nothing converts a time in either direction — 9 AM is 9 AM.

**The one rough edge this leaves:** "overdue" compares a due date against
`DateTime.Now`, the server's clock. That is correct while the app runs on your
own laptop and wrong once it is deployed to a UTC server, at which point it needs
`ApplicationUser.TimeZone`. Noted in `Deployment.md`.

## Phase 3b — Email reminders ✅

- [x] `NotificationPreference` with defaults, and a settings page for every
      switch on it
- [x] `IEmailService` with Resend and a verified sending domain;
      `ConsoleEmailService` stands in when no API key is configured, so the whole
      flow still runs on a laptop
- [x] `IEmailQueue` and a hosted worker, so sending never happens on the request
      thread — a slow provider cannot slow down a save, and a queued send costs
      the same whether or not there was an account to send to
- [x] `NotificationService` with duplicate-safe sending via `Notification` records
- [x] Assignment reminders, event reminders, daily and weekly digests
      (`NotificationType`: `AssignmentDue`, `EventReminder`, `DailyDigest`,
      `WeeklyDigest`)
- [x] Optional confirmation emails when a course, assignment or event is created
      — off by default, because it is the only mail here not tied to a deadline
- [x] Time-zone-correct delivery: every student carries an IANA time zone, set at
      registration and changeable in settings, and `UserClock` is what turns
      server time into their local time
- [x] Assignment reminders take a **list** of offsets rather than one — up to
      four, between an hour and two weeks out, stored comma-separated and largest
      first, with parsing, bounds and ordering all in `ReminderOffsets`
- [ ] Secured `/api/jobs/run` endpoint + external cron — deferred to deployment

**Not an external cron, for now.** `ReminderBackgroundService` runs the pass
in-process every five minutes, because there is no server yet and this has to
work on a laptop. `POST /api/notifications/run` forces a pass immediately and is
a 404 outside Development on purpose — it sends to everyone who is due
something, not just the caller. At deployment the same `INotificationService`
gets a secured endpoint and a hosted cron in front of it, and none of the
reminder logic moves.

The cost of running in-process is that reminders only go out while the API is up.
That is what the grace period in `NotificationService` is there to absorb.

**The offsets are a string, deliberately.** `AssignmentReminderHours` is
`"168,24"`, not a table. It is written whole, read whole, and never queried
across; a table would be a migration, an entity and a join to hold what fits in a
column. `ReminderOffsets.Parse` is forgiving on the way out — a row written by an
older version costs one student one odd setting rather than throwing and taking
down the reminder run for everybody.

## Phase 4 — Study materials ✅

- [x] `IFileStorageService` + `LocalFileStorageService`
- [x] Folder CRUD with nesting, plus loop prevention when moving a folder
- [x] Upload endpoint with size, type, and quota validation before anything
      touches disk
- [x] Authorized download streaming with the original file name
- [x] Notes CRUD
- [x] Folder delete removes descendants and their stored files, not just rows
- [x] Storage usage endpoint (`GET /api/storage/usage`)
- [x] Frontend page at `/courses/[id]/materials`: breadcrumb folder
      navigation, multi-file upload, download, notes editor, storage bar

**Security decisions in this phase:** files are stored with GUID names outside
`wwwroot`, so a client file name can never reach the filesystem and nothing is
served statically. Downloads go through an authorized endpoint with
`Content-Disposition: attachment`. Extension *and* MIME type must both be on the
allow-list.

**Frontend notes:** uploads bypass the shared API helper because `FormData`
requires the browser to set its own `Content-Type` boundary. Downloads fetch
with the bearer token, then trigger a save via a temporary blob URL, since a
plain link cannot send an Authorization header.

**Added beyond the original scope:**

- [x] Drag-and-drop upload, with a drop overlay
- [x] Drag a file onto a folder row to move it; a Move dropdown as the
      keyboard-friendly alternative
- [x] Inline preview: images and PDFs render from a blob URL, text files show
      their content
- [x] Course-wide search across files and notes. Searching deliberately ignores
      the current folder — the whole point is finding something whose folder you
      have forgotten. Debounced 300ms
- [x] `ITextExtractionService` (PDF via PdfPig, DOCX/PPTX/XLSX via the Open XML
      SDK) powering a text preview for formats no browser can render. PowerPoint
      extraction includes speaker notes

## Phase 5 — Internship & job tracker — **removed**

Built, then removed by decision in September 2026. The tracker worked, but the
search half never did: every Adzuna posting that looked promising turned out to
be closed by the time you clicked through, and the alternatives were worse.
Chasing a job board that stays accurate was pulling time away from the study
features, which are the reason the app exists.

**What was deleted:** `ApplicationsController`, `JobSearchController`,
`AdzunaJobSearchService`, `AtsJobSearchService`, `IJobSearchService`,
`JobSearchOptions`, `CompanyBoardOptions`, the `Applications` and `JobSearch`
DTO folders, and the `/jobs` and `/applications` pages.

**What was kept on purpose:** the `JobApplication` entity and its `DbSet`. It
costs nothing to leave the table in place, no migration was needed to remove the
feature, and none will be needed to bring it back.

**Worth remembering before rebuilding it:** the problem was never the tracker. If
this comes back, it should be a tracker you type into yourself, with search added
only if a source proves it keeps its listings current.

## Phase 6 — Frontend foundation ✅

Built ahead of Phases 4 and 5 so the app could be seen and clicked sooner.
Lives at `Frontend/pursuithq-web` (lowercase folder name required by npm).
Next.js 16 · React 19 · Tailwind v4 · App Router · JavaScript.

- [x] `create-next-app` scaffolded
- [x] Tailwind configured, root layout and top navigation
- [x] Login and register pages
- [x] `AuthProvider` holding session state, redirecting unauthenticated users
- [x] `lib/api.js` API client attaching the bearer token to every request
- [x] Dashboard with stat cards, upcoming assignments, and course list
- [x] Courses page: create, edit, delete, meeting times, term dates, colors
- [x] Assignments page: create, delete, filter, checkbox and status cycling
- [x] Study materials page — folders, uploads, downloads, notes
- [x] Calendar page — month, week, and day views
- [x] Shared `ColorPicker` and the saved-colors palette
- [x] Study section: per-course tutor, sessions, guides, flashcards, tests
- [x] Settings page: appearance, profile, time zone, email notifications,
      two-step verification, change password, delete account
- [x] Dark mode
- [x] Notification preferences — every switch on `NotificationPreference`,
      including the assignment reminder offsets and the digest days and times
- [x] `TimeZoneNotice`: when the browser's zone disagrees with the saved one it
      offers to change it, through `PUT /api/auth/me/timezone` — an endpoint that
      writes only the zone, because the full profile update would blank the
      student's name and major on its way past

**Known hardening item:** the JWT is kept in `localStorage`, which is readable
by any script on the page. Move to an httpOnly cookie before other students
use PursuitHQ. See `Security.md`.

**A bug worth remembering:** `body ? JSON.stringify(body) : undefined` in the API
client silently dropped a body of `0`, because `0` is falsy. Truthiness checks
and valid zero values do not mix — the check is `body !== undefined`.

**How dark mode works, and its limit.** Every page was written with literal
colors — `bg-white`, `text-slate-900` — from one small palette, so `globals.css`
remaps that palette when `.dark` is on `<html>` rather than adding a `dark:`
variant to several hundred class names across fourteen files. This assumes
`bg-white` always means "a surface" and `text-slate-900` always means "primary
text", which is true today. **A page that ever needs something to stay white in
dark mode cannot use this mechanism** and needs its own explicit colors. The
trade-off is written into the top of `globals.css` so it is not rediscovered.

An inline script in `layout.js` applies the saved theme before the first paint.
React cannot: its first render happens after the browser has drawn, so a
dark-mode user would see a white flash on every page load.

**Running it locally needs two terminals:**
`dotnet run` in `PursuitHQ.API` (port 5051) and `npm run dev` in
`Frontend/pursuithq-web` (port 3000).

## Phase 7 — Dashboard ✅

- [x] Aggregated `/api/dashboard` endpoint, so the page makes one request
      rather than six
- [x] Dashboard page leading with today, then the week: today's classes, what is
      due, recent decks and test scores
- [x] Assignments tickable from the dashboard itself

The calendar and the dashboard read the same day through one shared
`ICalendarFeedService`, so the two pages cannot disagree about what is on it.

## Phase 8 — Career growth — **still the gap**

The last pillar of the original idea with no code behind it. `Goal`, `Skill` and
`Certification` are entities and tables already — there is no controller, no
service and no page for any of them. Same shape as courses and assignments;
build one entity end to end before starting the next.

- [ ] Goals, skills, and certifications endpoints and pages

It is the largest missing *feature*, but not the most urgent work — see "What is
actually next" near the end of this file for what the built half is waiting on.

## Phase 9 — AI features ✅ (study tools)

- [x] Gemini API key from Google AI Studio; stored in user-secrets locally
- [x] `IAiService` + `GeminiAiService` with a typed HttpClient and a 90-second timeout
- [x] `ITextExtractionService` for PDF, DOCX, PPTX — **built early in Phase 4** for
      file previews, deliberately, so it would be ready to reuse here
- [x] Structured output with validation; malformed items are dropped, not shown
- [x] Retry and rate-limit handling, surfaced as sentences a student can act on
- [x] Per-user daily cap (`IAiUsageLimiter`, `Ai:RequestsPerUserPerDay`)
- [x] `StudyToolAiService`: flashcards and practice tests
- [x] `StudyChatService`: a per-course tutor that answers from attached material
      and writes study guides on request
- [x] Flashcard review with self-grading and per-card counters
- [x] Practice tests taken in the app, with AI grading for written answers
- [x] Truncation for large documents (40k characters of source per request)
- [x] Graceful failure: no partial decks, no unsaved-but-shown guides
- [ ] `StudyPlannerAiService` and study session endpoints
- [x] `ResumeAiService`: import from a file, a written review, and the resume
      editor at `/resume` — split view, printable preview, rearrangeable
      sections, and free-typed sections with bold and bullets. Alongside
      education, experience, projects and skills there is an **extracurricular**
      section, which for a student is usually where the leadership evidence
      actually is. The format check is deterministic C#, so it still runs when
      the AI allowance is gone

**How the three tools reach the student**

| Tool | Made from | Where |
|---|---|---|
| Flashcards | one file or note | `/courses/{id}/flashcards`, reviewed at `/decks/{id}` |
| Practice tests | one file or note, or asked for in chat | `/courses/{id}/tests`, taken at `/tests/{id}` |
| Study guides | asked for in chat | saved to `/courses/{id}/guides` |

**Decisions worth remembering**

*Artifacts are wrapped in text markers, not JSON.* A study guide is long markdown
full of quotes and newlines, and asking a model to escape all of that inside a
JSON string is exactly where these things break. Finding two markers in a text
stream cannot fail the same way. Practice tests use JSON inside those markers,
because questions are short and structure matters more there.

*Nothing is saved unless it is usable and wanted.* A deck with no valid cards is
not saved at all — a deck of three broken cards is worse than none, because it
looks finished. A study guide stays in the conversation until the student presses
Save, so the library only holds things that were deliberately kept.

*Grading never guesses in the student's favour.* An answer the model returns no
verdict for is marked wrong with an explanation. If grading fails outright, or the
daily allowance is gone, the test is still scored and returned with those answers
flagged — rather than throwing the submission away.

*Question types are sent as a sentence, not flags.* The UI toggles build a phrase
like "Use only these question types: multiple choice and free response", and a
free-text box sits beside it. A model reads "mostly multiple choice with two
written ones at the end" perfectly well; a dropdown never could.

*429 is retried, 500 is retried, and the difference matters.* The free tier limits
requests per **minute**, and Gemini reports how long to wait — so a 429 now costs
about 18 seconds instead of a failure. An earlier version excluded 429 on the
theory that a quota will not clear in seconds; that was wrong. Waits longer than
30 seconds still fail fast, because those are real daily quotas.

*Grounding was tried and abandoned.* Gemini with Google Search grounding is **not
available on the free tier at all** — it needs billing enabled. Plain completions
are what the study tools use.

**Billing is now enabled** with $10 of credit, which lifted the 20-requests-per-
minute ceiling. Roughly a penny per request; see `START-HERE.md` §5a.

## Phase 9a — Internship & job search — **removed**

The *search* half went with Phase 5. See that section for what was deleted and
why.

**What survived is the matcher.** `POST /api/resumes/{id}/match` scores one
resume against one posting — pasted in, or fetched from a link by
`IJobDescriptionFetcher` — pulling the posting's requirements apart and saying
with evidence which ones the resume answers. `SavedJobsController` keeps the ones
worth returning to, listed at `/resume/jobs`, storing the posting text alongside
the link because a link to a closed role is dead within weeks and a score with
nothing left to measure against can neither be explained nor re-run.

What went was the job *board*. Nothing in PursuitHQ goes looking for postings any
more; the student brings the posting.

## Phase 10 — Analytics & polish

- [ ] Analytics endpoints and charts
- [ ] Accessibility and mobile pass
- [ ] Study sessions get a UI — they already render on the calendar and are
      already a `DbSet`, but nothing in the app can create one yet. (The pages
      under `/courses/{id}/sessions` are study *chat* sessions, which is a
      different thing wearing the same word.)

## Phase 11 — Students & connections ✅

Built in September 2026, out of order: Phases 8 and 10 are still open. This is
the first part of PursuitHQ that shows one student anything belonging to another,
which is why it starts with who may see whom rather than with a feature.

- [x] `IsDiscoverable` on `ApplicationUser`, **off by default** — nobody is
      enrolled in a directory by signing up for a planner
- [x] `GET /api/students/search` — name search, two-character minimum, 20
      results, and only across students who opted in
- [x] `GET /api/students/lookup?email=` — exact address only, never partial, and
      capped at 20 lookups per student per hour
- [x] `StudentCardDto` everywhere, never the full profile; the email appears on
      it only once two students are connected
- [x] School, education level, major and graduation year as directory fields
- [x] `Connection` — `Pending → Accepted / Declined`, plus `Blocked` as a status
      on the same row, so "what is the state between these two people" is always
      one lookup with one answer
- [x] A short note can ride along with a request — "we met in CS 201" — and it is
      the only text a stranger can put in front of someone who has not accepted
      them
- [x] Blocking, one-sided, undoable only by whoever did it
- [x] `IConnectionService` as the single place that looks a pair up both ways round
- [x] Profile photos: `POST /api/profile/photo`, re-encoded server-side, served
      through an endpoint that checks who is asking rather than from a public
      folder
- [x] `/students` — search, incoming and outgoing requests, connections;
      `/students/{id}` for one profile

**Why direction is a service and not a query.** A connection has a direction
while it is pending — somebody asked, somebody has to answer — and none once it
is accepted, so every lookup has to consider the pair in both orders. Spreading
that across controllers is how a blocked person ends up still able to message.
`ConnectionService` is the only thing that does it.

**Why photos are re-encoded rather than stored as uploaded.** `ProfilePhotoService`
decodes with ImageSharp and writes a fresh JPEG, which does three jobs at once:
it strips EXIF, so a picture does not carry the GPS coordinates of where it was
taken to people the student has never met; it proves the file really is an image,
because a `.jpg` extension and an image content type are claims rather than
facts; and it bounds the size — 512px square for an avatar, 1600px for a picture
shared in a chat.

## Phase 11a — Direct messages ✅

- [x] `Conversation`, `ConversationMember` and `Message`, with `LastMessageAt`
      denormalised onto the conversation so the list can be ordered and paged
      without touching the messages table
- [x] `POST /api/conversations/direct` — opens the existing chat if there is one
- [x] `GET /api/conversations/{id}/messages`, 50 at a time
- [x] Replies (`ReplyToMessageId`), edits (`EditedAt`, and the UI says so), soft
      deletes (`DeletedAt`, body cleared, row kept)
- [x] Read state per member (`LastReadAt`); unread counts at
      `GET /api/conversations/unread`
- [x] Mark a chat unread again (`POST /{id}/unread`)
- [x] Mute and pin, both per member rather than per conversation
- [x] Typing indicators and read receipts, both over `GET /{id}/presence`
- [x] `/messages` — conversation list, thread, composer, day separators

**One helper is the entire security model.** Every endpoint here begins by
proving the caller is an *active* member, through `MemberAsync`, and nothing
reads or writes a message without going through it. This is different in kind
from the rest of PursuitHQ: elsewhere a missing `UserId` filter shows you an
error, here it shows you two other people's conversation.

**Polling, not push. There is no SignalR in this project.** The open thread
re-fetches every 5 seconds, presence every 2.5, the conversation list every 15,
and the unread badge every 30 — and polling pauses while the tab is hidden, or a
laptop left open overnight would make a thousand pointless requests. Everything
that shows a badge reads it from `useUnread`, which is the seam SignalR slots
into when it is built. It is in "What is actually next" below, not here.

**Typing is a timestamp, not a flag.** A boolean stays true forever the moment
somebody closes the tab mid-word, and every reader is told they are still typing.
`LastTypingAt` expires on its own after six seconds — comfortably longer than the
client's ping interval, so a steady typist never flickers.

**Marking unread backdates rather than clearing.** `LastReadAt` is set to a
second before the newest message somebody else sent, not to null: null means
"never opened", which would mark the whole history unread instead of the one
thing you wanted to come back to.

**Pinning is ordered by when it was pinned**, earliest first, so a new pin lands
underneath the ones already there. Ordering pinned chats by their last message
would let any of them jump the queue the moment somebody typed, which makes a
deliberately arranged list rearrange itself behind your back.

## Phase 11b — Group chats ✅

- [x] `POST /api/conversations/group`, up to 100 members
- [x] `ConversationRole` — Member, Admin, Owner — numbered and ordered so a
      numeric comparison works: anything above Member can invite and remove, and
      only the Owner can change roles
- [x] Exactly one Owner per group, who cannot be removed by anybody
- [x] Invitations folded into membership as `MembershipStatus.Invited`, so the
      unique index on (ConversationId, UserId) still means something and nobody
      can hold an invitation and a membership at the same time
- [x] `GET /api/conversations/invitations`, with accept and decline
- [x] Invite, remove a member, change a role, edit name and description
- [x] Group photos, through the same re-encoding pipeline as profile photos
- [x] System messages — "Sarah added Marcus", "Marcus left" — stored as messages
      with `MessageKind.System` rather than derived, because they belong in the
      timeline in the order they happened
- [x] Leaving keeps the row (`Status = Left`, `LeftAt`) so the history still reads
- [x] `GroupSettings` on the frontend: members, roles, invitations, photo

**Why one unremovable Owner.** Two admins removing each other is a nuisance; a
group nobody can administer is a dead room nobody can leave tidily. A single role
that cannot be taken away is what stops the second state existing.

## Phase 11c — Reactions, attachments, search ✅

- [x] `MessageReaction` — one row per person per emoji, unique on
      (MessageId, UserId, Emoji), toggled by a single endpoint
- [x] `MessageAttachment` — up to 10 files on one message, 15MB each, 60MB for
      the whole request, GUID storage keys, downloads through an authorized
      endpoint
- [x] Images (`.jpg .jpeg .png .webp .gif`) re-encoded server-side and only then
      marked `IsImage`; documents stored as sent and always served as attachments
- [x] `GET /api/conversations/search` across every conversation the student is a
      member of, scoped by membership inside the query rather than filtered after
- [x] The same box filters the chat list by name, client side, because those
      names are already in the browser
- [x] `EmojiPicker`, `ContextMenu` and inline previews on the messages page

**Why a row per reaction and not a count.** The question a chat actually asks is
"did I already react, and who else did" — a count answers neither. The unique
index is what makes the toggle safe when somebody double-clicks.

**Why attachments are a table.** One message can carry several, and nearly every
message carries none; columns on `Message` would make every text message pay for
six unused ones.

**A bug worth remembering.** The poll compared only the *last* message to decide
whether anything had changed, so reacting to, editing or deleting anything above
it read as "nothing new" and was thrown away — reactions appeared to do nothing
at all unless you happened to pick the newest message. The fix is a cheap
fingerprint over the whole page, reactions included.

## Phase 11d — Email for messages and requests ✅

- [x] `IMessageNotifier` — emails the other members after a message is safely
      saved, and never throws, because a mail provider having a bad minute must
      not turn a sent message into a 500
- [x] At most one email per conversation per person per 15 minutes
      (`LastMessageEmailAt`), and none at all to somebody whose last read was
      within 90 seconds — they have the thread open in front of them
- [x] `IRequestNotifier` — connection requests and group invitations, with no
      throttle, because each is a single event that needs a decision
- [x] `MessageEmailsEnabled` and `RequestEmailsEnabled` on
      `NotificationPreference`, both on by default, both switchable in settings

**The email names the sender and the group, and nothing else.** Putting the
message text in it hands what two students said to each other to a mail provider,
leaves it sitting in an inbox that may be read over somebody's shoulder, and
makes "delete for everyone" a lie. The same goes for the note written with a
connection request: it stays in the app.

## Phase 12 — Deployment (final phase)

Deliberately last: build and test everything locally first, then ship it once.

- [ ] Neon PostgreSQL project created; production connection string set
- [ ] Cloud file storage configured (Cloudflare R2 or similar)
- [ ] API deployed to Render; all environment variables set
- [ ] Frontend deployed to Vercel; `NEXT_PUBLIC_API_URL` pointed at the live API
- [ ] CORS updated from `localhost:3000` to the real Vercel origin
- [ ] Migrations applied to the production database
- [ ] JWT stored in an httpOnly cookie rather than `localStorage`
- [ ] "Overdue" switched from `DateTime.Now` to the student's saved time zone
- [ ] Complete the security checklist in `Security.md`
- [ ] Privacy policy covering AI data handling, if AI features are live
- [ ] Backups enabled; a restore rehearsed once
- [ ] End-to-end smoke test against the live URL
- [ ] **Milestone: another student can register and use PursuitHQ**

Full instructions in `Deployment.md`. Expect environment problems rather than
code problems: connection strings, CORS, HTTPS, and environment variables are
where the time goes.

## What is actually next

Not a wish list. These are the things the code as it stands is waiting on,
roughly in the order of how much it would hurt to leave them.

- [x] **Files off the local disk.** `S3FileStorageService` implements the
      existing `IFileStorageService` against Cloudflare R2. The provider is
      chosen by configuration and falls back to local disk when a credential is
      missing, so a half-configured bucket degrades instead of refusing to
      start — check the startup log line that names which was chosen. Stored
      keys are identical between the two implementations, so anything already
      on disk only needs its bytes copied.
- [ ] **The JWT out of `localStorage` and into an httpOnly cookie.** Any script
      that gets onto the page can read the token today. It touches `lib/api.js`,
      the auth setup in `Program.cs` and CORS all at once, which is why it has
      not happened — not because it is optional. See `Security.md`.
- [ ] **Rate limiting.** Two limits exist today: 20 email lookups per student per
      hour, hand-rolled with `IMemoryCache`, and a per-user daily cap on the AI
      endpoints (`AiUsageLimiter`, used by the flashcard, quiz, resume and study
      controllers). Login, registration, password reset and message sending have
      none. ASP.NET Core's built-in rate limiter, applied by policy.

      Pick the number *after* reducing the polling below. One user with one chat
      open already makes roughly 24 requests a minute legitimately, so a naive
      "100 per minute" would start rejecting somebody with four tabs open.
- [ ] **SignalR in place of polling.** Four timers currently stand in for push —
      three on the messages page (messages every 5s, presence every 2.5s, the
      conversation list every 15s) and one in `useUnread` (30s). Every badge
      already reads from that hook, so the swap is a handful of files rather than
      every component that shows a number.
- [ ] **An automated test suite, starting with conversation isolation.** There is
      none at all (see below). The first tests to write are the ones that prove a
      non-member cannot read, send, react, edit, delete, invite, or download an
      attachment in a conversation — every endpoint on `ConversationsController`,
      one test each. That is the one place in the app where a missing check shows
      somebody else's data instead of an error, so it is the one place where
      "verified by hand once" is not good enough.
- [ ] **Error monitoring.** Nothing reports a 500 from a deployed API. Today the
      only record of a failure is a line in a terminal on a laptop, which stops
      existing the moment there is no laptop.

Cloud storage and the cookie also appear in Phase 12, because they are hard
blockers for deploying. They are listed here as the work and there as the gate.

## Definition of Done (every phase)

1. Endpoints work and are documented in Swagger
2. Ownership enforced and verified
3. Validation errors return the standard error shape
4. Frontend handles loading, empty, and error states
5. Code committed to a feature branch and merged via pull request
6. `docs` updated if the design changed

**"Tested", in the line at the top of this file and in point 2, is an aspiration
rather than a description of practice.** There is no test project in the solution
— `PursuitHQ.slnx` holds `PursuitHQ.API` and nothing else — and no automated test
of any kind. Every phase so far has been verified by clicking through Swagger and
the UI. The aspiration stays because it is where the project should get to; the
first concrete step toward it is in "What is actually next" above.

## A habit worth keeping

When a change touches both C# and the database, run the three steps separately
and read the output of each before moving on:

```powershell
dotnet build                                  # does it compile?
dotnet ef migrations add <Name>               # generate the migration
dotnet ef database update                     # apply it
```

Stacking them into one paste hides which step failed. `dotnet ef` builds the
project first, so a compile error shows up as a confusing migration failure —
and a migration that was never created means the API silently keeps running
yesterday's code.

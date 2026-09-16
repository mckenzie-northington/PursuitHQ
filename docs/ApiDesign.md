# PursuitHQ — API Design

## Conventions

**Base URL:** `/api`
**Auth:** every endpoint except those marked *(public)* requires `Authorization: Bearer <jwt>`.
**Ownership:** `UserId` is always taken from the token, never from the request body. Any request for a record the caller does not own returns `404` (not `403`, so record existence isn't leaked).

### Status codes

| Code | Used for |
|---|---|
| 200 | Successful GET / PUT |
| 201 | Successful POST, with `Location` header |
| 204 | Successful DELETE |
| 400 | Validation failure |
| 401 | Missing or invalid token |
| 403 | Authenticated, but the relationship does not allow it (messaging somebody you are not connected to, removing a member you outrank) |
| 404 | Not found, or not owned by caller |
| 409 | Conflict (e.g. duplicate email, duplicate connection request) |
| 413 | Upload exceeds size limit |
| 415 | Unsupported file type |
| 422 | File could not be processed (e.g. a scanned PDF with no extractable text) |
| 423 | Locked — five failed sign-in attempts in 15 minutes |
| 429 | Rate limit exceeded (AI endpoints, email lookup) |
| 500 | Unhandled server error |
| 502 | The AI provider failed or returned something unusable |
| 503 | AI is not configured on this server |

Not every code in that table is reachable from every endpoint, and a few
endpoints answer `400` where the table above would suggest `409` — the social
endpoints in particular return `400` with a named error (`AlreadyConnected`,
`AlreadyRequested`) rather than a `409`.

### Error response shape

Every error returns the same shape, which is `DTOs/ApiErrorDto.cs`:

```json
{
  "error": "ValidationFailed",
  "message": "Due date must be in the future.",
  "details": { "dueDate": ["Must be a future date."] }
}
```

`details` is optional and omitted on most errors. `error` is a short code the
frontend can branch on; `message` is written to be shown to a student as-is.

### Pagination

**There is no general pagination.** List endpoints return everything the caller
owns; a student's own data is small enough that paging it would be complexity
for its own sake. Two endpoints cap what they return instead:

- `GET /api/students/search` and `GET /api/conversations/search` return at most
  20 and 50 rows.
- `GET /api/conversations/{id}/messages` is paged by cursor — 50 at a time,
  oldest-first, with `?before=<messageId>` for the page before that. A cursor
  rather than an offset, because an offset shifts under you every time somebody
  sends a message while you are scrolling back.

---

## Auth — `/api/auth`

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/register` *(public)* | Create account. Body: firstName, lastName, email, password, timeZone?. Returns JWT. |
| POST | `/api/auth/login` *(public)* | Authenticate. Body: email, password. Returns JWT + user profile, or a two-step challenge (see below). Counts failed attempts; returns 423 Locked after 5 failures in 15 minutes. |
| POST | `/api/auth/2fa/verify` *(public)* | Finish a login that came back as a challenge. Body: twoFactorToken, code. |
| POST | `/api/auth/forgot-password` *(public)* | Send reset email. Body: email. Always returns 200 (no account enumeration), except in Development. |
| POST | `/api/auth/reset-password` *(public)* | Body: email, token, newPassword. |
| GET | `/api/auth/me` | Current user's profile. |
| PUT | `/api/auth/me` | Update profile (name, school, education level, major, graduation year, time zone, directory listing). |
| PUT | `/api/auth/me/timezone` | Set the time zone on its own. Body: timeZone (IANA id). Used by the browser's "your clock says something else" prompt. |
| POST | `/api/auth/change-password` | Body: currentPassword, newPassword. |
| DELETE | `/api/auth/me` | Delete account and all owned data and files. |

Profile updates and registration both reject a time zone this server cannot
resolve, with `UnknownTimeZone` — Windows and Linux do not agree on every id.

### Two-step verification

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/auth/2fa` | Whether it is on, and how many recovery codes are left |
| POST | `/api/auth/2fa/setup` | Returns the shared key and an `otpauth://` URI for the QR code. Turns nothing on |
| POST | `/api/auth/2fa/enable` | Body: code. Confirms the app is working, switches it on, returns the recovery codes |
| POST | `/api/auth/2fa/disable` | Body: password. Turning it off costs a password, so a borrowed session cannot |
| POST | `/api/auth/2fa/recovery-codes` | Body: password. Issues a fresh set and invalidates the old one |

When two-step is on, `POST /api/auth/login` returns `requiresTwoFactor: true`
and a short-lived `twoFactorToken` **instead of** the JWT. The second call is
`POST /api/auth/2fa/verify` with that token and a six-digit code. A recovery
code is accepted in the same field and is spent when it is used. The account
lockout guards this step as well as the password.

### Password reset

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/auth/forgot-password` | Start a reset. Answers the same way for a known and unknown email, except in Development |
| POST | `/api/auth/reset-password` | Check the token and set the new password. Clears any lockout on success |

Tokens are Identity's own, expire after an hour, and work once. The link is
emailed. With no `Email:ApiKey` configured the console email service prints it
to the API terminal instead, and in Development it also comes back on the
response as `developmentResetUrl` so a reset can be tested without a mailbox.

---

## Courses — `/api/courses`

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses` | List courses. Optional `?semester=Fall 2026`. |
| GET | `/api/courses/{id}` | One course, including its schedules. |
| POST | `/api/courses` | Create. Body: name, professor, semester, creditHours?, colorHex?. |
| PUT | `/api/courses/{id}` | Update. |
| DELETE | `/api/courses/{id}` | Delete course and its schedules, assignments, folders, materials, notes. |

### Class schedules — nested under a course

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/schedules` | Meeting times for a course. |
| POST | `/api/courses/{courseId}/schedules` | Body: dayOfWeek, startTime, endTime, location. |
| PUT | `/api/courses/{courseId}/schedules/{id}` | Update. |
| DELETE | `/api/courses/{courseId}/schedules/{id}` | Delete. |

---

## Assignments — `/api/assignments`

| Method | Route | Description |
|---|---|---|
| GET | `/api/assignments` | List. Filters: `?courseId=`, `?status=`, `?dueBefore=`, `?dueAfter=`. |
| GET | `/api/assignments/{id}` | One assignment. |
| POST | `/api/assignments` | Body: courseId, title, dueDate, status. |
| PUT | `/api/assignments/{id}` | Update, including status and grade. |
| PATCH | `/api/assignments/{id}/status` | Tick off or un-tick without sending the whole record. Body: status. |
| DELETE | `/api/assignments/{id}` | Delete. |

`isOverdue` is worked out against the **student's** wall clock, not the
server's — `IUserClock` looks up their time zone once per request. Due dates are
stored as wall-clock times, so "has this passed" is a wall-clock question.

---

## Study materials — `/api/courses/{courseId}/...`

### Folders

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/folders` | Folder tree for a course. Optional `?parentId=` for one level, `?all=true` for every folder flat. |
| POST | `/api/courses/{courseId}/folders` | Body: name, parentFolderId?. |
| PUT | `/api/courses/{courseId}/folders/{id}` | Rename or move (change parentFolderId). |
| DELETE | `/api/courses/{courseId}/folders/{id}` | Delete folder and everything inside it. |

### Files

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/materials` | List files. Optional `?folderId=`, `?search=`. |
| POST | `/api/courses/{courseId}/materials` | `multipart/form-data` upload. Fields: file, folderId?, description?. Validates type and size before writing to storage. |
| GET | `/api/courses/{courseId}/materials/{id}/download` | Streams the file with its original file name. |
| GET | `/api/courses/{courseId}/materials/{id}/text` | Extracted plain text, for the in-app preview of Word / PowerPoint / Excel / PDF. |
| PUT | `/api/courses/{courseId}/materials/{id}` | Rename, move between folders, edit description. |
| DELETE | `/api/courses/{courseId}/materials/{id}` | Delete the record and the stored file. |
| GET | `/api/storage/usage` | Bytes used, quota, and file count for the caller. Not nested under a course. |

### Notes

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/notes` | List notes. Optional `?folderId=`, `?search=`. |
| GET | `/api/courses/{courseId}/notes/{id}` | One note with full content. |
| POST | `/api/courses/{courseId}/notes` | Body: title, content, folderId?. |
| PUT | `/api/courses/{courseId}/notes/{id}` | Update title/content/folder. |
| DELETE | `/api/courses/{courseId}/notes/{id}` | Delete. |

---

## Calendar — `/api/calendar` and `/api/calendar-events`

| Method | Route | Description |
|---|---|---|
| GET | `/api/calendar` | The merged feed for a date range. Required `?from=` and `?to=` (`YYYY-MM-DD`). Returns class meetings, assignment due dates, reminders and the student's own events as one list of `CalendarItemDto`, all-day items first within each day. One source failing degrades that source rather than the whole day. |

The dashboard reads the same day through the same `ICalendarFeedService`, so the
two pages cannot disagree about what is on it.

| Method | Route | Description |
|---|---|---|
| GET | `/api/calendar-events` | The student's own events. Optional `?from=`, `?to=`. |
| GET | `/api/calendar-events/{id}` | One event. |
| POST | `/api/calendar-events` | Body: title, description?, startDateTime, endDateTime, isAllDay, location?, eventType, isRecurring, recurrenceRule?, colorHex?. |
| PUT | `/api/calendar-events/{id}` | Update. |
| DELETE | `/api/calendar-events/{id}` | Delete. |

---

## Reminders — `/api/reminders`

| Method | Route | Description |
|---|---|---|
| GET | `/api/reminders` | List. Optional `?from=`, `?to=` (`YYYY-MM-DD`). |
| GET | `/api/reminders/{id}` | One reminder. |
| POST | `/api/reminders` | Body: title, notes?, date, isCompleted. |
| PUT | `/api/reminders/{id}` | Update. |
| PATCH | `/api/reminders/{id}/status` | Body: isCompleted. |
| DELETE | `/api/reminders/{id}` | Delete. |

---

## Email notifications — `/api/notifications`

| Method | Path | Purpose |
|---|---|---|
| GET | `/preferences` | Every switch, plus the student's time zone and whether a mail provider is actually configured (`deliveryConfigured`) |
| PUT | `/preferences` | Replace them. Assignment reminder offsets are a list of hours, at most four, each between 1 and 336 |
| POST | `/test` | Send one test email to the caller, so "is this working" does not mean waiting for a deadline |
| POST | `/run` | Force a reminder pass immediately. **404 outside Development** — it sends to everyone who is due something, not just the caller |

Sending is queued, never done on the request thread. `ReminderBackgroundService`
runs a pass in-process every five minutes; at deployment that becomes a secured
endpoint with a hosted cron in front of it and none of the logic moves.

---

## Profile photo — `/api/profile/photo`

| Method | Route | Description |
|---|---|---|
| POST | `/api/profile/photo` | `multipart/form-data`, field `file`. Re-encoded server-side to a square JPEG. |
| DELETE | `/api/profile/photo` | Remove it. |

Photos are read back through `GET /api/students/{id}/photo`, behind the same
visibility check as the profile — never from a public folder.

---

## Students — `/api/students`

Finding other students, and seeing what they have chosen to show. This is the
first part of PursuitHQ that returns somebody else's data, so it works under
rules of its own:

- **Name search only lists students who opted in.** `IsDiscoverable` is off by
  default; nobody is enrolled in a directory by signing up for a planner.
- **Email lookup is exact, never partial, and rate limited.**
- **Everything returns `StudentCardDto`**, never the full profile DTO, and the
  email only appears on it once the two are connected.

| Method | Route | Description |
|---|---|---|
| GET | `/api/students/search` | Name search. `?q=` — at least 2 characters, or an empty list comes back. Discoverable students only, never the caller, 20 results at most. |
| GET | `/api/students/{id}` | One student's card, at whatever visibility the caller has earned. 404 when they have earned none. |
| GET | `/api/students/{id}/photo` | Their photo, behind the same visibility check. |
| GET | `/api/students/lookup` | Exact email match: `?email=`. Finds someone whether or not they are listed — knowing the address is the evidence you know them. **Capped at 20 lookups per student per hour**; over that is a 429 `TooManyLookups`. |

A card carries `relationship` (`none`, `pending`, `incoming`, `connected`,
`blocked`) and `connectionId`, so the frontend knows which button to show
without a second call. Fields beyond name, school and education level — email,
major, graduation year — appear only at full visibility, which means connected.

A student who has blocked the caller answers exactly as a wrong address does.
"Found, but you are blocked" would tell them something they were deliberately
not told.

---

## Connections — `/api/connections`

A connection is the gate on everything social: the full profile, direct
messages, being added to a group. Nothing else in the feature decides for itself
who may talk to whom.

| Method | Route | Description |
|---|---|---|
| GET | `/api/connections` | Everyone the caller is connected to, as cards, surname first. |
| GET | `/api/connections/requests` | Requests waiting for the caller to answer. |
| GET | `/api/connections/sent` | Requests the caller has sent and not heard back on. |
| POST | `/api/connections` | Send one. Body: addresseeId, note? (clipped to 300 characters). |
| POST | `/api/connections/{id}/accept` | Addressee only. 204. |
| POST | `/api/connections/{id}/decline` | Addressee only. 204. |
| DELETE | `/api/connections/{id}` | Cancel a request, or remove an existing connection. Either side. 204. |
| POST | `/api/connections/block` | Body: userId. Works with or without an existing connection. 204. |
| POST | `/api/connections/unblock` | Body: userId. Only whoever set the block can lift it. 204. |

`POST /api/connections` has four answers other than "sent":

- **Already connected** → 400 `AlreadyConnected`.
- **Already sent** → 400 `AlreadyRequested`.
- **They asked first** → the pending request is accepted rather than a second
  one opened, and the connected card comes back.
- **Blocked, either direction** → 404, the same answer as a user who does not
  exist.

There is one row per pair, always. A declined request is reused rather than
added to, because every lookup in the feature assumes one row.

Unblocking deletes the row rather than restoring what was there before —
unblocking should not quietly put back a relationship that blocking ended.

---

## Conversations — `/api/conversations`

Direct chats and group chats. The only difference between them is `isGroup` and
how membership is allowed to change. Every endpoint here is gated on the
caller's **active** membership: a conversation that does not exist, one the
caller was never in, one they have left, and one they have only been invited to
all answer 404, so nothing here reveals that a conversation exists to somebody
outside it.

Messaging is **polled, not pushed** — there is no SignalR. The client calls the
listing, message, presence and unread endpoints on timers.

### Listing and unread

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/conversations` | Every conversation the caller is in: pinned first (oldest pin at the top of that block, so an arrangement made on purpose stays put), then by last message. Carries the last message, unread count, mute and pin state, and the caller's role |
| GET | `/api/conversations/unread` | The counts behind the dot on the messages icon: conversations, connection requests, group invitations, and a total |

### Starting one

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/conversations/direct` | Body: userId. Returns the existing chat if there is one rather than opening a second |
| POST | `/api/conversations/group` | Body: name, description?, memberIds. The caller becomes Owner; everyone named is **invited**, not added |

### Group invitations

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/conversations/invitations` | Groups the caller has been invited to, with who invited them and how many members |
| POST | `/api/conversations/{id}/invitations/accept` | Join. 204 |
| POST | `/api/conversations/{id}/invitations/decline` | 204 |
| POST | `/api/conversations/{id}/invitations` | Invite more people. Body: memberIds. Admins and the owner only |

Both refuse with 403 `NotConnected` unless the caller is connected to everybody
involved — the rule that makes a connection request mean something, checked on
the server because the client is not what is being defended against. A group
needs at least one invitee and holds 100 people.

An invitation is a membership row with status `Invited`: the invitee sees the
invitation, not the messages, until they accept.

### Members and group settings

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/conversations/{id}/members` | Everyone in it, with their role and when they joined |
| DELETE | `/api/conversations/{id}/members/{userId}` | Leave, or remove somebody. `me` works in place of your own id. Removing others is admin-only; the owner cannot be removed, and an admin cannot remove another admin |
| PUT | `/api/conversations/{id}/members/{userId}/role` | Body: role (`0` Member, `1` Admin, `2` Owner). Owner only. Setting Owner is a handover — the old owner becomes an admin in the same step, so the group is never ownerless or double-owned |
| PUT | `/api/conversations/{id}` | Rename the group or change its description. Body: name, description? |
| GET | `/api/conversations/{id}/photo` | The group picture, behind membership rather than in a public folder |
| POST | `/api/conversations/{id}/photo` | `multipart/form-data`, field `file`. Re-encoded server-side. 8MB cap |
| DELETE | `/api/conversations/{id}/photo` | Remove it |

There is exactly one Owner per group and it cannot be removed by anybody. A
single unremovable role is what stops a group reaching a state where nothing can
be administered. Someone removed and later re-invited comes back as a plain
member rather than quietly regaining the rank they held.

Joining, leaving and being removed are written into the timeline as messages
with `kind: 1` (System) rather than derived, so people do not appear and vanish
from a group with no explanation.

### Per-caller state

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/conversations/{id}/mute` | Body: muted |
| POST | `/api/conversations/{id}/pin` | Body: pinned |
| POST | `/api/conversations/{id}/read` | Caught up to the newest message |
| POST | `/api/conversations/{id}/unread` | Put the marker back behind the newest message — not to null, which would mean "never opened" and mark the whole history unread |

### Messages

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/conversations/{id}/messages` | 50 at a time, oldest-first. `?before=<messageId>` for the page before that |
| POST | `/api/conversations/{id}/messages` | Body: body, replyToMessageId? |
| PUT | `/api/conversations/{id}/messages/{messageId}` | Edit your own. Body: body. Sets `isEdited` |
| DELETE | `/api/conversations/{id}/messages/{messageId}` | Soft delete — the row stays so replies to it still read, with `isDeleted` set |
| POST | `/api/conversations/{id}/messages/{messageId}/reactions` | Body: emoji. A **toggle**: the same emoji twice removes it |
| GET | `/api/conversations/search` | `?q=` across every conversation the caller is in, at least 2 characters, 50 hits. Scoped by membership inside the query, not filtered after |

A message carries its reactions, its attachments, and the sender and body of
whatever it replies to, so a thread renders from one response.

### Attachments

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/conversations/{id}/messages/attachment` | `multipart/form-data`: the files, plus an optional `body` caption. Up to 10 files, 15MB each, 60MB per request |
| GET | `/api/conversations/{id}/attachments/{attachmentId}` | Download one, members only |

One endpoint rather than "upload, then send with an id" — the two-step version
leaves an orphaned upload behind every time somebody changes their mind between
the two steps. Every file in a batch is validated before any of them is written.

Images (`.jpg .jpeg .png .webp .gif`) are re-encoded on the way in and render
inline. Everything else (`.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .md .csv
.zip`) is stored as sent and always served as a download, whatever it claims to
be — serving an uploaded file inline is how a chat becomes an XSS hole. The
extension is the gate, not the content type, because a content type arrives from
the uploader and is a claim rather than a fact.

### Typing and read receipts

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/conversations/{id}/typing` | "I am still typing." Called on a throttle from the composer. One timestamp written, nothing read |
| GET | `/api/conversations/{id}/presence` | Who is typing (within the last 6 seconds) and how far everybody else has read |

Presence is separate from the messages endpoint because these change every
second or two while the messages do not, and pulling the whole history that
often to learn one timestamp would be wasteful.

---

## Study sessions — *not built*

`StudySession` is an entity and a table, and nothing else. There is **no
controller**, nothing in the app creates one, and they were taken out of the
calendar feed in September 2026 because that source only ever returned nothing.
The table was left in place so the feature can return without a migration. The
AI study planner (`/generate`, `/accept`) is a design, not code. See
`Roadmap.md` Phase 10.

Note that the pages under `/courses/{id}/sessions` are study *chat* sessions,
which is a different thing wearing the same word.

---

## Job applications — *removed*

Built, then removed in September 2026 along with job search. See `Roadmap.md` §5.
The `JobApplication` table was kept so the feature can return without a migration.

## Saved jobs — `/api/saved-jobs`

What survived the removal: a posting the student kept, together with the score
their resume got against it.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/saved-jobs` | The list, newest first, with score and which resume it was scored against |
| GET | `/api/saved-jobs/{id}` | One, with the posting text and the stored match breakdown |
| POST | `/api/saved-jobs` | Body: title, company?, url?, postingText, score, match?, resumeId?, notes?. The posting is clipped at 20,000 characters |
| DELETE | `/api/saved-jobs/{id}` | Delete |

The match is stored as JSON exactly as the page had it, overrides included. A
row whose breakdown will not deserialise still shows its title, link and score
rather than failing the whole page.

## Preferences — `/api/preferences`

| Method | Path | Purpose |
|---|---|---|
| GET | `/colors` | The student's saved colour palette |
| PUT | `/colors` | Replace it. Every value must be a hex colour — these go straight into a style attribute, so this is the boundary where that has to be true |

## Study chat — `/api/study`

| Method | Path | Purpose |
|---|---|---|
| GET | `/conversations?courseId=` | Sessions, newest first |
| POST | `/conversations` | Start one |
| GET | `/conversations/{id}` | One session with its messages and chosen sources |
| PUT | `/conversations/{id}` | Rename |
| PUT | `/conversations/{id}/sources` | Choose which files and notes are in context. Ids not owned by the caller in that course are silently dropped |
| POST | `/conversations/{id}/ask` | Ask a question; returns the reply. Both turns are saved before responding |
| DELETE | `/conversations/{id}` | Delete the session and its messages |
| POST | `/messages/{id}/save` | Keep what a reply produced — a study guide or a practice test |
| GET | `/guides?courseId=` | Saved study guides |
| GET | `/guides/{id}` | One guide, with its markdown |
| DELETE | `/guides/{id}` | Delete it |

A reply carries `artifactKind` (0 none, 1 study guide, 2 practice test) and the
artifact itself. It stays on the message until saved, so the library only holds
what was deliberately kept.

These are **not** the same thing as `/api/conversations`, which is messaging
between students. Same word, different feature.

## Flashcards — `/api/flashcard-decks`

| Method | Path | Purpose |
|---|---|---|
| GET | `/ai-status` | Whether AI is configured, and how much of today's allowance is left |
| GET | `?courseId=` | Decks |
| GET | `/{id}` | One deck with its cards |
| POST | `/generate` | Write a deck from one file or note |
| PUT | `/{id}` | Rename |
| DELETE | `/{id}` | Delete |
| POST | `/{deckId}/cards` | Add a card by hand |
| PUT | `/{deckId}/cards/{cardId}` | Edit |
| DELETE | `/{deckId}/cards/{cardId}` | Delete |
| POST | `/{deckId}/cards/{cardId}/review` | Record right or wrong |

Generation saves nothing unless at least one card survived validation.

## Practice tests — `/api/quizzes`

| Method | Path | Purpose |
|---|---|---|
| GET | `?courseId=` | Tests, with attempt count and best score |
| GET | `/{id}` | The test to sit — **no answers, no explanations** |
| POST | `/generate` | Write one. `style` is free text describing the kind of test wanted |
| DELETE | `/{id}` | Delete the test and its attempts |
| POST | `/{id}/attempts` | Submit answers; returns the marked paper |
| GET | `/{id}/attempts` | Past scores |

`GET /{id}` deliberately omits `correctAnswer` and `explanation`. Anything the
browser receives, the student can read, and a test you can peek at is not a test.
The key comes back with the results.

Written answers are graded by AI in one batched call. A blank is marked wrong
without spending a request; a grading failure still returns a scored paper with
those answers flagged.

## Resumes — `/api/resumes`

| Method | Route | Description |
|---|---|---|
| GET | `/api/resumes` | List resume summaries (no full content). |
| GET | `/api/resumes/{id}` | Full resume content. |
| POST | `/api/resumes` | Body: title, content. |
| POST | `/api/resumes/import` | `multipart/form-data`, field `file`. Reads a `.pdf`, `.docx`, `.txt` or `.md` into the builder's own structure. 5MB cap. |
| PUT | `/api/resumes/{id}` | Update content. |
| DELETE | `/api/resumes/{id}` | Delete. |
| POST | `/api/resumes/{id}/review` | **AI.** Returns section-level suggestions. Does not modify the resume. Counts against the daily AI cap. |
| POST | `/api/resumes/{id}/match` | **AI.** Scores this resume against one job posting. Survived the job-board removal and is the engine behind `/resume/jobs`. |

`match` is `multipart/form-data`, not JSON, because the posting can arrive three
ways and one of them is a file: `jobText` (pasted), `file` (uploaded), or
`jobUrl` (fetched). They are read in that order of how clearly the student meant
it, and nothing silently falls through to a source they did not use. Returns
`JobMatchDto` — the same shape `POST /api/saved-jobs` takes back.

There is **no PDF export endpoint.** The resume is rendered and printed in the
browser.

---

## Career growth — *not built*

`Goal`, `Skill` and `Certification` are entities and tables. There is no
controller, no service and no page for any of them. This is `Roadmap.md`
Phase 8, and the endpoints below are the plan, not the API:

| Method | Route | Description |
|---|---|---|
| GET / POST | `/api/goals` | List / create goals. |
| PUT / DELETE | `/api/goals/{id}` | Update progress / delete. |
| GET / POST | `/api/skills` | List / create skills. |
| PUT / DELETE | `/api/skills/{id}` | Update level / delete. |
| GET / POST | `/api/certifications` | List / create certifications. |
| PUT / DELETE | `/api/certifications/{id}` | Update / delete. |

---

## Dashboard — `/api/dashboard`

| Method | Route | Description |
|---|---|---|
| GET | `/api/dashboard` | Single aggregated payload, so the page makes one request rather than six: today's schedule (the same `CalendarItemDto` feed the calendar uses), what is due soon with overdue flags, the course list with open-assignment counts, recent flashcard decks and practice tests, and the four header counts. |

---

## Analytics — `/api/analytics` *(not built — Phase 10)*

| Method | Route | Description |
|---|---|---|
| GET | `/api/analytics/assignments` | Completion rate and grade trend by course. |
| GET | `/api/analytics/study` | Study hours per week and per course. |

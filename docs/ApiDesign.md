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
| 404 | Not found, or not owned by caller |
| 409 | Conflict (e.g. duplicate email, duplicate connection request) |
| 413 | Upload exceeds size limit |
| 415 | Unsupported file type |
| 422 | File could not be processed (e.g. a scanned PDF with no extractable text) |
| 429 | Rate limit exceeded (AI endpoints) |
| 500 | Unhandled server error |

### Error response shape

Every error returns the same shape:

```json
{
  "error": "ValidationFailed",
  "message": "Due date must be in the future.",
  "details": { "dueDate": ["Must be a future date."] }
}
```

### Pagination

List endpoints accept `?page=1&pageSize=25` (default 25, max 100) and return:

```json
{ "items": [], "page": 1, "pageSize": 25, "totalCount": 137 }
```

---

## Auth — `/api/auth`

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/register` *(public)* | Create account. Body: firstName, lastName, email, password. Returns JWT. |
| POST | `/api/auth/login` *(public)* | Authenticate. Body: email, password. Returns JWT + user profile. Counts failed attempts; returns 423 Locked after 5 failures in 15 minutes. |
| POST | `/api/auth/forgot-password` *(public)* | Send reset email. Body: email. Always returns 200 (no account enumeration). |
| POST | `/api/auth/reset-password` *(public)* | Body: email, token, newPassword. |
| GET | `/api/auth/me` | Current user's profile. |
| PUT | `/api/auth/me` | Update profile (name, major, graduation year). |
| DELETE | `/api/auth/me` | Delete account and all owned data and files. |

---


### Password reset

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/auth/forgot-password` | Start a reset. Answers the same way for a known and unknown email, except in Development |
| POST | `/api/auth/reset-password` | Check the token and set the new password. Clears any lockout on success |

Tokens are Identity's own, expire after an hour, and work once. The reset link is
logged by the API and returned in the response **only** in Development, until
email sending exists (Phase 3b).

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
| DELETE | `/api/assignments/{id}` | Delete. |

---

## Study materials — `/api/courses/{courseId}/...`

### Folders

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/folders` | Folder tree for a course. Optional `?parentId=` for one level. |
| POST | `/api/courses/{courseId}/folders` | Body: name, parentFolderId?. |
| PUT | `/api/courses/{courseId}/folders/{id}` | Rename or move (change parentFolderId). |
| DELETE | `/api/courses/{courseId}/folders/{id}` | Delete folder and everything inside it. |

### Files

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/materials` | List files. Optional `?folderId=`, `?search=`. |
| POST | `/api/courses/{courseId}/materials` | `multipart/form-data` upload. Fields: file, folderId?, description?. Validates type and size before writing to storage. |
| GET | `/api/courses/{courseId}/materials/{id}/download` | Streams the file with its original file name. |
| PUT | `/api/courses/{courseId}/materials/{id}` | Rename, move between folders, edit description. |
| DELETE | `/api/courses/{courseId}/materials/{id}` | Delete the record and the stored file. |

### Notes

| Method | Route | Description |
|---|---|---|
| GET | `/api/courses/{courseId}/notes` | List notes. Optional `?folderId=`, `?search=`. |
| GET | `/api/courses/{courseId}/notes/{id}` | One note with full content. |
| POST | `/api/courses/{courseId}/notes` | Body: title, content, folderId?. |
| PUT | `/api/courses/{courseId}/notes/{id}` | Update title/content/folder. |
| DELETE | `/api/courses/{courseId}/notes/{id}` | Delete. |

---

## Study sessions — `/api/study-sessions`

| Method | Route | Description |
|---|---|---|
| GET | `/api/study-sessions` | List. Filters: `?from=`, `?to=`, `?courseId=`, `?status=`. |
| POST | `/api/study-sessions` | Body: title, scheduledDate, startTime, endTime, courseId?, notes?. |
| PUT | `/api/study-sessions/{id}` | Update, including status. |
| DELETE | `/api/study-sessions/{id}` | Delete. |
| POST | `/api/study-sessions/generate` | **AI.** Body: dateRange, optional courseIds. Returns suggested sessions; saves nothing. Rate-limited. |
| POST | `/api/study-sessions/accept` | Persists chosen suggestions with `isAiGenerated = true`. |

---

## Job applications — *removed*

Built, then removed in September 2026 along with job search. See `Roadmap.md` §5.
The `JobApplication` table was kept so the feature can return without a migration.

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

## Resumes — `/api/resumes`## Resumes — `/api/resumes`

| Method | Route | Description |
|---|---|---|
| GET | `/api/resumes` | List resume summaries (no full content). |
| GET | `/api/resumes/{id}` | Full resume content. |
| POST | `/api/resumes` | Body: title, content. |
| PUT | `/api/resumes/{id}` | Update content. |
| DELETE | `/api/resumes/{id}` | Delete. |
| POST | `/api/resumes/{id}/review` | **AI.** Returns section-level suggestions. Does not modify the resume. Rate-limited. |
| GET | `/api/resumes/{id}/export` | Download as PDF. |

---

## Career growth

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
| GET | `/api/dashboard` | Single aggregated payload: upcoming assignments, today's classes, today's study sessions, application counts by status, active goals. |

---

## Analytics — `/api/analytics` *(Later)*

| Method | Route | Description |
|---|---|---|
| GET | `/api/analytics/applications` | Funnel counts and conversion rates. |
| GET | `/api/analytics/assignments` | Completion rate and grade trend by course. |
| GET | `/api/analytics/study` | Study hours per week and per course. |

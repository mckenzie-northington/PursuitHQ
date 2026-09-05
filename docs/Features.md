# PursuitHQ — Feature Specification

Each feature below lists what it does, how you know it's done, which entities it touches, and which release it belongs to. Endpoint details live in `ApiDesign.md`; entity fields live in `DatabaseDesign.md`.

---

## 1. Authentication & Accounts — *MVP*

Registration, login, and account management, built on ASP.NET Core Identity.

**Behavior**
- Register with first name, last name, email, password.
- Log in and receive a JWT; the frontend stores it and sends it on every request.
- Failed login attempts are counted; the account locks for 15 minutes after 5 failures within 15 minutes, and the response says how many attempts remain before lockout.
- Log out (client discards token).
- Request a password reset link by email; set a new password with the emailed token.
- View and edit profile (name, email, optional major/graduation year).
- Set notification preferences: whether to receive emails, how far ahead of a due date, whether to get a daily morning digest and at what time, and a weekly week-ahead summary. Time zone is captured so reminders land at the right local time.
- Delete account, which removes all owned records and uploaded files.

**Acceptance criteria**
- Registering with an email that already exists returns a clear error, not a server crash.
- A password not meeting policy is rejected with the specific rule that failed.
- Requests without a valid token to any protected endpoint return 401.
- After account deletion, no rows referencing that user remain and their uploaded files are gone from storage.

**Entities:** `ApplicationUser`

---

## 2. Dashboard — *MVP*

The landing page after login: a summary of what needs attention.

**Behavior**
- Assignments due in the next 7 days, soonest first.
- Today's classes with times and locations.
- Study sessions scheduled today.
- Application pipeline counts (how many Applied, Interview, Offer).
- Active goals with progress bars.
- Quick-add buttons for assignment, application, and study session.

**Acceptance criteria**
- Dashboard loads in a single API call, not one call per widget.
- Empty states render helpful prompts ("No assignments due — add one") instead of blank panels.

**Entities:** reads `Assignment`, `ClassSchedule`, `StudySession`, `JobApplication`, `Goal`

---

## 3. Academic Planner — *MVP*

Courses, their meeting times, assignments, and the calendar that ties them together.

**Behavior**
- Add/edit/delete courses with name, professor, semester, optional credit hours and color.
- Add one or more weekly meeting times per course (day, start, end, location).
- Add/edit/delete assignments under a course with title, due date, status, and grade.
- Filter courses by semester so past semesters stay out of the way.
- Add non-class activities to the calendar (work shifts, club meetings, appointments, personal events), one-off or recurring.
- Calendar view (week and month) rendering class meetings, assignment due dates, study sessions, and other activities together, color-coded.

**Acceptance criteria**
- A course meeting Mon/Wed/Fri appears three times per week on the calendar.
- Deleting a course warns that its assignments and materials will also be deleted.
- Assignments past their due date and not completed are visually flagged as overdue.

**Entities:** `Course`, `ClassSchedule`, `Assignment`

---

## 3a. Email Notifications & Reminders — *MVP*

Students get emailed about what's coming up, on a schedule they control.

**Behavior**
- Assignment due reminders, sent a configurable number of hours before the due date (default 24).
- Event reminders a configurable number of minutes before a class or calendar event.
- Optional daily digest: one morning email listing today's classes, events, study sessions, and anything due.
- Optional weekly digest: a Sunday-evening look at the week ahead.
- Every email has a one-click link to notification settings, and a master switch to turn all email off.
- Sending respects the student's time zone.

**Acceptance criteria**
- A reminder for a given assignment is sent at most once, even if the job runs repeatedly (enforced by the `Notification` record).
- Turning off email means no email is sent, with no exceptions.
- A failed send is recorded with its error and retried once; it never crashes the job or blocks other students' emails.
- Completed assignments generate no reminders.
- Digests are skipped entirely when there is nothing to report, rather than sending an empty email.

**How it runs:** the API exposes a secured job endpoint that an external free cron service calls every 15 minutes. See Architecture.md — this is a deliberate design choice, because free hosting tiers sleep and cannot be relied on to run an in-process timer.

**Entities:** `NotificationPreference`, `Notification` · **Services:** `NotificationService`, `IEmailService`

---

## 4. Study Materials — *MVP*

A file-system-like space inside each course for uploaded files and typed notes.

**Behavior**
- Create nested folders inside a course (e.g. `Exam 1` → `Practice Problems`).
- Upload files into a course or a specific folder; multiple files at once.
- Write typed notes in-app with a title and body, saved under a course or folder.
- Rename, move (change parent folder), download, and delete materials and notes.
- Browse a course's materials as a folder tree with breadcrumbs.
- Search across file names and note titles within a course.

**Acceptance criteria**
- Uploading a file over the size limit returns a clear error naming the limit.
- Uploading a disallowed file type is rejected before anything is written to disk.
- Deleting a folder deletes its contents (files removed from storage too) after confirmation.
- A user cannot download another user's file even with a direct URL and a valid token.
- Downloaded files keep their original file name, not the internal stored name.

**Allowed file types (initial):** PDF, DOCX, PPTX, XLSX, TXT, MD, PNG, JPG, JPEG, GIF
**Size limit (initial):** 25 MB per file, 1 GB total per user

**Entities:** `MaterialFolder`, `StudyMaterial`, `Note`

---

## 5. AI Study Tools — *v1.1*

Turn uploaded course material into things a student can actually study from.

**Behavior**
- Pick any uploaded file (PDF, DOCX, PPTX, TXT) or typed note in a course and generate:
  - **Flashcards** — a deck of question/answer pairs, reviewable one card at a time with self-marking (got it / missed it).
  - **Practice quizzes** — multiple choice, true/false, and short answer questions, with an explanation revealed after each answer.
  - **Study guides** — a condensed, structured summary of the source material, saved as a note-style document.
- Review generated output before saving; edit any card or question, delete the ones that are wrong.
- Take a quiz, get scored, and see which questions were missed.
- Retake a quiz; past attempts and scores are kept so progress is visible.
- Flashcard decks track per-card review counts so a student can drill only the cards they keep missing.

**Acceptance criteria**
- Text extraction works on PDF, DOCX, and PPTX; an unsupported or unreadable file gives a clear message rather than an empty deck.
- Nothing is saved until the student accepts the generated set.
- Generated content is always editable — the student, not the AI, has the final say.
- Large documents are chunked or truncated before being sent to the AI, and the student is told when only part of a document was used.
- Generation is rate-limited per user, and an AI failure leaves no partial deck or quiz behind.
- A quiz attempt records every answer, so results can be reviewed later.

**Entities:** `FlashcardDeck`, `Flashcard`, `Quiz`, `QuizQuestion`, `QuizAttempt`, `QuizAnswer`, `StudyGuide`
**Services:** `ITextExtractionService`, `StudyToolAiService`

---

## 5a. Study Planner (AI-assisted) — *v1.1*

Scheduled study blocks, either created by hand or generated by AI.

**Behavior**
- Create/edit/delete study sessions with title, date, start/end time, optional course link, notes.
- Mark a session Planned, Completed, or Skipped.
- "Generate a study plan" sends the student's courses, upcoming assignment due dates, and existing sessions to `StudyPlannerAiService`.
- Suggested sessions are shown for review; accepted ones are saved with `IsAiGenerated = true`.
- Sessions appear on the calendar alongside classes, assignments, and other activities.

**Acceptance criteria**
- AI suggestions never overwrite or delete existing sessions.
- Nothing is saved until the student accepts it.
- Generation is rate-limited per user and fails gracefully when the AI API is unavailable.
- AI-generated sessions are visually distinguishable from manually created ones.

**Entities:** `StudySession` · **Service:** `StudyPlannerAiService`

---

## 6. Internship & Job Tracker — *MVP*

A pipeline for everything the student applies to.

**Behavior**
- Log an application: company, role, type (Internship / PartTime / FullTime), status, applied date, notes.
- Move applications through Saved → Applied → Interview → Offer → Rejected.
- Board view grouped by status, plus a table view sortable by date.
- Filter by type and status; search by company or role.
- Per-application notes for interview details and follow-ups.

**Acceptance criteria**
- Changing status updates immediately and persists on refresh.
- Counts per status column match the underlying records.
- Deleting an application asks for confirmation.

**Entities:** `JobApplication`

---

## 6a. Internship & Job Search — *v1.1*

Finding roles to apply to, not just recording ones already found.

**Behavior**
- Search internships and jobs by keyword, location, and type, powered by an external job-board API.
- "Match my resume" — pulls skills and keywords from the student's resume, uses them as the search query, and ranks results by overlap with those skills.
- Each result shows company, role, location, posted date, salary if available, and a short description.
- **Apply** opens the original posting on the employer's or job board's own site in a new tab — PursuitHQ never tries to submit applications on the student's behalf.
- **Save to tracker** creates a `JobApplication` with status `Saved`, keeping the source URL so the student can get back to the posting.

**Acceptance criteria**
- Applying always sends the student to the real posting; no fabricated or dead links.
- Saving a result to the tracker records its source URL and external id.
- The same posting saved twice does not create duplicate tracker entries.
- API failures show a readable message and never lose the student's search terms.
- Results are cached briefly to stay inside the job API's free request quota.

**Design note:** LinkedIn and Indeed do not offer open APIs for third-party job search, and scraping them violates their terms. PursuitHQ uses an aggregator with a documented API and a free tier (Adzuna is the leading candidate — see Open Decisions in README.md).

**Entities:** `JobApplication` (with `Source`, `ExternalJobId`, `SourceUrl`) · **Service:** `JobSearchService`

---

## 7. Resume Builder (AI-assisted) — *v1.1*

Create and refine resumes, with AI feedback.

**Behavior**
- Create multiple named resumes (e.g. "SWE Internship", "Data Analyst").
- Edit resume content in structured sections: summary, education, experience, projects, skills.
- "Review with AI" sends the resume to `ResumeAiService` and returns specific, section-level suggestions on content and wording.
- Format check: flags problems that hurt a resume in applicant tracking systems — missing sections, inconsistent date formats, weak or passive bullet openers, bullets without measurable results, length over one page.
- Accept a suggestion to apply it, or dismiss it; the stored resume changes only on accept.
- Export a resume to PDF.

**Acceptance criteria**
- AI review never silently modifies stored content.
- Suggestions cite which section they apply to.
- Rate-limited per user; a failed AI call shows an error and leaves the resume untouched.

**Entities:** `Resume` · **Service:** `ResumeAiService`

---

## 8. Career Growth — *v1.1*

**Behavior**
- Goals with title, target date, and 0–100 progress; mark complete.
- Skills with a proficiency level (Beginner / Intermediate / Advanced).
- Certifications with name and date earned.

**Acceptance criteria**
- Goal progress is constrained to 0–100.
- Past-target-date incomplete goals are flagged.

**Entities:** `Goal`, `Skill`, `Certification`

---

## 9. Networking Hub — *v1.1 (peer) / Later (external contacts)*

**Behavior — student-to-student (v1.1)**
- Search students by name, major, or shared course.
- Send a connection request; recipient accepts or declines.
- View connections list and pending requests.
- Direct message a connected student; conversation view with read status.
- Block or report a user.

**Behavior — external contacts (Later)**
- Track recruiters, alumni, and other professional contacts with company, role, LinkedIn URL, last contact date, and notes.
- Flag contacts not followed up with in 30+ days.

**Acceptance criteria**
- Messaging a non-connected user is rejected.
- A user cannot read a conversation they are not part of.
- Blocking prevents further messages and connection requests from that user.
- Profile search exposes only public profile fields, never email or private records.

**Entities:** `StudentConnection`, `Message`, `NetworkingContact`

---

## 10. Analytics — *Later*

**Behavior**
- Application funnel chart (applied → interview → offer, with conversion rates).
- Assignment completion rate and grade trend by course.
- Study hours logged per week and per course.
- Goal completion over time.

**Acceptance criteria**
- Charts render correctly with zero data and with a single data point.
- All figures derive from the user's own records only.

**Entities:** reads `JobApplication`, `Assignment`, `StudySession`, `Goal`

---

## Feature-to-Release Summary

| Feature | Release |
|---|---|
| Authentication & Accounts | MVP |
| Dashboard | MVP |
| Academic Planner | MVP |
| Study Materials | MVP |
| Calendar with non-class activities | MVP |
| Internship & Job Tracker | MVP |
| Email notifications & reminders | MVP |
| Internship & job search | v1.1 |
| AI study tools (flashcards, quizzes, study guides) | v1.1 |
| Study Planner (AI) | v1.1 |
| Resume Builder (AI) | v1.1 |
| Career Growth | v1.1 |
| Networking — peer connections & messaging | v1.1 |
| Networking — external contacts | Later |
| Analytics | Later |

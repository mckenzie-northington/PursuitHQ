# PursuitHQ — Requirements

## 1. Purpose

PursuitHQ is a web application that gives college students one place to manage everything tied to their academic and early-career life: classes and assignments, study materials and study time, resumes, professional goals, and the classmates they work with.

Today students spread this across a campus LMS, a calendar app, a folder of resume versions, and a group chat on somebody else's platform. PursuitHQ consolidates it, and uses AI where it saves real time (resume feedback, study tools).

**Scope changed in September 2026.** Tracking internship and job applications was built and then removed; see §3.4. What replaced it as the app's second half is the social side — finding classmates, connecting, and messaging them — which is described throughout this document as a requirement of the product rather than an extra.

## 2. Target Users

| User | Description | Primary needs |
|---|---|---|
| Undergraduate student | Taking 3–6 courses per semester | Track coursework and deadlines, keep materials organized, study with classmates |
| Graduate / transfer student | Heavier research or job-search focus | Resume iteration, goal tracking, study materials |
| Student balancing work and school | Juggling classes with shifts and commitments | Calendar covering everything, reminders that actually arrive |

Out of scope for now: professors, advisors, and university administrators. The app is student-facing only.

## 3. Scope

### 3.1 MVP (must ship before the app is usable by anyone else)

- Account registration, login, logout, password reset
- Create/edit/delete courses, with weekly meeting times
- Calendar view combining class times, assignment due dates, and study sessions
- Assignments with due dates, status, and grades
- Upload and organize study materials per course (folders, files, typed notes)
- Non-class calendar activities (work, clubs, appointments)
- Email reminders for assignments and schedule, configurable per student, delivered in the student's own time zone
- Dashboard summarizing what's due and what's pending
- Every student sees only their own data

All of the above is built, apart from the deployment itself. The internship/job application tracker used to be on this list and was removed — §3.4.

### 3.2 Version 1.1

- Resume builder with AI review and format checking — **built**, including an extracurricular section alongside education, experience, projects and skills
- AI study tools: flashcards, practice quizzes, and study guides generated from uploaded materials — **built**
- Resume matching against a job posting the student supplies, by pasting it or giving a link — **built**
- Two-step verification with an authenticator app and single-use recovery codes — **built**
- Goals, skills, and certifications tracking — **not built.** The entities and tables exist; nothing else does
- AI study planner that suggests study sessions — **not built**

### 3.3 Students and messaging (built September 2026)

This is the largest single area of the app after the academic planner, and none of it existed before September 2026.

- An opt-in student directory: a student is findable by name only if they switch discoverability on, and lookup by exact email address otherwise
- Connection requests, with an optional short note, that can be accepted, declined, cancelled, or removed later
- Blocking, one-sided, reversible only by whoever did it
- Profile photos
- Direct messages between connected students
- Group chats with owner, admin and member roles, and invitations that have to be accepted
- Replies, edits, and deletes that leave the message in place rather than a hole in the conversation
- Emoji reactions, and up to ten attachments on one message
- Pinning a chat, muting a chat, and marking one unread again
- Searching across every conversation the student is in, and filtering the chat list by name
- Typing indicators and read receipts
- Email when a message arrives or somebody asks to connect, throttled and switchable

### 3.4 Removed: internship and job tracking

The application tracker and the job-board search were built, used, and removed in September 2026 — the tracker worked, but the search half never returned postings that were still open, and maintaining it was taking time from the study features. What remains of it is one unused `JobApplication` table, kept so that bringing the tracker back needs no migration. `Roadmap.md` Phase 5 has the full account.

Nothing in this document should be read as promising an application tracker or job search. Requirements that referred to them have been rewritten or removed.

### 3.5 Later / stretch

- Analytics dashboard with charts
- Real-time messaging over SignalR, replacing the polling the messages page does today
- Mobile-optimized PWA or native app

## 4. User Stories

### Accounts
- As a student, I can register with my email and a password so I have a private account.
- As a student, I can log in and stay logged in for a reasonable session.
- As a student, I can reset my password if I forget it.
- As a student, I can turn on two-step verification with an authenticator app, and keep recovery codes in case I lose the phone.
- As a student, I can set my time zone, so reminders arrive at the right local hour.
- As a student, I can delete my account and have my data removed.

### Academic planner
- As a student, I can add the courses I'm taking this semester, with professor and semester.
- As a student, I can set the days and times each course meets so it appears on my calendar.
- As a student, I can add assignments to a course with a due date.
- As a student, I can mark an assignment as in progress or completed, and record my grade.
- As a student, I can choose how far in advance assignment reminders reach me, and have more than one — a week out and again the night before.
- As a student, I can see a weekly and monthly calendar of my classes, assignments, and study sessions.

### Study materials
- As a student, I can upload files (PDFs, slides, images, documents) and save them under a specific course.
- As a student, I can create folders inside a course to organize materials the way I want.
- As a student, I can write and save typed notes inside a course without leaving the app.
- As a student, I can rename, move, download, and delete my materials.
- As a student, I can search my materials by file name or note title.

### Study planner — not built
- As a student, I can schedule study sessions and link them to a course.
- As a student, I can ask the AI to generate a study plan based on my upcoming assignments and exams.
- As a student, I can accept, edit, or delete any AI-suggested session.
- As a student, I can mark a session completed or skipped.

### Classmates and messaging
- As a student, I can decide whether other students can find me by name at all, and it is off until I say so.
- As a student, I can find a classmate by name if they are discoverable, or by their exact email address if I know it.
- As a student, I can send a connection request with a short note saying where we met, and accept, decline, or cancel requests.
- As a student, I can block someone, and they are not told.
- As a student, I can message anyone I am connected to, reply to a particular message, edit what I sent, and delete it.
- As a student, I can start a group chat for a course, invite people to it, and hand out admin.
- As a student, I can react to a message, attach files to one, and search back through everything I have been part of.
- As a student, I can pin a chat, mute one, and mark one unread so I remember to come back to it.
- As a student, I can be emailed when somebody messages me, without the email containing what they said.

### Resume
- As a student, I can create and edit one or more resumes in the app, including an extracurricular section.
- As a student, I can import an existing resume from a file and correct what was read out of it.
- As a student, I can ask the AI to review a resume and get specific improvement suggestions.
- As a student, I can accept or ignore AI suggestions; nothing changes without my confirmation.
- As a student, I can paste in a job posting, or give a link to one, and see how my resume measures against it — and keep the ones worth coming back to.

### Career growth — not built
- As a student, I can set goals with a target date and track progress.
- As a student, I can list my skills with a proficiency level.
- As a student, I can record certifications I've earned.

## 5. Functional Requirements

| ID | Requirement |
|---|---|
| FR-1 | The system shall allow registration with a unique email and a password meeting the policy in Security.md. |
| FR-2 | The system shall authenticate users and issue a JWT for subsequent API calls. |
| FR-3 | The system shall scope every query to the authenticated user; no user may read or modify another user's records. |
| FR-4 | The system shall support full CRUD on courses, class schedules, assignments, materials, notes, folders, calendar events, reminders, and resumes. (Goals, skills, and certifications are specified here but not yet built; study sessions exist as data and appear on the calendar, but nothing can create one yet.) |
| FR-5 | The system shall accept file uploads up to the configured size limit and only of allowed content types. |
| FR-6 | The system shall store uploaded files outside the database and record their metadata in PostgreSQL. |
| FR-7 | The system shall generate AI study plans on demand and persist accepted suggestions as study sessions. **Not built.** |
| FR-8 | The system shall generate AI resume feedback on demand without modifying the stored resume unless the user accepts a change. |
| FR-9 | The system shall allow a user to export or delete all of their data. **Partly built:** account deletion removes the database rows but not the files on storage, and there is no export. |
| FR-10 | The system shall return consistent, structured error responses for all failures. |
| FR-11 | The system shall lock an account for 15 minutes after 5 failed login attempts within 15 minutes, and clear that lock when the password is successfully reset. |
| FR-12 | The system shall let a student record non-class activities on their calendar, including recurring ones. |
| FR-13 | The system shall email reminders for upcoming assignments and events according to each student's saved preferences and time zone, and shall not send the same reminder twice. |
| FR-13a | The system shall store an IANA time zone per student, set at registration and changeable afterwards, and shall interpret every scheduled delivery in it. |
| FR-13b | The system shall allow up to four assignment reminder offsets per student, between one hour and two weeks before a due date. |
| FR-14 | The system shall let a student disable all email notifications. |
| FR-15 | The system shall extract text from uploaded PDF, DOCX, and PPTX files and generate flashcards, practice quizzes, and study guides from it. |
| FR-16 | The system shall record quiz attempts and scores, and allow retaking a quiz. |
| FR-17 | The system shall score a student's resume against a job posting the student supplies — pasted in, or fetched from a link — and shall let them save the result with the posting text, so a score can still be explained after the posting comes down. The system does not search for postings. |
| FR-18 | The system shall never submit a job application on a student's behalf or store employer-site credentials. |
| FR-19 | The system shall offer optional two-step verification using a TOTP authenticator app, shall issue single-use recovery codes when it is enabled, and shall not issue an access token until the second factor is satisfied. |
| FR-20 | The system shall not list a student in name search unless that student has explicitly enabled discoverability; the default shall be off. |
| FR-21 | The system shall allow lookup of a student by exact email address only, never by partial match, and shall rate-limit it per student. |
| FR-22 | The system shall not disclose a student's email address to another student until the two are connected. |
| FR-23 | The system shall support connection requests carrying an optional short note, with accept, decline, cancel, and remove, and shall support one-sided blocking that the blocked student is not notified of. |
| FR-24 | The system shall permit messaging only between connected students, and shall verify active membership of a conversation on every read and every write within it. |
| FR-25 | The system shall support group conversations with exactly one owner who cannot be removed, any number of admins who may invite and remove, and invitations that must be accepted before any message is visible. |
| FR-26 | The system shall support replies, edits, and deletes on messages; a deleted message shall remain in the timeline with its content removed rather than disappearing, and an edited message shall be marked as edited. |
| FR-27 | The system shall support emoji reactions, multiple file attachments per message, pinning, muting, marking a conversation unread, and search across the conversations a student belongs to. |
| FR-28 | The system shall re-encode profile photos, group photos, and images attached to messages server-side before storing them, and shall render inline only images it re-encoded itself. (Course study materials are stored as uploaded, behind an extension and content-type allow-list, and are always served as downloads.) |
| FR-29 | The system shall email a student when a message arrives or somebody asks to connect, subject to their preferences, and such email shall never contain message text or the note sent with a request. |
| FR-30 | The system shall throttle message email to at most one per conversation per recipient per 15 minutes, and shall not send it to a recipient who has read the conversation in the last 90 seconds. |

## 6. Non-Functional Requirements

| Category | Requirement |
|---|---|
| Security | Passwords hashed by ASP.NET Core Identity; optional TOTP two-step verification; all traffic over HTTPS; secrets never committed to source control. |
| Privacy | A user's materials, notes, resumes, and calendar are visible only to them. Nothing is shared between accounts except what a student deliberately sends to somebody they are connected to. |
| Performance | Typical list endpoints respond in under 500 ms with realistic data volumes; list endpoints are paginated. |
| Reliability | Database backed up daily in production; migrations are versioned and reversible. |
| Usability | Every destructive action asks for confirmation; forms show inline validation errors. |
| Accessibility | Keyboard navigable; sufficient color contrast; form fields properly labeled. |
| Maintainability | Layered architecture (Controllers → Services → Data); no business logic in controllers; DTOs at every API boundary. |
| Cost control | AI endpoints are rate-limited per user to bound API spend. |
| Portability | File storage accessed through an interface so local disk can be swapped for cloud storage without entity changes. |

**Known gaps against the rows above, as of September 2026.** These are stated here rather than quietly left out; `Roadmap.md` §"What is actually next" is where they are being worked through.

- The JWT is held in browser `localStorage`, readable by any script on the page. It belongs in an httpOnly cookie.
- Rate limiting exists in exactly one place — email lookup in the student directory. Login, registration, password reset, message sending, and the AI endpoints have none.
- Files are written to local disk, which does not survive a redeploy on a hosted container. Cloud storage behind `IFileStorageService` is required before deployment.
- List endpoints are not paginated apart from messages (50 at a time) and the capped result sets in student search.
- There is no error monitoring; a failure in a deployed API would leave no record anybody sees.

## 7. Constraints and Assumptions

- Backend is ASP.NET Core Web API on .NET 10 with Entity Framework Core and PostgreSQL.
- Frontend is Next.js (JavaScript) with Tailwind CSS.
- AI features call Google Gemini through an `IAiService` abstraction; the free Flash tier covers development, with Azure OpenAI (funded by the Azure for Students credit) as the production upgrade path.
- Gemini's free tier uses submitted content to improve Google's products; a privacy disclosure or a paid tier is required before third parties use the AI features.
- Single-region deployment; no multi-tenancy beyond per-user data isolation.
- Students authenticate with email/password, optionally with a TOTP second factor. No university SSO.
- Messaging is delivered by polling, not push: the client re-fetches on a timer. SignalR is a planned replacement, not a current dependency.
- Transactional email is sent through Resend, queued in-process; without an API key it falls back to writing the message to the API console.

## 8. Success Criteria

The project is considered successfully delivered when:

1. A new user can register, log in, and use every MVP feature without developer assistance.
2. No user can access another user's data through any endpoint. **Not yet verifiable as written:** there is no test project in the solution and no automated test of any kind, so this has only ever been checked by hand. Conversation isolation is the first thing that needs real tests, because it is the one area where a missing check returns somebody else's data rather than an error.
3. The app is deployed at a public URL over HTTPS with a live database.
4. Documentation in this `docs` folder is sufficient for another developer to run the project locally.

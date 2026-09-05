# PursuitHQ — Requirements

## 1. Purpose

PursuitHQ is a web application that gives college students one place to manage everything tied to their academic and early-career life: classes and assignments, study materials and study time, internship and job applications, resumes, professional goals, and connections with other students.

Today students spread this across a campus LMS, a calendar app, a spreadsheet of job applications, a folder of resume versions, and group chats. PursuitHQ consolidates it, and uses AI where it saves real time (resume feedback, study planning).

## 2. Target Users

| User | Description | Primary needs |
|---|---|---|
| Undergraduate student | Taking 3–6 courses per semester, applying to internships | Track coursework, deadlines, applications; keep materials organized |
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
- Internship/job application tracker with status pipeline
- Non-class calendar activities (work, clubs, appointments)
- Email reminders for assignments and schedule, configurable per student
- Dashboard summarizing what's due and what's pending
- Every student sees only their own data

### 3.2 Version 1.1

- Goals, skills, and certifications tracking
- Resume builder with AI review and format checking
- AI study tools: flashcards, practice quizzes, and study guides generated from uploaded materials
- AI study planner that suggests study sessions
- Internship/job search with resume matching

### 3.3 Later / stretch

- Analytics dashboard with charts
- AI internship match analyzer
- Email/push reminders for deadlines
- Mobile-optimized PWA or native app

## 4. User Stories

### Accounts
- As a student, I can register with my email and a password so I have a private account.
- As a student, I can log in and stay logged in for a reasonable session.
- As a student, I can reset my password if I forget it.
- As a student, I can delete my account and have my data removed.

### Academic planner
- As a student, I can add the courses I'm taking this semester, with professor and semester.
- As a student, I can set the days and times each course meets so it appears on my calendar.
- As a student, I can add assignments to a course with a due date.
- As a student, I can mark an assignment as in progress or completed, and record my grade.
- As a student, I can see a weekly and monthly calendar of my classes, assignments, and study sessions.

### Study materials
- As a student, I can upload files (PDFs, slides, images, documents) and save them under a specific course.
- As a student, I can create folders inside a course to organize materials the way I want.
- As a student, I can write and save typed notes inside a course without leaving the app.
- As a student, I can rename, move, download, and delete my materials.
- As a student, I can search my materials by file name or note title.

### Study planner
- As a student, I can schedule study sessions and link them to a course.
- As a student, I can ask the AI to generate a study plan based on my upcoming assignments and exams.
- As a student, I can accept, edit, or delete any AI-suggested session.
- As a student, I can mark a session completed or skipped.

### Internship / job tracker
- As a student, I can log an application with company, role, type, and date applied.
- As a student, I can move an application through stages: Saved → Applied → Interview → Offer → Rejected.
- As a student, I can add notes to an application (interview details, contacts, follow-ups).
- As a student, I can see all my applications grouped by status.

### Resume
- As a student, I can create and edit one or more resumes in the app.
- As a student, I can ask the AI to review a resume and get specific improvement suggestions.
- As a student, I can accept or ignore AI suggestions; nothing changes without my confirmation.

### Career growth
- As a student, I can set goals with a target date and track progress.
- As a student, I can list my skills with a proficiency level.
- As a student, I can record certifications I've earned.

## 5. Functional Requirements

| ID | Requirement |
|---|---|
| FR-1 | The system shall allow registration with a unique email and a password meeting the policy in Security.md. |
| FR-2 | The system shall authenticate users and issue a JWT for subsequent API calls. |
| FR-3 | The system shall scope every query to the authenticated user; no user may read or modify another user's records. |
| FR-4 | The system shall support full CRUD on courses, class schedules, assignments, materials, notes, folders, study sessions, applications, resumes, goals, skills, and certifications. |
| FR-5 | The system shall accept file uploads up to the configured size limit and only of allowed content types. |
| FR-6 | The system shall store uploaded files outside the database and record their metadata in PostgreSQL. |
| FR-7 | The system shall generate AI study plans on demand and persist accepted suggestions as study sessions. |
| FR-8 | The system shall generate AI resume feedback on demand without modifying the stored resume unless the user accepts a change. |
| FR-9 | The system shall allow a user to export or delete all of their data. |
| FR-10 | The system shall return consistent, structured error responses for all failures. |
| FR-11 | The system shall lock an account for 15 minutes after 5 failed login attempts within 15 minutes. |
| FR-12 | The system shall let a student record non-class activities on their calendar, including recurring ones. |
| FR-13 | The system shall email reminders for upcoming assignments and events according to each student's saved preferences and time zone, and shall not send the same reminder twice. |
| FR-14 | The system shall let a student disable all email notifications. |
| FR-15 | The system shall extract text from uploaded PDF, DOCX, and PPTX files and generate flashcards, practice quizzes, and study guides from it. |
| FR-16 | The system shall record quiz attempts and scores, and allow retaking a quiz. |
| FR-17 | The system shall let a student search internships and jobs from an external job board, rank them against their resume, and link out to the original posting to apply. |
| FR-18 | The system shall never submit a job application on a student's behalf or store employer-site credentials. |

## 6. Non-Functional Requirements

| Category | Requirement |
|---|---|
| Security | Passwords hashed by ASP.NET Core Identity; all traffic over HTTPS; secrets never committed to source control. |
| Privacy | A user's materials, notes, resumes, and applications are visible only to them. No student data is ever shared between accounts. |
| Performance | Typical list endpoints respond in under 500 ms with realistic data volumes; list endpoints are paginated. |
| Reliability | Database backed up daily in production; migrations are versioned and reversible. |
| Usability | Every destructive action asks for confirmation; forms show inline validation errors. |
| Accessibility | Keyboard navigable; sufficient color contrast; form fields properly labeled. |
| Maintainability | Layered architecture (Controllers → Services → Data); no business logic in controllers; DTOs at every API boundary. |
| Cost control | AI endpoints are rate-limited per user to bound API spend. |
| Portability | File storage accessed through an interface so local disk can be swapped for cloud storage without entity changes. |

## 7. Constraints and Assumptions

- Backend is ASP.NET Core Web API on .NET 10 with Entity Framework Core and PostgreSQL.
- Frontend is Next.js (JavaScript) with Tailwind CSS.
- AI features call Google Gemini through an `IAiService` abstraction; the free Flash tier covers development, with Azure OpenAI (funded by the Azure for Students credit) as the production upgrade path.
- Gemini's free tier uses submitted content to improve Google's products; a privacy disclosure or a paid tier is required before third parties use the AI features.
- Single-region deployment; no multi-tenancy beyond per-user data isolation.
- Students authenticate with email/password only in v1 (no university SSO).

## 8. Success Criteria

The project is considered successfully delivered when:

1. A new user can register, log in, and use every MVP feature without developer assistance.
2. No user can access another user's data through any endpoint (verified by test).
3. The app is deployed at a public URL over HTTPS with a live database.
4. Documentation in this `docs` folder is sufficient for another developer to run the project locally.

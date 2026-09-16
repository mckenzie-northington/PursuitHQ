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
- View and edit profile (name, email, optional school, education level, major, graduation year).
- Upload a profile photo. It is decoded and re-encoded on the way in, which strips the EXIF a phone photo carries, and it is served through an endpoint that checks who is asking rather than from a public folder.
- Choose whether to be listed in the student directory. Off by default — see Students & Connections.
- Turn on two-step verification with an authenticator app. Enabling it hands over ten single-use recovery codes, shown once and stored only as hashes; turning it off needs the account password.
- Set notification preferences: whether to receive emails, how far ahead of a due date, whether to get a daily morning digest and at what time, and a weekly week-ahead summary. Time zone is captured so reminders land at the right local time.
- Delete account, which removes all owned records.

**Acceptance criteria**
- Registering with an email that already exists returns a clear error, not a server crash.
- A password not meeting policy is rejected with the specific rule that failed.
- Requests without a valid token to any protected endpoint return 401.
- After account deletion, no rows referencing that user remain.
- Two-step verification is only switched on after a code proves the secret arrived intact, so a scan that silently failed cannot lock somebody out of their own account.
- A wrong two-step code says to check the phone's clock, because that is what is usually wrong.

**Known limitation:** deleting an account removes its database rows but not its uploaded files from storage. That half is deferred, not done — see Roadmap.md Phase 2.

**Entities:** `ApplicationUser`

---

## 2. Dashboard — *MVP*

The landing page after login: a summary of what needs attention.

**Behavior**
- Today's schedule: classes, calendar events, and study sessions happening today.
- Assignments due in the next 7 days, soonest first, with anything already late included and flagged overdue.
- Courses, each with a count of its open assignments.
- Recently created flashcard decks, each with the accuracy of every review so far.
- Recently created tests, each with its question count, completed attempts, and best score.
- Counts across the account: courses, open assignments, overdue assignments, and study tools created.
- Links through to the calendar, assignments, study, and courses pages.

**Acceptance criteria**
- Dashboard loads in a single API call, not one call per widget.
- "Today" is the student's own date in their time zone, not the server's idea of today.
- A deck nobody has reviewed shows no accuracy at all rather than 0%. Never studied and studied badly are very different things to put in front of someone.
- Empty states render helpful prompts ("Add a course and PursuitHQ has something to work with") instead of blank panels.

**Entities:** reads `Assignment`, `Course`, `ClassSchedule`, `CalendarEvent`, `StudySession`, `FlashcardDeck`, `Quiz`, `StudyGuide`

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
- Optional weekly digest: a look at the week ahead, on a day and at a time the student picks (Sunday evening by default).
- A message email when another student messages you, and a request email when one asks to connect or invites you to a group. Both are on by default and each has its own switch; when they are sent, and what they are careful not to say, is under Messaging and Group Chats.
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

## 6. Internship & Job Tracker — *removed*

Built, then removed by decision in September 2026. The tracker itself worked; the search half never did — postings that looked promising were routinely closed by the time a student clicked through — and keeping it honest was pulling time away from the study features, which are the reason the app exists.

The `JobApplication` entity and its `DbSet` were left in place on purpose: the table costs nothing, removing the feature needed no migration, and bringing it back will not need one either. Nothing else survives — there is no applications controller, no job search service, and no `/applications` or `/jobs` page.

**Before rebuilding it:** the problem was never the tracker. If this comes back it should be one a student types into themselves, with search added only if a source proves it keeps its listings current. See Roadmap.md Phase 5 for the full account.

**Entities:** `JobApplication` (table retained, nothing reads it)

---

## 6a. Internship & Job Search — *removed*

Removed alongside the tracker above, and for the reason the tracker was removed. See Roadmap.md Phase 5.

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

## 9. Students & Connections — *v1.2*

Finding other students, and the request that has to be accepted before anything social is possible.

**Behavior**
- Search students by first name, last name, or full name. Two letters minimum, twenty results at most.
- Name search only returns students who have put themselves in the directory. Discoverability is off by default — nobody is enrolled in a directory by signing up for a planner.
- Look somebody up by their exact email address instead. This finds a student whether or not they are listed, which is how you reach a classmate who would rather not appear in search. It is capped at 20 lookups per student per hour, because it also confirms whether an address has an account.
- Send a connection request, optionally with a short note ("we met in CS 201"), clipped to 300 characters. That note is the only text a stranger can put in front of somebody who has not accepted them.
- Accept or decline an incoming request, cancel one you sent, or remove a connection you already have.
- Block somebody, and unblock them again.
- Three tabs: find students, answer requests, and browse your connections. The connections list has a search box of its own, always shown rather than appearing past some number of people — a search box that comes and goes is harder to trust than one that is simply there.

**What a connection unlocks:** the full profile, direct messages, and being invited to a group. Every one of those asks whether the two are connected rather than whether the other person is a student, and they all ask it in one place — `ConnectionService`, which is the only code that reads the relationship. A connection is stored on one row with a requester and an addressee, so every question about a pair has to be asked in both directions, and scattering that is how a blocked person ends up still able to message.

**How much of a profile is visible**
- **None** — blocked, or no relationship and not listed. Treated as a 404.
- **Card** — name, photo, school, education level. Enough to decide whether to connect.
- **Full** — the card plus email, major, and graduation year. Connected students only.

**Acceptance criteria**
- A pending or declined request still shows the card, so a declined request does not make somebody vanish mid-flow.
- Blocking is one-sided and silent. The blocked person gets exactly what a stranger gets: no profile, no search result, and a connection request that fails the way a request to a non-existent account fails. The blocker keeps the card, because that is the only place left to undo it from.
- An email lookup by a blocked viewer answers "nobody is using that email address", the same as a wrong address.
- Sending a request to somebody who already sent you one is treated as accepting theirs, rather than opening a second request neither side can tell from the first.
- Wildcards in a search term are escaped, so searching for `%` cannot list every discoverable student in one request.
- Unblocking returns the pair to no relationship at all, not to connected — unblocking should not quietly restore something blocking ended.
- There is one row per pair at all times, in either direction.
- Search results are always cards. A connected student's fuller profile comes from opening it, not from appearing in a list.
- Profile photos are behind the same visibility check as the profile, so one cannot be fetched by anyone who happens to learn the file name.

**Entities:** `Connection`, `ApplicationUser` (`IsDiscoverable`, `School`, `EducationLevel`, `PhotoPath`) · **Services:** `ConnectionService`, `StudentCardMapper`, `ProfilePhotoService`

---

## 10. Messaging — *v1.2*

Direct and group chats, with one membership check standing in front of every read and write.

**Behavior**
- Start a direct chat from a connected student's profile. There is one chat per pair; opening it again finds the existing one.
- Send a message, up to the 4,000 characters the composer allows. Enter sends, Shift+Enter starts a new line.
- Reply to a message. The reply carries a clipped quote of what it answers.
- Edit your own messages. An edit is always marked as edited — an edit that leaves no trace is a way to rewrite what somebody remembers being said.
- Delete a message: your own anywhere, anybody's if you are an admin or the owner of the group. A group with no way to take down what somebody posted is a group with no way to deal with a problem.
- React with any emoji. Tapping the same one again removes it. Common ones sit in a row above the message menu, with the full picker behind a `+`.
- Send up to 10 files or images in one message, with an optional caption.
- Pin a chat to the top of the list, mute it, mark it read, or mark it unread.
- Search every conversation you are in from one box: it matches conversation titles and message text at the same time, from two characters, newest 50 hits.
- The conversation list shows each chat's last message and who sent it, an unread count, and whether it is pinned or muted.
- A dot on the messages icon counts unread messages, connection requests, and group invitations together. To the person looking at it they are the same thing: somebody is waiting on you.
- Typing indicators, and a read receipt under the newest message you sent — "Sent", "Seen", or "Seen by 3" in a group.
- Removing a direct chat takes it off your list only. It does not block the other person, and their next message puts it back, because otherwise the message lands somewhere you will never look. Leaving a group is deliberately not treated this way: that is a decision.

**Attachments**
- Images: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`. Files: `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.ppt`, `.pptx`, `.txt`, `.md`, `.csv`, `.zip`.
- 15 MB per file, 10 files per message, 60 MB per upload.
- The extension is the gate, not the content type. A content type arrives from the uploader and is a claim, not a fact.
- Images are decoded and re-encoded at up to 1600 pixels, which strips their EXIF and proves they really are images. PNG stays PNG, because screenshots of code and slides are the most common thing a student shares and JPEG turns small text into a smear; everything else becomes JPEG.
- Only a file the server decoded itself is ever rendered inline. Everything else downloads, whatever it claims to be — serving an uploaded file inline is how a chat becomes an XSS hole.
- The stored name is a GUID, so nothing about the file system can be guessed from a file name; the sender's name for the file is only ever displayed.

**Acceptance criteria**
- Every read and write proves an *active* membership first. A conversation that does not exist, one the caller was never in, one they left, and one they have only been invited to all become the same 404, so the API never reveals to somebody outside a conversation that it exists.
- A direct message is refused once the connection is removed or blocked, even though the conversation still exists — an old thread must not stay open as a back door.
- Replying to a message from another conversation is rejected rather than silently quoting something the reader is not allowed to see.
- Sending counts as reading, so your own message never comes back as unread.
- System messages ("Marcus left") belong in the timeline and never raise an unread count.
- A muted conversation still shows its unread count in the list but never lights the dot in the nav bar. Muting means stop shouting at me, not hide that anything happened.
- Marking a chat unread puts the marker just behind the newest message from somebody else, never back to never-opened, which would mark the whole history unread instead of the one thing you wanted to come back to.
- Pinned chats sort above the rest, and among themselves by when they were pinned, earliest first. A new pin lands underneath the existing ones, so an arrangement made on purpose does not rearrange itself the moment somebody types.
- Deleting is soft: the row stays and the UI says so, because removing it outright leaves a hole in a conversation two people are reading at once and makes "did they say that?" unanswerable.
- History pages by message id rather than by offset, so scrolling back does not shift under you when somebody sends something.
- A batch of attachments is checked in full before any of it is written, so a batch that fails halfway leaves no stored files behind and no guessing about which file was the problem.
- Search reaches only conversations the student is an active member of, scoped in the query rather than filtered after it, and skips deleted and system messages.

**How it updates:** polling, not push. An open conversation re-fetches every 5 seconds; typing and read state every 2.5 seconds, because those are the parts that have to feel immediate; the conversation list every 15 seconds. The composer reports typing at most every 2.5 seconds against a 6-second server window, so a steady typist never flickers off and somebody who walks away mid-sentence stops being announced. Typing is stored as a timestamp rather than a flag precisely so it expires on its own. Moving to SignalR would replace the message timer and leave everything else as it is.

**Email:** a message email names the sender and the group and nothing else. Putting the text in it would hand what two students said to each other to a mail provider, leave it in an inbox that may be read over somebody's shoulder, and make "delete" a lie. At most one email per conversation per person every 15 minutes, since a chat is a back-and-forth and the alternative is thirty emails from a five-minute conversation. Nothing is sent for a muted conversation, or to somebody whose last read was within 90 seconds — they have the thread open and are watching it arrive. A failure to queue an email never turns a sent message into an error.

**Entities:** `Conversation`, `ConversationMember`, `Message`, `MessageReaction`, `MessageAttachment` · **Services:** `MessageNotifier`, `ProfilePhotoService`, `IFileStorageService`

---

## 11. Group Chats — *v1.2*

The same conversations as above, plus who is allowed to run them.

**Behavior**
- Create a group with a name, an optional description (clipped to 300 characters), and at least one other person. Up to 100 members.
- You can only invite students you are connected with — all of them, not most. A group is otherwise a way to put a message in front of somebody who never accepted you.
- People are invited, never added. Being dropped into a room without being asked is how group chats become something people resent.
- An invitation shows the group's name, description, picture, member count, and who sent it, and is accepted or declined from the messages page. An invitee can see the group picture, so the decision is not a blind one, but not its messages.
- Three roles: owner, admin, member. Admins can invite, remove members, rename the group, and change its picture. Only the owner can change roles.
- Hand the group to somebody else, which makes them the owner and you an admin.
- Leave a group, or remove somebody from one.
- Set or remove a group picture (admins and above). It goes through the same pipeline as a profile photo, so a group picture cannot carry the GPS coordinates of wherever it was taken into a room of people who were not there.
- Changes are narrated in the timeline as system messages: joined, left, was removed, renamed, is now an admin, is now the owner. They are stored as messages rather than derived, because they belong in the order they happened — without them people appear and vanish from a group with no explanation, which reads as a bug.

**Acceptance criteria**
- The owner cannot be removed by anybody, and an admin cannot remove another admin — otherwise two admins can fight over a group and whoever clicks first wins.
- There is exactly one owner at a time. A handover moves both sides in one step, so the group is never briefly ownerless or briefly double-owned.
- When an owner leaves, the group passes to the longest-serving admin, or to the longest-serving member if there are no admins. An ownerless group nobody can rename, add to or clean up is worse than any choice this makes.
- Somebody who left and is invited again is re-invited rather than re-added, and returns as a plain member rather than quietly regaining the rank they held.
- Declining an invitation removes the row, so a later invitation does not read as a rejoin in the group's history.
- Membership is one row through the whole lifecycle — invited, active, left — so nobody can hold an invitation and a membership at once and end up in a group twice.
- A member who leaves keeps their messages in the timeline; the row stays so the history still reads.
- The last person out closes the room. An empty conversation is a row nobody could ever see again.
- Muting and pinning are per member. Which conversations matter is one person's judgement, and muting a busy group for everybody in it would be somebody else deciding what you look at.

**Known limitation:** the connection check is between the inviter and each person they invite, not between every pair in the group. A group can therefore hold two students who are not connected to each other.

**Email:** an invitation email names who invited you and which group, and only people newly invited are emailed — somebody already in the group, or already holding an invitation, is not told again. These are not throttled the way message email is: an invitation is a single event that needs an answer, and the API already refuses a second request while the first is outstanding.

**Entities:** `Conversation`, `ConversationMember`, `Message` · **Services:** `RequestNotifier`, `ProfilePhotoService`

---

## 12. Analytics — *Later*

**Behavior**
- Assignment completion rate and grade trend by course.
- Study hours logged per week and per course.
- Goal completion over time.

**Acceptance criteria**
- Charts render correctly with zero data and with a single data point.
- All figures derive from the user's own records only.

**Entities:** reads `Assignment`, `StudySession`, `Goal`

---

## Feature-to-Release Summary

| Feature | Release |
|---|---|
| Authentication & Accounts | MVP |
| Dashboard | MVP |
| Academic Planner | MVP |
| Study Materials | MVP |
| Calendar with non-class activities | MVP |
| Internship & Job Tracker | Removed |
| Email notifications & reminders | MVP |
| Internship & job search | Removed |
| AI study tools (flashcards, quizzes, study guides) | v1.1 |
| Study Planner (AI) | v1.1 |
| Resume Builder (AI) | v1.1 |
| Career Growth | v1.1 |
| Students & connections | v1.2 |
| Messaging (direct chats, attachments, reactions) | v1.2 |
| Group chats | v1.2 |
| Analytics | Later |

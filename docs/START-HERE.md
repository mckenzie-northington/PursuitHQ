# START HERE — Picking PursuitHQ Back Up

Your day-to-day guide. Read this first whenever you sit down to work on PursuitHQ.

---

## 1. Which programs to open

You need **three** things open. PostgreSQL is a fourth but you never open it directly.

### VS Code — your main workspace

This is where you will spend most of your time. It handles the frontend code, the
backend code, and the terminals.

1. Open **VS Code** (Windows key → type `code` → Enter)
2. Menu bar → **File** → **Open Folder…**
3. Navigate to and select this folder:
   `C:\Users\mcken\Desktop\Personal Projects\PursuitHQ`
   (select the folder itself — do not go inside it)
4. Click **Select Folder**

You should now see `Backend`, `Frontend`, `docs`, `README.md`, and `start-dev.ps1`
in the left sidebar.

**Shortcut for next time:** VS Code remembers recent folders. **File** → **Open
Recent** → PursuitHQ.

Opening the **root** folder matters — it means one window gives you the backend,
the frontend, the docs, and git all at once.

### Visual Studio Community — optional, for C# work

VS Code can do everything through the terminal, so this is optional. Visual Studio
has better C# tooling (IntelliSense, debugging, breakpoints), so use it if you want
those.

1. Open **Visual Studio Community**
2. Click **Open a project or solution**
3. Navigate to:
   `PursuitHQ\Backend\PursuitHQ\PursuitHQ.slnx`
4. Select that `.slnx` file and click **Open**

**Shortcut for next time:** Visual Studio's start screen lists recent projects —
PursuitHQ will be at the top.

> If you only want one editor open, use VS Code. Everything so far was done through
> its terminal.

### pgAdmin 4 — to look at the database

Only needed when you want to see the actual data or check that tables exist.

1. Windows key → type `pgadmin` → **pgAdmin 4**
2. Wait 10–30 seconds — it is slow to start and opens in its own browser-like window
3. If it asks for a **master password**, that is pgAdmin's own password, not your
   postgres one
4. Left panel → expand **Servers** → **PostgreSQL 18** → **Databases** → **pursuithq**
   → **Schemas** → **public** → **Tables**

To run SQL: right-click the **pursuithq** database → **Query Tool**, type the
statement, press **F5**.

> The Query Tool runs *everything in the editor*, not just what you last pasted.
> Press **Ctrl+A** then **Delete** before each new query, or leftover text from the
> last one gets glued onto the front and you get a syntax error on a line you did
> not write.

### Your browser — to use the site

Chrome or whatever you normally use. The startup script opens it for you.

- Website: `http://localhost:3000`
- API testing page: `http://localhost:5051/swagger`

### PostgreSQL itself — nothing to open

PostgreSQL runs in the background as a Windows service and starts automatically with
your computer. You never open it.

If you ever get a database connection error, check that it is running:
Windows key → type `services` → **Services** → find **postgresql-x64-18** → it should
say **Running**. If not, right-click → **Start**.

---

## 2. Start everything (2 minutes)

**The easy way:** open a terminal in VS Code (`Terminal` → `New Terminal`) and run:
```powershell
cd "C:\Users\mcken\Desktop\Personal Projects\PursuitHQ"
.\start-dev.ps1
```
The `.\` prefix is required — PowerShell will not run a script from the current
folder without it.

("Run with PowerShell" only appears in Windows File Explorer, not in VS Code. To
use it that way, right-click the file in VS Code → **Reveal in File Explorer**,
then right-click it there.)

It opens two windows (the API and the website), waits for both to boot, then opens
your browser. Leave both windows open while you work.

**The manual way**, if the script gives you trouble — open two terminals in VS Code
(use the **+** icon for the second one):

Terminal 1 — the API:
```powershell
cd "C:\Users\mcken\Desktop\Personal Projects\PursuitHQ\Backend\PursuitHQ\PursuitHQ.API"
dotnet run
```

Terminal 2 — the website:
```powershell
cd "C:\Users\mcken\Desktop\Personal Projects\PursuitHQ\Frontend\pursuithq-web"
npm run dev
```

**First time on a new machine or after a fresh clone**, the website needs its
packages installed once:
```powershell
cd "C:\Users\mcken\Desktop\Personal Projects\PursuitHQ\Frontend\pursuithq-web"
npm install
```
There is no env file to copy. `lib/api.js` falls back to `http://localhost:5051`
when `NEXT_PUBLIC_API_URL` is not set, which is exactly what you want locally —
an `.env.local` is only needed when the API is somewhere else.

The API needs `ConnectionStrings:DefaultConnection` and `Jwt:Key` in
user-secrets — it throws on startup without the key and tells you the command to
run. The AI and email keys are optional; the app runs without them, with those
features switched off. See §5a. On a fresh machine also run
`dotnet ef database update` from the `PursuitHQ.API` folder to create the tables.

**Both must be running.** The website is just a face on top of the API — if the API
is not running, nothing on the site works.

**The website reloads itself when you change frontend code. The API does not.**
Any change to C# needs the API stopped and restarted, and any change that touches
the database needs a migration on top of that. This is the single most common
reason something "isn't working" — see §6.

---

## 3. Where everything lives

| What | Where |
|---|---|
| The website | http://localhost:3000 |
| API testing page (Swagger) | http://localhost:5051/swagger |
| Database browser | pgAdmin 4 (Windows key → type `pgadmin`) |
| Your code | `Backend/PursuitHQ/PursuitHQ.API` and `Frontend/pursuithq-web` |
| The plan | `docs/` — start with `docs/README.md` |
| What to build next | `docs/Roadmap.md` |
| The solution file | `Backend/PursuitHQ/PursuitHQ.slnx` — one project, `PursuitHQ.API`. There is no test project |

---

## 4. What already works

- **Accounts** — register, log in, profile, 5-attempt lockout, hashed passwords
- **Two-step verification** — optional, and off unless you turn it on. Scan a QR
  code with an authenticator app, then logging in asks for a six-digit code as a
  second step. Ten recovery codes for when the phone is gone; turning it off or
  reissuing the codes costs your password
- **Courses** — create, edit, delete, weekly meeting times, term start and end
  dates, and a color
- **Assignments** — create, delete, filter, tick off as done, overdue flagging
- **Calendar** — month, week, and day views. Merges class meetings, assignment due
  dates, reminders, and your own events. Click any empty slot to add an
  event; all-day and multi-day events; weekly repeats; overlapping events sit
  side by side; a live line shows the current time. Assignments have a checkbox
  in the all-day strip, and clicking a class opens that course's materials
- **Reminders** — a dated to-do list of your own, on the calendar alongside
  everything else
- **Saved colors** — the colors you add are stored on your account and offered in
  both the event dialog and the course form
- **Study materials** — nested folders, drag-and-drop upload, move files between
  folders, inline preview (images, PDFs, text, and extracted text from Word /
  PowerPoint / Excel), typed notes, course-wide search, storage quota
- **Flashcards** — generated from a file or note, reviewed one card at a time
  with a self-grade, editable. Per-card counters show what you keep missing
- **Study chat** — a per-course tutor at `/study`. Attach files and notes, ask
  questions, ask for a study guide. Sessions are saved per course and can be
  renamed or deleted; guides download as markdown
- **Practice tests** — pick the question types and how many, or ask in chat.
  Taken in the app and scored: multiple choice and true/false instantly, written
  answers graded on meaning by AI with a sentence of feedback on each
- **Resume builder** — write one in the app or import a PDF / Word / text file,
  format it section by section, and print it from the browser. AI review gives
  section-level suggestions
- **Job matcher** — score a resume against one posting, pasted, uploaded, or by
  link. Postings worth keeping are saved with their score and breakdown at
  `/resume/jobs`
- **Student directory** — find classmates by name (only people who switched
  themselves on in settings) or by exact email. A card shows name, school and
  education level; the email, major and graduation year appear once you are
  connected
- **Connections** — send a request with a note, accept, decline, cancel, remove,
  block and unblock. A connection is the gate on everything social
- **Messages** — direct chats with anyone you are connected to, and group chats
  with owner / admin / member roles, invitations rather than being dropped in,
  a group photo, and a system line in the timeline when people join or leave.
  Replies, edits, deletes, emoji reactions, file and image attachments (10 per
  message, 15MB each), search across every conversation, typing indicators, read
  receipts, pin, mute, and mark-unread. It **polls** — the list every 15 seconds,
  an open thread every 5, typing and read receipts every 2.5; SignalR replaces
  exactly those timers
- **Email** — reminders for assignment due dates and calendar events, daily and
  weekly digests, optional confirmations when you create something, and mail
  when you get a message or a connection request. Every switch is in settings,
  and delivery is in your own time zone. Password reset goes out by email too.
  Without a Resend API key configured, every one of those prints to the API
  terminal instead, so the whole flow still runs on a laptop
- **Settings** — light/dark/system theme, profile, photo, time zone, whether you
  are listed in the student directory, two-step verification, every email
  switch, change password (needs your current one), delete account
- **Password reset** — request a link and set a new password; a successful reset
  also clears any lockout
- **Dashboard** — one aggregated request: today's schedule, what is due soon and
  what is overdue, your courses with open-assignment counts, recent decks and
  test scores. Assignments are tickable from the page itself
- **Time zones** — every account carries an IANA zone, set at registration and
  changeable in settings. "Overdue" and every reminder are worked out against
  *your* wall clock, not the server's
- **Database** — 32 application tables in PostgreSQL on top of Identity's own,
  every one modeled and migrated

Phases 0–4 (including 2a, 3a and 3b), 6, 7, 9 and 11–11d are complete. Phases 8
(career growth), 10 (analytics and polish) and 12 (deployment) are open.

**Removed on purpose:** the job tracker and job search. Every posting the search
returned had already closed by the time you clicked it, and it was eating the time
meant for the study features. `docs/Roadmap.md` §5 has the full account, including
what was kept so it could come back later — the resume matcher and saved jobs
both survived and still work.

---

## 5. What you are building next

The social feature area was built in September 2026, out of order. What is left
is a short list of things the code as it stands is actually waiting on, roughly
in the order of how much it would hurt to leave them. `docs/Roadmap.md` has the
same list under "What is actually next", with more detail on each.

- **The JWT out of `localStorage` and into an httpOnly cookie.** Any script that
  gets onto the page can read the token today. This is the only remaining item
  that is a genuine hole rather than a rough edge, and it is deliberately not
  done yet: it touches login, logout, every API call and the CORS policy, and
  wants a session where each step can be tested.
- **Error monitoring.** In production a 500 is invisible unless somebody says so.
- **Some automated tests.** There is no test suite and no test project in the
  solution. The first ones to write are the ones proving a non-member cannot
  read, send, react, edit, delete, invite or download in a conversation.
- **SignalR in place of polling.** Four timers currently stand in for push: the
  conversation list, the open thread, presence, and the unread badge.

Features still unbuilt, neither blocking the other:

- **Phase 8 — career growth.** Goals, skills, and certifications: the last pillar
  of the original PursuitHQ idea with no code behind it. `Goal`, `Skill` and
  `Certification` are entities and tables already — no controller, no page. Same
  shape as courses and assignments, so the pattern is one you have built before.
- **Study sessions.** The `StudySession` table exists and nothing uses it.
  Nothing in the app creates one, and they were dropped from the calendar feed
  in September 2026 because that source only ever returned nothing. The table
  was left in place so the feature can come back without a migration. (The pages
  under `/courses/{id}/sessions` are study *chat* sessions, which is a different
  thing wearing the same word.)
- **Phase 10 — analytics and polish.** Charts over what you already collect:
  assignments, test scores. Plus an accessibility and mobile pass.

**Build one thing end to end before starting the next.** Flashcards, then the
chat, then practice tests each went generate → save → use before the next began.
Three half-finished features are far harder to debug than one finished one.

---

## 5a. API keys already set up

Lives in user-secrets, outside the project, so it is not in git:

| Key | What it powers | Cost |
|---|---|---|
| `Ai:ApiKey` | Gemini, for every study tool | ~1¢ per request |
| `Email:ApiKey` + `Email:FromAddress` | Resend, for reminders, digests, password reset, message and request mail | Free tier covers this volume |

Also in user-secrets: `ConnectionStrings:DefaultConnection` and `Jwt:Key`. The
API refuses to start without `Jwt:Key`, and says so.

**Run `dotnet user-secrets list` from the `PursuitHQ.API` folder to see which of
these this machine actually has** — the secrets live outside the repo, so a
fresh clone or a second machine starts with none of them.

With no email key configured the app falls back to `ConsoleEmailService`, which
prints every email — reset links included — to the API terminal. Nothing breaks;
the mail just does not leave the laptop. The settings page shows this as
"delivery not configured".

**Billing is enabled**, with $10 of credit on it. Two reasons, and the second one
matters more. The free tier caps you at 20 requests per *minute*, shared across
flashcards, chat and tests, which is easy to hit in one sitting — paying lifts
that ceiling. And the free tier's terms let Google use submitted content to
improve its products, which is not an acceptable thing to do with another
student's notes; the paid plan's terms do not.

Rough costs at current prices: a flashcard deck ~1.3¢, a chat question ~0.8¢, a
study guide ~1.4¢. Maxing out the app's own 30-a-day cap every day would be about
$11 a month; realistic use is closer to $1.50. **Prices double on 1 January 2027.**

The cost driver is attached material: every chat message re-sends the full text
of every attached file, so a long conversation pays for the same PDFs repeatedly.
If a bill ever looks wrong, look there first.

**Worth setting:** a $5/month budget alert in Google Cloud Billing. Normal use
will not come near it; the risk is a bug retrying in a loop.

**The app's own cap** is `Ai:RequestsPerUserPerDay` in `appsettings.json`,
currently 30. It is a guard against runaway loops, not a budget control. Change
the number and restart — no rebuild, it is config.

---

## 6. Changing code — what has to be re-run

Getting this wrong is the most common way to lose half an hour. Match your change
to the column:

| You changed | Website (`npm run dev`) | API (`dotnet run`) | Database |
|---|---|---|---|
| Anything in `Frontend/` | Reloads itself | — | — |
| C# with no new/changed model field | — | Stop and restart | — |
| A model, or anything about the schema | — | Stop and restart | Migration needed |

**When a change touches the database, run the three steps separately and read each
one's output before continuing:**

```powershell
dotnet build
dotnet ef migrations add DescribeTheChange
dotnet ef database update
dotnet run
```

Do not paste all four at once. `dotnet ef` builds first, so a compile error turns
up as a confusing migration error — and if the migration was never created, the
API just keeps running the old code while you wonder why nothing changed.

If `dotnet build` says a file is locked, an old copy of the API is still running:

```powershell
Get-Process PursuitHQ.API -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## 7. Saving your work

From the **PursuitHQ root folder**:

```powershell
git add .
git commit -m "describe what you changed"
git push
```

Do this whenever you finish something that works. Commit more often than feels
necessary — it costs nothing and it is how you get back to a working state after
breaking something.

**Verify the push actually happened.** Run `git status` afterwards and look for
`Your branch is up to date with 'origin/main'`. "Writing objects: 100%" in the
output is *not* proof — git prints that during its own cleanup too.

**Push, do not just commit.** On 12 September the whole working folder was
emptied — every tracked file, and the contents of `.git` with it. Everything came
back from GitHub because it had been pushed an hour earlier; the only work at
risk was the four files pushed after. A commit that never left the laptop would
have gone with the laptop's copy.

---

## 8. Things that went wrong before, and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| A change to C# had no effect | The API was never restarted | Stop it and `dotnet run` again — see §6 |
| A new field or feature errors out | The migration was never created or applied | `dotnet ef migrations add …` then `dotnet ef database update` |
| `error MSB3027` / "file is locked by PursuitHQ.API" | An old API is still running, possibly in another window | `Get-Process PursuitHQ.API \| Stop-Process -Force` |
| `error CS0747` / duplicate member in an object initializer | The same property was added to an initializer twice | Delete the duplicate; `dotnet build` names the file and line |
| `cannot find path ...` on a command | Terminal is in the wrong folder | `cd` to the right folder; check the prompt text before running anything |
| `dotnet ef: command not found` | PATH not picked up | Fully quit and reopen VS Code (a new terminal is not enough) |
| Site says "Could not reach the API" | API is not running | Start it in its own terminal |
| Blank page at localhost:7136 | Wrong port / missing path | Use `http://localhost:5051/swagger` |
| "Too many failed attempts. Locked for 15 minutes." | Five wrong passwords | Wait 15 minutes, or in pgAdmin: `UPDATE "AspNetUsers" SET "LockoutEnd" = NULL, "AccessFailedCount" = 0;` — the double quotes are required |
| pgAdmin: `syntax error at or near "SELECT"` on a line you did not write | Leftover SQL still in the Query Tool | Ctrl+A, Delete, then paste just the one statement |
| `Unable to create '.git/index.lock': File exists` | A git process died and left a lock | `Remove-Item .git\index.lock` from the project root, then retry |
| `Deletion of directory '.git/objects/xx' failed (y/n)` | OneDrive holding git files — should not recur now the repo is out of OneDrive | Press **n** (never `y`), then `git status` to see what actually completed |
| Files or whole folders have vanished from the project | Happened once, 12 Sep, while the repo was in OneDrive | Do not run git in the folder. Re-clone from GitHub into a fresh folder, then check OneDrive's recycle bin for anything newer than the last push |
| `Remove-Item` says "the item is in use" | Your terminal is inside the folder you are deleting | `cd` somewhere else first, then delete |
| Pushed but still "ahead by N commits" | The push did not run — what you saw was git's cleanup | Run `git push` again and look for `abc123..def456  main -> main` |
| Swagger shows no endpoints | App rebuilt but not restarted | Stop the app fully (Ctrl+C), then `dotnet run` again |
| Swagger says `FolderNotFound` on an optional field | Swagger pre-fills optional numbers with `0`, and there is no id 0 | Clear the field before executing |
| Editor shows red errors but code looks fine | Language server is stale | Ctrl+Shift+P → **Developer: Reload Window**. Trust `dotnet build`, not squiggles |
| npm command blocked by PowerShell | Execution policy | Already fixed — `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |
| A new endpoint returns 401 or 404 | The API was never rebuilt after the code was written | Check the timestamps: `bin\Debug\net10.0\PursuitHQ.API.dll` must be newer than the `.cs` file. Rebuild |
| Gemini: "exceeded your current quota ... limit: 20" | The free tier's per-**minute** cap, not a daily one | It clears in seconds and is retried automatically now. If it persists, billing is not on the same Google Cloud project as the API key |
| Gemini returns 200 but the app says "empty answer" | The response shape did not match any known path | The API log prints the raw JSON — look for the `warn` line starting "Gemini returned a success with no readable text" |
| Something looks wrong only in dark mode | An element assumed a light background | See the note at the top of `globals.css`; that element needs its own explicit colors |
| "You can only message students you are connected with" | A 403, not a bug — a direct chat needs an accepted connection both ways | Send a request from `/students` first |
| A classmate does not come up in student search | Name search only lists people who switched on "list me in the directory" | Look them up by their exact email instead, or ask them to turn it on in settings |
| Reminder emails never arrive | No `Email:ApiKey` in user-secrets, so `ConsoleEmailService` is standing in | Look in the API terminal — the email is printed there in full |
| Nothing in the app can create a study session | There is no UI for it, and nothing reads the table | Not a bug; see §5 |

---

## 9. Known items to deal with eventually

Tracked in `docs/README.md`, repeated here so they are not forgotten:

- **File storage has two implementations**, chosen by `FileStorage:Provider`.
  Local disk in development; `S3FileStorageService` (Cloudflare R2) in
  production. It **falls back to local disk when any S3 credential is missing**
  rather than refusing to start, so check the startup log line that names which
  was chosen — a typo in the bucket settings otherwise looks like success until
  a restart eats the files. See `docs/Deployment.md` §7.
- **The JWT is stored in `localStorage`.** Readable by any script on the page. Move
  to an httpOnly cookie before other students use PursuitHQ.
- **There is no test suite.** No test project in the solution, nothing automated
  anywhere. Everything has been checked by hand. The conversation endpoints are
  the place where that is least good enough, because a missing check there shows
  somebody else's messages rather than an error.
- **Almost nothing is rate limited.** One hand-rolled cap on email lookup and
  that is all. Login, password reset, message sending and the AI endpoints are
  wide open.
- **Messaging polls.** Four timers, the fastest every 2.5 seconds. It works and
  it is wasteful; SignalR replaces the timers and nothing else.
- **Nothing reports a 500 from a deployed API.** Today the only record of a
  failure is a line in a terminal on a laptop.
- **The project no longer lives in OneDrive.** It moved to
  `C:\Users\mcken\Desktop\Personal Projects\PursuitHQ` on 12 September, after
  OneDrive emptied the folder — every tracked file and the inside of `.git` with
  it. GitHub is the backup now, which means pushing is not optional. Note that
  `C:\Users\mcken\Desktop` and `C:\Users\mcken\OneDrive\Desktop` are two
  different folders on this machine; the old, emptied copy is in the second one.
- **Keep Gemini on the paid plan.** On the free tier Google uses submitted content
  to improve its products; on a paid plan it does not, and that is the only
  reason it is safe to point other people's coursework and resumes at it. An API
  key in a project without billing reverts to free-tier terms silently - no
  error, no log line - so re-check billing whenever you rotate the key.
- **Reminders only go out while the API is up.** `ReminderBackgroundService` runs
  in-process every five minutes. At deployment that becomes a secured endpoint
  with a hosted cron in front of it.

---

## 10. If you are stuck

Give Claude this context and you will get straight back into it:

> I'm working on PursuitHQ. Phases 0–4 (with 2a two-step verification, 3a the
> calendar and 3b email reminders), 6, 7, 9 and 11–11d are done: the AI study
> tools all work — flashcards, a per-course study chat, practice tests with AI
> grading — plus the resume builder and job matcher, and the whole social side:
> student directory, connections, direct and group messages with reactions,
> attachments, search, typing indicators and read receipts, all by polling
> rather than SignalR. Settings, dark mode and password reset are done too. The
> job tracker and job search were removed on purpose; the resume matcher and
> saved jobs survived. Still open: Phase 8 (career growth), Phase 10 (analytics)
> and Phase 12 (deployment), and there is no test suite. Check
> `docs/Roadmap.md` and `docs/START-HERE.md` for where I am.

Everything is documented in `docs/`. Nothing about this project lives only in
someone's head.

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
   `C:\Users\mcken\OneDrive\Desktop\Personal Projects\PursuitHQ`
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
cd "C:\Users\mcken\OneDrive\Desktop\Personal Projects\PursuitHQ"
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
cd "C:\Users\mcken\OneDrive\Desktop\Personal Projects\PursuitHQ\Backend\PursuitHQ\PursuitHQ.API"
dotnet run
```

Terminal 2 — the website:
```powershell
cd "C:\Users\mcken\OneDrive\Desktop\Personal Projects\PursuitHQ\Frontend\pursuithq-web"
npm run dev
```

**First time on a new machine or after a fresh clone**, the website also needs its
packages and env file:
```powershell
cd "C:\Users\mcken\OneDrive\Desktop\Personal Projects\PursuitHQ\Frontend\pursuithq-web"
npm install
Copy-Item .env.example .env.local
```

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

---

## 4. What already works

- **Accounts** — register, log in, profile, 5-attempt lockout, hashed passwords
- **Courses** — create, edit, delete, weekly meeting times, term start and end
  dates, and a color
- **Assignments** — create, delete, filter, tick off as done, overdue flagging
- **Calendar** — month, week, and day views. Merges class meetings, assignment due
  dates, study sessions, and your own events. Click any empty slot to add an
  event; all-day and multi-day events; weekly repeats; overlapping events sit
  side by side; a live line shows the current time. Assignments have a checkbox
  in the all-day strip, and clicking a class opens that course's materials
- **Saved colors** — the colors you add are stored on your account and offered in
  both the event dialog and the course form
- **Study materials** — nested folders, drag-and-drop upload, move files between
  folders, inline preview (images, PDFs, text, and extracted text from Word /
  PowerPoint / Excel), typed notes, course-wide search, storage quota
- **Dashboard** — counts, upcoming assignments, course list
- **Database** — 30 tables in PostgreSQL, every table modeled and migrated

Phases 0–4 and 6 are complete, plus the calendar (Phase 3a).

**Removed on purpose:** the job tracker and job search. Every posting the search
returned had already closed by the time you clicked it, and it was eating the time
meant for the study features. `docs/Roadmap.md` §5 has the full account, including
what was kept so it could come back later.

---

## 5. What you are building next

**Phase 9 — AI study tools.** Upload a PDF, Word doc, or PowerPoint to a course
and turn it into flashcards, a practice quiz, or a study guide.

This is mostly assembly rather than new infrastructure:

- `ITextExtractionService` already pulls text out of PDF, DOCX, PPTX, and XLSX
- `IAiService` / `GeminiAiService` already talk to Gemini and are registered
- The tables already exist: `FlashcardDeck`, `Flashcard`, `Quiz`, `QuizQuestion`,
  `QuizAttempt`, `QuizAnswer`, `StudyGuide`

What is missing is the controller, the prompts, validation of what comes back, and
the review UI. Plain Gemini completions are free, so nothing here needs billing.

**Build it one tool at a time, end to end.** Flashcards first — generate, save,
review — then quizzes, then study guides. Three half-finished features are much
harder to debug than one finished one.

Other things queued up, none blocking the others:

- **Phase 7 — Dashboard.** One aggregated endpoint and a page worth opening every
  morning.
- **Phase 3b — Email reminders.** Needs a Resend account, a verified sending
  domain, and an external cron. More setup than code, and parts cannot be tested
  locally.
- **Study session UI.** They already show on the calendar and already have a
  table, but nothing can create one yet.
- **Settings page.** Profile and, once reminders exist, notification preferences.

---

## 5a. API keys already set up

Lives in user-secrets, outside the project, so it is not in git:

| Key | What it powers | Cost |
|---|---|---|
| `Ai:ApiKey` | Gemini, for the study tools | Free tier |

Also in user-secrets: `ConnectionStrings:DefaultConnection` and `Jwt:Key`.

Check them with `dotnet user-secrets list` from the `PursuitHQ.API` folder.

**Known limit:** Gemini's Google Search grounding requires billing to be enabled
and is not available on the free tier. Plain completions are free, which is
everything the study tools need.

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
output is *not* proof — git prints that during its own cleanup too, and a push
blocked by OneDrive can look identical to one that worked.

Worth doing once, if you have not already:

```powershell
git config gc.auto 0
```

That stops git from running its automatic cleanup, which is what collides with
OneDrive and produces the endless `Deletion of directory '.git/objects/xx' failed.
Should I try again? (y/n)` prompt. Answer **n** to that, never **y** — `y` retries
the same blocked delete forever.

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
| `Deletion of directory '.git/objects/xx' failed (y/n)` | OneDrive holding git files | Press **n** (never `y`), then `git status` to see what actually completed. `git config gc.auto 0` prevents it |
| Pushed but still "ahead by N commits" | The push did not run — what you saw was git's cleanup | Run `git push` again and look for `abc123..def456  main -> main` |
| Swagger shows no endpoints | App rebuilt but not restarted | Stop the app fully (Ctrl+C), then `dotnet run` again |
| Swagger says `FolderNotFound` on an optional field | Swagger pre-fills optional numbers with `0`, and there is no id 0 | Clear the field before executing |
| Editor shows red errors but code looks fine | Language server is stale | Ctrl+Shift+P → **Developer: Reload Window**. Trust `dotnet build`, not squiggles |
| npm command blocked by PowerShell | Execution policy | Already fixed — `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |

---

## 9. Known items to deal with eventually

Tracked in `docs/README.md`, repeated here so they are not forgotten:

- **No password reset.** Five wrong attempts locks you out and the only way back
  in is waiting or editing the database by hand. A development-only reset endpoint
  would solve it now; the real flow waits on email in Phase 3b.
- **The JWT is stored in `localStorage`.** Readable by any script on the page. Move
  to an httpOnly cookie before other students use PursuitHQ.
- **"Overdue" uses the server's clock.** Correct on your laptop, wrong on a UTC
  server. Needs `ApplicationUser.TimeZone` before deployment.
- **The project lives inside OneDrive.** OneDrive occasionally locks git files,
  which shows up as `index.lock` errors or failed cleanup during commits. Neither
  is dangerous — the fixes are in the table above. Pausing OneDrive sync (tray icon
  → gear → Pause syncing) while working avoids it entirely.
- **Gemini free tier uses your content to improve Google's products.** Fine while
  you are the only user; needs a privacy disclosure or a paid tier before anyone
  else uses the AI features.
- **Email provider domain** — needed before reminder emails can send.

---

## 10. If you are stuck

Give Claude this context and you will get straight back into it:

> I'm working on PursuitHQ. Phases 0–4 and 6 are done, plus the calendar (Phase
> 3a). The job tracker and job search were removed on purpose. I'm starting Phase
> 9, the AI study tools — flashcards, quizzes, and study guides generated from
> uploaded course materials. Check `docs/Roadmap.md` and `docs/START-HERE.md` for
> where I am.

Everything is documented in `docs/`. Nothing about this project lives only in
someone's head.

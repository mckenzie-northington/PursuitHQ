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
   `C:\dev\PursuitHQ`
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

> If you only want one editor open, use VS Code. Everything today was done through
> its terminal.

### pgAdmin 4 — to look at the database

Only needed when you want to see the actual data or check that tables exist.

1. Windows key → type `pgadmin` → **pgAdmin 4**
2. Wait 10–30 seconds — it is slow to start and opens in its own browser-like window
3. If it asks for a **master password**, that is pgAdmin's own password, not your
   postgres one
4. Left panel → expand **Servers** → **PostgreSQL 18** → **Databases** → **pursuithq**
   → **Schemas** → **public** → **Tables**

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

**The easy way:** in File Explorer, right-click **`start-dev.ps1`** in the PursuitHQ
folder → **Run with PowerShell**.

It opens two windows (the API and the website), waits for both to boot, then opens
your browser. Leave both windows open while you work.

**The manual way**, if the script gives you trouble — open two terminals in VS Code
(use the **+** icon for the second one):

Terminal 1 — the API:
```powershell
cd "C:\dev\PursuitHQ\Backend\PursuitHQ\PursuitHQ.API"
dotnet run
```

Terminal 2 — the website:
```powershell
cd "C:\dev\PursuitHQ\Frontend\pursuithq-web"
npm run dev
```

**First time on a new machine or after a fresh clone**, the website also needs its
packages and env file:
```powershell
cd C:\dev\PursuitHQ\Frontend\pursuithq-web
npm install
Copy-Item .env.example .env.local
```

**Both must be running.** The website is just a face on top of the API — if the API
is not running, nothing on the site works.

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
- **Courses** — create, edit, delete, plus weekly meeting times
- **Assignments** — create, delete, filter, click-to-cycle status, overdue flagging
- **Dashboard** — counts, upcoming assignments, course list
- **Database** — 30 tables in PostgreSQL, every table modeled and migrated

Phases 0–3 are complete, and Phase 6 (the website) was built early.

---

## 5. What you are building next: Phase 4 — Study Materials

Upload files and organize them in folders inside each course, plus typed notes.
This is also the groundwork for the AI study tools later — flashcards and quizzes
get generated from these uploads.

The entities already exist in the database (`MaterialFolder`, `StudyMaterial`,
`Note`). What is missing is the code that uses them.

**In order:**

1. `Services/IFileStorageService.cs` and `LocalFileStorageService.cs` — save, read,
   and delete files on disk, behind an interface so cloud storage can swap in later
2. `Controllers/FoldersController.cs` — create, rename, move, delete folders
   (nested under a course)
3. `Controllers/MaterialsController.cs` — upload with size/type validation, an
   authorized download endpoint, rename, move, delete
4. `Controllers/NotesController.cs` — straight CRUD, closest to what you have already
5. A frontend page at `app/courses/[id]/materials` — folder tree, upload button, notes

Full specification: **`docs/Features.md` section 4** and **`docs/ApiDesign.md`**
under "Study materials".

Key rules from `docs/Security.md` for this feature:
- Reject files over 25 MB and anything not on the allowed type list
- Never use the uploaded file name on disk — generate a GUID name
- Store files outside `wwwroot` so they can only be reached through an authorized
  download endpoint
- Verify the course belongs to the signed-in student before anything else

---

## 6. Saving your work

From the **PursuitHQ root folder**:

```powershell
git add .
git commit -m "describe what you changed"
git push
```

Do this whenever you finish something that works. Commit more often than feels
necessary — it costs nothing and it is how you get back to a working state after
breaking something.

---

## 7. Things that went wrong before, and their fixes

| Symptom | Cause | Fix |
|---|---|---|
| `cannot find path ...` on a command | Terminal is in the wrong folder | `cd` to the right folder; check the prompt text before running anything |
| `dotnet ef: command not found` | PATH not picked up | Fully quit and reopen VS Code (a new terminal is not enough) |
| Site says "Could not reach the API" | API is not running | Start it in its own terminal |
| Blank page at localhost:7136 | Wrong port / missing path | Use `http://localhost:5051/swagger` |
| `Unable to create '.git/index.lock': File exists` | A git process died and left a lock | `Remove-Item .git\\index.lock` from the project root, then retry |
| Swagger shows no endpoints | App rebuilt but not restarted | Stop the app fully (Ctrl+C), then `dotnet run` again |
| Editor shows red errors but code looks fine | Language server is stale | Ctrl+Shift+P → **Developer: Reload Window**. Trust `dotnet build`, not squiggles |
| npm command blocked by PowerShell | Execution policy | Already fixed — `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |

---

## 8. Known items to deal with eventually

Tracked in `docs/README.md`, repeated here so they are not forgotten:

- **The JWT is stored in `localStorage`.** Readable by any script on the page. Move
  to an httpOnly cookie before other students use PursuitHQ.
- **Gemini free tier uses your content to improve Google's products.** Fine while you
  are the only user; needs a privacy disclosure or a paid tier before anyone else
  uses the AI features.
- **Job-board API** — Adzuna is the leading candidate for internship search.
- **Email provider domain** — needed before reminder emails can send.

---

## 9. If you are stuck

Give Claude this context and you will get straight back into it:

> I'm working on PursuitHQ. Phases 0-3 are done plus the frontend (login,
> dashboard, courses, assignments). I'm starting Phase 4, study materials and
> file uploads. Check `docs/Roadmap.md` and `docs/START-HERE.md` for where I am.

Everything is documented in `docs/`. Nothing about this project lives only in
someone's head.

# Time zone work — install order

## 1. Add the five new files

| File | Goes in |
|---|---|
| `UserClock.cs` | `Backend/PursuitHQ.API/Services/` |
| `UpdateTimeZoneDto.cs` | `Backend/PursuitHQ.API/DTOs/Auth/` |
| `timezones.js` | `Frontend/pursuithq-web/lib/` |
| `TimeZoneSelect.js` | `Frontend/pursuithq-web/components/` |
| `TimeZoneNotice.js` | `Frontend/pursuithq-web/components/` |

## 2. Run the two patch scripts from the repo root

```
python patch_tz.py
python patch_tz_fe.py
```

Between them they edit nine existing files. Each one refuses to write a file
if any anchor text is missing, so if something has changed since I last read
it you get a loud error naming the file rather than a half-patched build.
Nothing is written to a file whose anchors did not all match.

`patch_tz.py` — backend

- `Controllers/AssignmentsController.cs`
- `Controllers/DashboardController.cs`
- `Controllers/AuthController.cs`
- `Services/CalendarFeedService.cs`
- `Services/NotificationService.cs`
- `Program.cs`

`patch_tz_fe.py` — frontend

- `lib/api.js`
- `app/settings/page.js`
- `app/assignments/page.js`

## 3. Build and run

```
dotnet build
npm run dev
```

No migration. `ApplicationUser.TimeZone` already exists — it just wasn't
being used for this.

## 4. What to check

1. **Settings → Time zone** now lists every zone the browser knows, grouped by
   region, labelled `(UTC−05:00) Central Time — Chicago`. Typing "chic" jumps
   to Chicago.
2. **Change it to something a few hours west** and save. An assignment due
   later tonight should stop reading as overdue; one due a few hours ago
   should start. The dashboard and the calendar should agree with the
   assignments page — they were three separate answers before.
3. **The mismatch notice.** With a zone saved that isn't your laptop's, reload
   Settings: an amber panel offers to switch. "Keep" hides it for that pair
   only; change the saved zone again and it asks again.
4. **Register a new account.** Its zone should come from the browser rather
   than defaulting to Eastern.

## One thing I did not change

`NotificationPreference.TimeZone` is a dead column. It's written once at
registration and never read again — `NotificationsController` deliberately
reads the account's zone instead, and `NotificationService` joins to
`ApplicationUser.TimeZone`. So nothing is broken, but it looks authoritative
and isn't, which is the kind of thing that costs an hour in six months. Worth
dropping in a later migration.

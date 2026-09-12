'use client';

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { auth as authApi, notifications as notificationsApi, clearSession } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import { useTheme } from "@/components/ThemeProvider";

const THEME_OPTIONS = [
  { value: "light", label: "Light", hint: "Always light" },
  { value: "dark", label: "Dark", hint: "Always dark" },
  { value: "system", label: "System", hint: "Follow your computer" },
];

// Where students of a US university actually are. A full tz list is 400 entries
// of noise for a feature that only needs to get deadlines right.
const TIME_ZONES = [
  "America/New_York",
  "America/Chicago",
  "America/Denver",
  "America/Phoenix",
  "America/Los_Angeles",
  "America/Anchorage",
  "Pacific/Honolulu",
  "UTC",
];

const HOURS_BEFORE = [
  { value: 1, label: "1 hour before" },
  { value: 3, label: "3 hours before" },
  { value: 6, label: "6 hours before" },
  { value: 12, label: "12 hours before" },
  { value: 24, label: "A day before" },
  { value: 48, label: "Two days before" },
  { value: 72, label: "Three days before" },
  { value: 168, label: "A week before" },
];

const MINUTES_BEFORE = [
  { value: 5, label: "5 minutes before" },
  { value: 10, label: "10 minutes before" },
  { value: 15, label: "15 minutes before" },
  { value: 30, label: "30 minutes before" },
  { value: 60, label: "An hour before" },
  { value: 120, label: "Two hours before" },
];

export default function SettingsPage() {
  const { user, loading, updateUser, signOut } = useAuth();
  const { theme, setTheme } = useTheme();
  const router = useRouter();

  const [profile, setProfile] = useState(null);
  const [ready, setReady] = useState(false);

  const [notify, setNotify] = useState(null);
  const [notifyMessage, setNotifyMessage] = useState("");
  const [notifyError, setNotifyError] = useState("");
  const [savingNotify, setSavingNotify] = useState(false);
  const [testing, setTesting] = useState(false);

  const [profileMessage, setProfileMessage] = useState("");
  const [profileError, setProfileError] = useState("");
  const [savingProfile, setSavingProfile] = useState(false);

  const [passwords, setPasswords] = useState({ current: "", next: "", confirm: "" });
  const [passwordMessage, setPasswordMessage] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const [savingPassword, setSavingPassword] = useState(false);

  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (loading || !user) return;

    authApi
      .me()
      .then((me) =>
        setProfile({
          firstName: me.firstName,
          lastName: me.lastName,
          email: me.email,
          major: me.major ?? "",
          graduationYear: me.graduationYear ?? "",
          timeZone: me.timeZone || "America/New_York",
        })
      )
      .catch((err) => setProfileError(err.message))
      .finally(() => setReady(true));

    // Separate from the profile call on purpose: if notifications fail to load,
    // the rest of settings still works rather than the whole page refusing.
    notificationsApi
      .preferences()
      .then(setNotify)
      .catch((err) => setNotifyError(err.message));
  }, [loading, user]);

  async function saveNotifications(e) {
    e.preventDefault();
    setSavingNotify(true);
    setNotifyError("");
    setNotifyMessage("");

    try {
      setNotify(
        await notificationsApi.savePreferences({
          emailEnabled: notify.emailEnabled,
          assignmentRemindersEnabled: notify.assignmentRemindersEnabled,
          assignmentReminderHoursBefore: Number(notify.assignmentReminderHoursBefore),
          eventRemindersEnabled: notify.eventRemindersEnabled,
          eventReminderMinutesBefore: Number(notify.eventReminderMinutesBefore),
          dailyDigestEnabled: notify.dailyDigestEnabled,
          dailyDigestTime: notify.dailyDigestTime,
          weeklyDigestEnabled: notify.weeklyDigestEnabled,
        })
      );

      setNotifyMessage("Saved.");
    } catch (err) {
      setNotifyError(err.message);
    } finally {
      setSavingNotify(false);
    }
  }

  async function sendTest() {
    setTesting(true);
    setNotifyError("");
    setNotifyMessage("");

    try {
      const result = await notificationsApi.sendTest();
      setNotifyMessage(result.message);
    } catch (err) {
      setNotifyError(err.message);
    } finally {
      setTesting(false);
    }
  }

  async function saveProfile(e) {
    e.preventDefault();
    setSavingProfile(true);
    setProfileError("");
    setProfileMessage("");

    try {
      const updated = await authApi.updateProfile({
        firstName: profile.firstName.trim(),
        lastName: profile.lastName.trim(),
        major: profile.major.trim() || null,
        graduationYear: profile.graduationYear === "" ? null : Number(profile.graduationYear),
        timeZone: profile.timeZone,
      });

      updateUser(updated);
      setProfileMessage("Saved.");
    } catch (err) {
      setProfileError(err.message);
    } finally {
      setSavingProfile(false);
    }
  }

  async function changePassword(e) {
    e.preventDefault();
    setPasswordError("");
    setPasswordMessage("");

    if (passwords.next !== passwords.confirm) {
      setPasswordError("The new passwords do not match.");
      return;
    }

    if (passwords.next.length < 8) {
      setPasswordError("The new password has to be at least 8 characters.");
      return;
    }

    setSavingPassword(true);

    try {
      await authApi.changePassword(passwords.current, passwords.next);

      setPasswords({ current: "", next: "", confirm: "" });
      setPasswordMessage("Password changed.");
    } catch (err) {
      // Identity explains exactly what a rejected password was missing, and
      // those explanations are more useful than the summary line.
      const details = err.details ? Object.values(err.details).flat().join(" ") : "";
      setPasswordError(details || err.message);
    } finally {
      setSavingPassword(false);
    }
  }

  async function deleteAccount() {
    const typed = prompt(
      'This deletes your account and everything in it — courses, assignments, materials, and study tools. This cannot be undone.\n\nType DELETE to confirm.'
    );

    if (typed !== "DELETE") return;

    setDeleting(true);

    try {
      await authApi.deleteAccount();
      clearSession();
      router.replace("/register");
    } catch (err) {
      setProfileError(err.message);
      setDeleting(false);
    }
  }

  if (loading || !ready || !profile) {
    return <div className="mx-auto max-w-3xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const field =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";
  const label = "block text-sm font-medium text-slate-700";
  const card = "rounded-xl border border-slate-200 bg-white p-5";

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-2xl font-semibold">Settings</h1>
      <p className="mt-1 text-sm text-slate-600">
        Your account and how PursuitHQ looks.
      </p>

      {/* Appearance */}
      <section className={`mt-6 ${card}`}>
        <h2 className="font-medium text-slate-900">Appearance</h2>
        <p className="mt-1 text-sm text-slate-600">
          Saved in this browser, so each device can be set differently.
        </p>

        <div className="mt-4 grid gap-2 sm:grid-cols-3">
          {THEME_OPTIONS.map((option) => (
            <button
              key={option.value}
              onClick={() => setTheme(option.value)}
              aria-pressed={theme === option.value}
              className={`rounded-lg border px-4 py-3 text-left transition ${
                theme === option.value
                  ? "border-indigo-600 bg-indigo-50"
                  : "border-slate-300 hover:bg-slate-50"
              }`}
            >
              <span className="block text-sm font-medium text-slate-900">{option.label}</span>
              <span className="block text-xs text-slate-500">{option.hint}</span>
            </button>
          ))}
        </div>
      </section>

      {/* Profile */}
      <section className={`mt-6 ${card}`}>
        <h2 className="font-medium text-slate-900">Your information</h2>

        <form onSubmit={saveProfile} className="mt-4 grid gap-4 sm:grid-cols-2">
          <div>
            <label className={label}>First name</label>
            <input
              required
              value={profile.firstName}
              onChange={(e) => setProfile({ ...profile, firstName: e.target.value })}
              className={field}
            />
          </div>
          <div>
            <label className={label}>Last name</label>
            <input
              required
              value={profile.lastName}
              onChange={(e) => setProfile({ ...profile, lastName: e.target.value })}
              className={field}
            />
          </div>

          <div className="sm:col-span-2">
            <label className={label}>Email</label>
            <input value={profile.email} disabled className={`${field} opacity-60`} />
            <p className="mt-1 text-xs text-slate-500">
              Your email is how you sign in, so it cannot be changed here yet.
            </p>
          </div>

          <div>
            <label className={label}>Major</label>
            <input
              value={profile.major}
              onChange={(e) => setProfile({ ...profile, major: e.target.value })}
              placeholder="Optional"
              className={field}
            />
          </div>
          <div>
            <label className={label}>Graduation year</label>
            <input
              type="number"
              min="1900"
              max="2100"
              value={profile.graduationYear}
              onChange={(e) => setProfile({ ...profile, graduationYear: e.target.value })}
              placeholder="Optional"
              className={field}
            />
          </div>

          <div className="sm:col-span-2">
            <label className={label}>Time zone</label>
            <select
              value={profile.timeZone}
              onChange={(e) => setProfile({ ...profile, timeZone: e.target.value })}
              className={field}
            >
              {TIME_ZONES.map((zone) => (
                <option key={zone} value={zone}>
                  {zone.replace("_", " ")}
                </option>
              ))}
            </select>
          </div>

          {profileError && (
            <p className="sm:col-span-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {profileError}
            </p>
          )}
          {profileMessage && (
            <p className="sm:col-span-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
              {profileMessage}
            </p>
          )}

          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={savingProfile}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {savingProfile ? "Saving..." : "Save changes"}
            </button>
          </div>
        </form>
      </section>

      {/* Notifications */}
      <section className={`mt-6 ${card}`}>
        <h2 className="font-medium text-slate-900">Email notifications</h2>
        <p className="mt-1 text-sm text-slate-600">
          Choose what PursuitHQ emails you about, or turn it off entirely.
        </p>

        {notifyError && (
          <p className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {notifyError}
          </p>
        )}

        {!notify ? (
          <p className="mt-4 text-sm text-slate-500">Loading...</p>
        ) : (
          <form onSubmit={saveNotifications} className="mt-4">
            {/*
              Said before anything is switched on, not after. Turning on
              reminders and then waiting a week to discover nothing sends is a
              worse experience than being told up front.
            */}
            {!notify.deliveryConfigured && (
              <p className="mb-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                Sending is not set up on this server yet, so nothing will actually
                arrive. Your choices are saved and will be used as soon as it is.
              </p>
            )}

            <Toggle
              label="Email me reminders"
              hint="The master switch. With this off, nothing is ever sent."
              checked={notify.emailEnabled}
              onChange={(v) => setNotify({ ...notify, emailEnabled: v })}
            />

            <div
              className={`mt-4 space-y-4 border-t border-slate-200 pt-4 transition ${
                notify.emailEnabled ? "" : "pointer-events-none opacity-40"
              }`}
            >
              <div>
                <Toggle
                  label="Assignment due dates"
                  hint="One reminder per assignment, before it is due."
                  checked={notify.assignmentRemindersEnabled}
                  onChange={(v) => setNotify({ ...notify, assignmentRemindersEnabled: v })}
                />
                {notify.assignmentRemindersEnabled && (
                  <select
                    value={notify.assignmentReminderHoursBefore}
                    onChange={(e) =>
                      setNotify({ ...notify, assignmentReminderHoursBefore: e.target.value })
                    }
                    className={`${field} sm:max-w-xs`}
                  >
                    {HOURS_BEFORE.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                )}
              </div>

              <div>
                <Toggle
                  label="Classes and calendar events"
                  hint="A nudge before something on your calendar starts."
                  checked={notify.eventRemindersEnabled}
                  onChange={(v) => setNotify({ ...notify, eventRemindersEnabled: v })}
                />
                {notify.eventRemindersEnabled && (
                  <select
                    value={notify.eventReminderMinutesBefore}
                    onChange={(e) =>
                      setNotify({ ...notify, eventReminderMinutesBefore: e.target.value })
                    }
                    className={`${field} sm:max-w-xs`}
                  >
                    {MINUTES_BEFORE.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                )}
              </div>

              <div>
                <Toggle
                  label="Daily summary"
                  hint="One morning email: today's classes and what is due."
                  checked={notify.dailyDigestEnabled}
                  onChange={(v) => setNotify({ ...notify, dailyDigestEnabled: v })}
                />
                {notify.dailyDigestEnabled && (
                  <input
                    type="time"
                    value={notify.dailyDigestTime}
                    onChange={(e) => setNotify({ ...notify, dailyDigestTime: e.target.value })}
                    className={`${field} sm:max-w-xs`}
                  />
                )}
              </div>

              <Toggle
                label="Weekly summary"
                hint="Sunday evening, a look at the week ahead."
                checked={notify.weeklyDigestEnabled}
                onChange={(v) => setNotify({ ...notify, weeklyDigestEnabled: v })}
              />
            </div>

            <p className="mt-4 text-xs text-slate-500">
              Times are in {notify.timeZone.replace("_", " ")}, taken from your time
              zone above. Change it there and reminders follow.
            </p>

            {notifyMessage && (
              <p className="mt-4 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
                {notifyMessage}
              </p>
            )}

            <div className="mt-4 flex flex-wrap items-center gap-3">
              <button
                type="submit"
                disabled={savingNotify}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                {savingNotify ? "Saving..." : "Save notifications"}
              </button>

              {/*
                Goes only to your own address, so it is safe to leave enabled
                even before sending works - with no provider it prints to the
                API terminal, which is exactly what you want to see while
                setting one up.
              */}
              <button
                type="button"
                onClick={sendTest}
                disabled={testing}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
              >
                {testing ? "Sending..." : "Send a test email"}
              </button>
            </div>
          </form>
        )}
      </section>

      {/* Password */}
      <section className={`mt-6 ${card}`}>
        <h2 className="font-medium text-slate-900">Change password</h2>
        <p className="mt-1 text-sm text-slate-600">
          Your current password is required — it proves it is you at the keyboard, not
          someone who found your screen unlocked.
        </p>

        <form onSubmit={changePassword} className="mt-4 grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <label className={label}>Current password</label>
            <input
              type="password"
              required
              autoComplete="current-password"
              value={passwords.current}
              onChange={(e) => setPasswords({ ...passwords, current: e.target.value })}
              className={field}
            />
          </div>
          <div>
            <label className={label}>New password</label>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={passwords.next}
              onChange={(e) => setPasswords({ ...passwords, next: e.target.value })}
              className={field}
            />
          </div>
          <div>
            <label className={label}>Confirm new password</label>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={passwords.confirm}
              onChange={(e) => setPasswords({ ...passwords, confirm: e.target.value })}
              className={field}
            />
          </div>

          <p className="sm:col-span-2 text-xs text-slate-500">
            At least 8 characters, with an uppercase letter, a lowercase letter, and a number.
          </p>

          {passwordError && (
            <p className="sm:col-span-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {passwordError}
            </p>
          )}
          {passwordMessage && (
            <p className="sm:col-span-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
              {passwordMessage}
            </p>
          )}

          <div className="sm:col-span-2">
            <button
              type="submit"
              disabled={savingPassword}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {savingPassword ? "Changing..." : "Change password"}
            </button>
          </div>
        </form>
      </section>

      {/* Account */}
      <section className={`mt-6 ${card}`}>
        <h2 className="font-medium text-slate-900">Account</h2>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button
            onClick={signOut}
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            Sign out
          </button>

          <button
            onClick={deleteAccount}
            disabled={deleting}
            className="rounded-md border border-red-200 px-4 py-2 text-sm font-medium text-red-600 transition hover:bg-red-50 disabled:opacity-50"
          >
            {deleting ? "Deleting..." : "Delete account"}
          </button>
        </div>

        <p className="mt-3 text-xs text-slate-500">
          Deleting removes your courses, assignments, materials, and study tools. It cannot
          be undone.
        </p>
      </section>
    </div>
  );
}

/**
 * A labelled on/off switch.
 *
 * A real button with role="switch" rather than a styled checkbox, so it
 * announces itself properly to a screen reader and works from the keyboard
 * without any extra handling.
 */
function Toggle({ label, hint, checked, onChange }) {
  return (
    <div className="flex items-start justify-between gap-4">
      <div className="min-w-0">
        <span className="block text-sm font-medium text-slate-900">{label}</span>
        {hint && <span className="block text-xs text-slate-500">{hint}</span>}
      </div>

      <button
        type="button"
        role="switch"
        aria-checked={checked}
        aria-label={label}
        onClick={() => onChange(!checked)}
        className={`relative mt-0.5 h-6 w-11 shrink-0 rounded-full transition ${
          checked ? "bg-indigo-600" : "bg-slate-300"
        }`}
      >
        <span
          className={`absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-all ${
            checked ? "left-[1.375rem]" : "left-0.5"
          }`}
        />
      </button>
    </div>
  );
}

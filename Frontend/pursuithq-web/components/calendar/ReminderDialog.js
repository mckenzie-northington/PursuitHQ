'use client';

import { useEffect, useState } from "react";

/**
 * A reminder: something to do on a day, with no time.
 *
 * Its own dialog rather than a mode inside EventDialog, because almost nothing
 * is shared - no start and end, no repeats, no location, no colour. Three
 * fields in a box of their own is clearer than a form that hides two thirds of
 * itself.
 *
 * The two dialogs carry the same tabs at the top, so switching between them
 * reads as one composer with two sides.
 */
export default function ReminderDialog({
  initial,
  onSave,
  onDelete,
  onClose,
  onSwitchKind,
  busy,
  error,
}) {
  const [form, setForm] = useState(initial);
  const [localError, setLocalError] = useState("");

  const isEdit = initial.id != null;

  useEffect(() => {
    function onKeyDown(e) {
      if (e.key === "Escape") onClose();
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  function set(patch) {
    setForm((f) => ({ ...f, ...patch }));
  }

  function submit(e) {
    e.preventDefault();
    setLocalError("");

    if (!form.title.trim()) {
      setLocalError("Give the reminder a title.");
      return;
    }

    if (!form.date) {
      setLocalError("Pick a day.");
      return;
    }

    onSave({
      title: form.title.trim(),
      notes: form.notes.trim() || null,
      date: form.date,
      isCompleted: form.isCompleted ?? false,
    });
  }

  const field =
    "w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";
  const label = "block text-xs font-medium uppercase tracking-wide text-slate-500";

  const message = localError || error;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/40 p-4 sm:items-center">
      <div className="absolute inset-0" onClick={onClose} aria-hidden="true" />

      <form
        onSubmit={submit}
        className="relative w-full max-w-lg rounded-xl border border-slate-200 bg-white shadow-xl"
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-3">
          <ComposerTabs kind="reminder" onSwitchKind={onSwitchKind} isEdit={isEdit} />
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
            aria-label="Close"
          >
            &#10005;
          </button>
        </div>

        <div className="space-y-4 px-5 py-4">
          <input
            autoFocus
            required
            value={form.title}
            onChange={(e) => set({ title: e.target.value })}
            placeholder="Email my advisor"
            className="w-full border-b border-slate-300 pb-2 text-lg font-medium outline-none transition placeholder:text-slate-400 focus:border-indigo-500"
          />

          <div>
            <label className={label}>Day</label>
            <input
              type="date"
              required
              value={form.date}
              onChange={(e) => set({ date: e.target.value })}
              className={`${field} mt-1`}
            />
            <p className="mt-1 text-xs text-slate-500">
              Reminders belong to a day, not a time. This one sits at the top of
              the calendar with your assignments, and you tick it off there.
            </p>
          </div>

          <div>
            <label className={label}>Notes</label>
            <textarea
              rows={3}
              value={form.notes}
              onChange={(e) => set({ notes: e.target.value })}
              placeholder="Optional"
              className={`${field} mt-1`}
            />
          </div>

          {isEdit && (
            <label className="flex w-fit items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                checked={form.isCompleted ?? false}
                onChange={(e) => set({ isCompleted: e.target.checked })}
                className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
              />
              Done
            </label>
          )}

          {message && (
            <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {message}
            </p>
          )}
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-slate-200 px-5 py-3">
          {isEdit ? (
            <button
              type="button"
              onClick={onDelete}
              className="text-sm font-medium text-red-600 transition hover:text-red-700"
            >
              Delete
            </button>
          ) : (
            <span />
          )}

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-slate-300 px-4 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={busy}
              className="rounded-md bg-indigo-600 px-4 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Saving..." : isEdit ? "Save" : "Add reminder"}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}

/**
 * The Event / Reminder tabs.
 *
 * Hidden while editing: an existing record is one or the other, and offering to
 * switch would raise the question of what happens to what you typed.
 */
export function ComposerTabs({ kind, onSwitchKind, isEdit }) {
  if (isEdit) {
    return (
      <h2 className="text-sm font-semibold text-slate-900">
        {kind === "reminder" ? "Edit reminder" : "Edit event"}
      </h2>
    );
  }

  const tab = (value, labelText) => (
    <button
      key={value}
      type="button"
      onClick={() => value !== kind && onSwitchKind?.(value)}
      aria-pressed={value === kind}
      className={`rounded-md px-3 py-1 text-sm font-medium transition ${
        value === kind ? "bg-indigo-600 text-white" : "text-slate-600 hover:bg-slate-100"
      }`}
    >
      {labelText}
    </button>
  );

  return (
    <div className="flex items-center gap-1">
      {tab("event", "Event")}
      {tab("reminder", "Reminder")}
    </div>
  );
}

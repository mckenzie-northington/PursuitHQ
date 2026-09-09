'use client';

import { useEffect, useState } from "react";
import { WEEKDAY_CODES, fromIso, longDate } from "@/lib/calendar";
import ColorPicker from "@/components/ColorPicker";
import { useSavedColors } from "@/lib/useSavedColors";

/**
 * The add/edit event dialog.
 *
 * Deliberately shaped like the one in Google Calendar: it opens over the
 * calendar already knowing the day and hour you clicked, the title field has
 * focus so you can start typing immediately, and Escape closes it.
 *
 * The parent gives this a `key` tied to what is being edited, so React
 * remounts it and the form starts from `initial` every time it opens. That is
 * simpler and less bug-prone than syncing props into state with an effect.
 */
export default function EventDialog({ initial, onSave, onDelete, onClose, busy, error }) {
  const [form, setForm] = useState(initial);
  const [localError, setLocalError] = useState("");
  const palette = useSavedColors();

  const isEdit = initial.id != null;

  // Escape closes, which is what anyone expects from a modal.
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

  function toggleDay(code) {
    setForm((f) => ({
      ...f,
      recurrenceDays: f.recurrenceDays.includes(code)
        ? f.recurrenceDays.filter((d) => d !== code)
        : [...f.recurrenceDays, code],
    }));
  }

  function submit(e) {
    e.preventDefault();
    setLocalError("");

    const endDate = form.isAllDay ? form.endDate : form.startDate;

    if (form.isAllDay) {
      if (fromIso(endDate) < fromIso(form.startDate)) {
        setLocalError("The last day has to be on or after the first day.");
        return;
      }
    } else if (form.endTime <= form.startTime) {
      setLocalError("The end time has to be after the start time.");
      return;
    }

    if (form.isRecurring && form.recurrenceDays.length === 0) {
      setLocalError("Pick at least one day for a repeating event.");
      return;
    }

    onSave({
      title: form.title.trim(),
      description: form.description.trim() || null,
      isAllDay: form.isAllDay,
      startDateTime: form.isAllDay
        ? `${form.startDate}T00:00:00`
        : `${form.startDate}T${form.startTime}:00`,
      endDateTime: form.isAllDay
        ? `${endDate}T00:00:00`
        : `${endDate}T${form.endTime}:00`,
      location: form.location.trim() || null,
      eventType: Number(form.eventType),
      isRecurring: form.isRecurring,
      recurrenceRule: form.isRecurring
        ? `FREQ=WEEKLY;BYDAY=${form.recurrenceDays.join(",")}`
        : null,
      colorHex: form.colorHex,
    });
  }

  const field =
    "w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";
  const label = "block text-xs font-medium uppercase tracking-wide text-slate-500";

  const message = localError || error;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/40 p-4 sm:items-center">
      {/* Clicking the backdrop closes; clicking the card must not. */}
      <div className="absolute inset-0" onClick={onClose} aria-hidden="true" />

      <form
        onSubmit={submit}
        className="relative w-full max-w-lg rounded-xl border border-slate-200 bg-white shadow-xl"
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-3">
          <h2 className="text-sm font-semibold text-slate-900">
            {isEdit ? "Edit event" : "New event"}
          </h2>
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
            placeholder="Add a title"
            className="w-full border-b border-slate-300 pb-2 text-lg font-medium outline-none transition placeholder:text-slate-400 focus:border-indigo-500"
          />

          <label className="flex w-fit items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              checked={form.isAllDay}
              onChange={(e) => set({ isAllDay: e.target.checked })}
              className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
            />
            All day
          </label>

          {form.isAllDay ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className={label}>First day</label>
                <input
                  type="date"
                  required
                  value={form.startDate}
                  onChange={(e) => set({ startDate: e.target.value })}
                  className={`mt-1 ${field}`}
                />
              </div>
              <div>
                <label className={label}>Last day</label>
                <input
                  type="date"
                  required
                  value={form.endDate}
                  onChange={(e) => set({ endDate: e.target.value })}
                  className={`mt-1 ${field}`}
                />
              </div>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-3">
              <div>
                <label className={label}>Date</label>
                <input
                  type="date"
                  required
                  value={form.startDate}
                  onChange={(e) => set({ startDate: e.target.value })}
                  className={`mt-1 ${field}`}
                />
              </div>
              <div>
                <label className={label}>Start</label>
                <input
                  type="time"
                  required
                  value={form.startTime}
                  onChange={(e) => set({ startTime: e.target.value })}
                  className={`mt-1 ${field}`}
                />
              </div>
              <div>
                <label className={label}>End</label>
                <input
                  type="time"
                  required
                  value={form.endTime}
                  onChange={(e) => set({ endTime: e.target.value })}
                  className={`mt-1 ${field}`}
                />
              </div>
            </div>
          )}

          {!form.isAllDay && form.startDate && (
            <p className="-mt-2 text-xs text-slate-500">{longDate(form.startDate)}</p>
          )}

          <div>
            <label className={label}>Repeat</label>
            <label className="mt-1 flex w-fit items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                checked={form.isRecurring}
                onChange={(e) =>
                  set({
                    isRecurring: e.target.checked,
                    // Default to the day it starts on, which is almost always
                    // what a weekly shift or club meeting means.
                    recurrenceDays:
                      e.target.checked && form.recurrenceDays.length === 0 && form.startDate
                        ? [WEEKDAY_CODES[fromIso(form.startDate).getDay()][0]]
                        : form.recurrenceDays,
                  })
                }
                className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
              />
              Repeats weekly
            </label>

            {form.isRecurring && (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {WEEKDAY_CODES.map(([code, name]) => (
                  <button
                    key={code}
                    type="button"
                    onClick={() => toggleDay(code)}
                    className={`h-8 w-11 rounded-md border text-xs font-medium transition ${
                      form.recurrenceDays.includes(code)
                        ? "border-indigo-600 bg-indigo-600 text-white"
                        : "border-slate-300 text-slate-700 hover:bg-slate-50"
                    }`}
                  >
                    {name}
                  </button>
                ))}
              </div>
            )}
          </div>

          <div>
            <label className={label}>Where</label>
            <input
              value={form.location}
              onChange={(e) => set({ location: e.target.value })}
              placeholder="Optional"
              className={`mt-1 ${field}`}
            />
          </div>

          <div>
            <label className={label}>Color</label>
            <ColorPicker
              value={form.colorHex}
              onChange={(colorHex) => set({ colorHex })}
              saved={palette.colors}
              onSave={palette.add}
              onRemove={palette.remove}
            />
          </div>

          <div>
            <label className={label}>Notes</label>
            <textarea
              rows={2}
              value={form.description}
              onChange={(e) => set({ description: e.target.value })}
              placeholder="Optional"
              className={`mt-1 ${field}`}
            />
          </div>

          {message && (
            <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {message}
            </p>
          )}
        </div>

        <div className="flex items-center justify-between gap-2 border-t border-slate-200 px-5 py-3">
          <div>
            {isEdit && (
              <button
                type="button"
                onClick={onDelete}
                disabled={busy}
                className="rounded-md px-3 py-2 text-sm font-medium text-slate-500 transition hover:bg-red-50 hover:text-red-600 disabled:opacity-50"
              >
                Delete
              </button>
            )}
          </div>

          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={busy}
              className="rounded-md bg-indigo-600 px-5 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Saving..." : "Save"}
            </button>
          </div>
        </div>
      </form>
    </div>
  );
}

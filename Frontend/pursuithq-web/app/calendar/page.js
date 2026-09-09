'use client';

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  assignments as assignmentsApi,
  calendar as calendarApi,
  calendarEvents as eventsApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import MonthGrid from "@/components/calendar/MonthGrid";
import TimeGrid from "@/components/calendar/TimeGrid";
import EventDialog from "@/components/calendar/EventDialog";
import AssignmentCheckbox from "@/components/calendar/AssignmentCheckbox";
import {
  TYPE,
  TYPE_LABEL,
  addDays,
  colorOf,
  fromIso,
  isDone,
  iso,
  longDate,
  minutesOf,
  startOfToday,
  timeRangeOf,
  toTimeValue,
  todayIso,
} from "@/lib/calendar";

const VIEWS = ["month", "week", "day"];

export default function CalendarPage() {
  const { user, loading } = useAuth();
  const router = useRouter();

  const [view, setView] = useState("month");
  const [anchor, setAnchor] = useState(() => startOfToday());
  const [items, setItems] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  // The event dialog. `null` means closed; otherwise it holds the form to
  // start from, and the dialog is keyed on it so it remounts fresh each time.
  const [dialog, setDialog] = useState(null);
  const [dialogError, setDialogError] = useState("");
  const [busy, setBusy] = useState(false);

  // A class, assignment or study session that was clicked - those are not
  // editable here, so they get a read-only card instead of the dialog.
  const [detail, setDetail] = useState(null);

  const { rangeStart, rangeEnd, gridDays } = useMemo(
    () => buildRange(anchor, view),
    [anchor, view]
  );

  const refresh = useCallback(async () => {
    try {
      const data = await calendarApi.range(iso(rangeStart), iso(rangeEnd));
      setItems(data);
      setError("");
    } catch (err) {
      const sources = err.details ? Object.values(err.details).flat().join(" | ") : "";
      setError(sources ? `${err.message} - ${sources}` : err.message);
    } finally {
      setReady(true);
    }
  }, [rangeStart, rangeEnd]);

  useEffect(() => {
    if (loading || !user) return;
    refresh();
  }, [loading, user, refresh]);

  // Group once, so each day cell is a lookup instead of a filter over everything.
  const byDate = useMemo(() => {
    const map = {};
    for (const item of items) (map[item.date] ??= []).push(item);
    return map;
  }, [items]);

  // --- navigation ----------------------------------------------------------

  function move(step) {
    setAnchor((current) => {
      if (view === "month") {
        const next = new Date(current);
        next.setDate(1);
        next.setMonth(next.getMonth() + step);
        return next;
      }
      return addDays(current, step * (view === "week" ? 7 : 1));
    });
  }

  function openDay(dateStr) {
    setAnchor(fromIso(dateStr));
    setView("day");
  }

  // --- the event dialog ----------------------------------------------------

  function startNewEvent(dateStr, timeStr) {
    setDialogError("");
    setDialog(blankEvent(dateStr ?? todayIso(), timeStr));
  }

  async function openItem(item) {
    // A class on the calendar is a shortcut into the course itself - that is
    // where its notes, slides and handouts live.
    if (item.type === TYPE.CLASS && item.courseId) {
      router.push(`/courses/${item.courseId}/materials`);
      return;
    }

    if (item.type !== TYPE.EVENT) {
      setDetail(item);
      return;
    }

    // The calendar item only carries what the grid draws, so the full record
    // has to be fetched before the dialog can show notes, repeats and kind.
    try {
      const full = await eventsApi.get(item.sourceId);
      setDialogError("");
      setDialog(eventToForm(full));
    } catch (err) {
      setError(err.message);
    }
  }

  async function saveEvent(payload) {
    setBusy(true);
    setDialogError("");

    try {
      if (dialog.id != null) await eventsApi.update(dialog.id, payload);
      else await eventsApi.create(payload);

      setDialog(null);
      await refresh();
    } catch (err) {
      setDialogError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function deleteEvent() {
    if (!confirm(`Delete "${dialog.title}"?`)) return;

    setBusy(true);
    try {
      await eventsApi.remove(dialog.id);
      setDialog(null);
      await refresh();
    } catch (err) {
      setDialogError(err.message);
    } finally {
      setBusy(false);
    }
  }

  // --- assignment checkbox -------------------------------------------------

  /**
   * Ticking the box updates the screen first and saves afterwards, so the tick
   * feels instant. If the save fails we say so and reload, which puts the box
   * back where the database says it belongs.
   */
  async function toggleAssignment(item) {
    const nextStatus = isDone(item) ? 0 : 2;
    const nextLabel = nextStatus === 2 ? "Completed" : "NotStarted";

    setItems((current) =>
      current.map((i) =>
        i.id === item.id
          ? { ...i, status: nextLabel, isOverdue: nextStatus === 2 ? false : i.isOverdue }
          : i
      )
    );
    setDetail((d) => (d && d.id === item.id ? { ...d, status: nextLabel } : d));

    try {
      await assignmentsApi.setStatus(item.sourceId, nextStatus);
    } catch (err) {
      setError(`Could not update "${item.title}" - ${err.message}`);
      await refresh();
    }
  }

  // --- render --------------------------------------------------------------

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-6xl px-6 py-10">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Calendar</h1>
          <p className="mt-1 text-sm text-slate-600">
            Classes, assignment due dates, study sessions, and everything else.
            Click any empty space to add an event.
          </p>
        </div>
        <button
          onClick={() => startNewEvent(view === "day" ? iso(anchor) : todayIso())}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
        >
          Add event
        </button>
      </div>

      <div className="mt-6 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <button onClick={() => move(-1)} className={navBtn} aria-label="Previous">
            &lsaquo;
          </button>
          <button onClick={() => setAnchor(startOfToday())} className={navBtn}>
            Today
          </button>
          <button onClick={() => move(1)} className={navBtn} aria-label="Next">
            &rsaquo;
          </button>

          <h2 className="ml-2 text-lg font-medium text-slate-900">
            {headingFor(anchor, view, rangeStart, rangeEnd)}
          </h2>
        </div>

        <div className="flex rounded-md border border-slate-300 p-0.5">
          {VIEWS.map((v) => (
            <button
              key={v}
              onClick={() => setView(v)}
              className={`rounded px-3 py-1 text-sm font-medium capitalize transition ${
                view === v ? "bg-indigo-600 text-white" : "text-slate-600 hover:bg-slate-100"
              }`}
            >
              {v}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      <div className="mt-4">
        {view === "month" ? (
          <MonthGrid
            gridDays={gridDays}
            anchorMonth={anchor.getMonth()}
            byDate={byDate}
            onDayNumberClick={openDay}
            onEmptyClick={(dateStr) => startNewEvent(dateStr)}
            onItemClick={openItem}
            onToggleAssignment={toggleAssignment}
          />
        ) : (
          <TimeGrid
            days={gridDays}
            byDate={byDate}
            onSlotClick={startNewEvent}
            onItemClick={openItem}
            onToggleAssignment={toggleAssignment}
            onDayClick={openDay}
          />
        )}
      </div>

      {view === "day" && (
        <DaySchedule
          dateStr={iso(anchor)}
          items={byDate[iso(anchor)] ?? []}
          onItemClick={openItem}
          onToggleAssignment={toggleAssignment}
        />
      )}

      {dialog && (
        <EventDialog
          key={dialog.key}
          initial={dialog}
          busy={busy}
          error={dialogError}
          onSave={saveEvent}
          onDelete={deleteEvent}
          onClose={() => setDialog(null)}
        />
      )}

      {detail && <ItemDetail item={detail} onClose={() => setDetail(null)} onToggle={toggleAssignment} />}
    </div>
  );
}

const navBtn =
  "rounded-md border border-slate-300 px-2.5 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50";

/** The written-out version of a day, under the hour grid. */
function DaySchedule({ dateStr, items, onItemClick, onToggleAssignment }) {
  return (
    <div className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
      <h3 className="font-medium text-slate-900">{longDate(dateStr)}</h3>

      {items.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">Nothing scheduled.</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {items.map((item) => {
            const assignment = item.type === TYPE.ASSIGNMENT;
            const done = isDone(item);

            return (
              <li key={item.id} className="flex items-start gap-3 py-2.5">
                {assignment ? (
                  <span className="mt-0.5">
                    <AssignmentCheckbox checked={done} onChange={() => onToggleAssignment(item)} />
                  </span>
                ) : (
                  <span
                    className="mt-1.5 h-3 w-3 shrink-0 rounded-sm"
                    style={{ backgroundColor: colorOf(item) }}
                  />
                )}

                <button
                  type="button"
                  onClick={() => onItemClick(item)}
                  className="min-w-0 flex-1 text-left"
                >
                  <p
                    className={`truncate font-medium text-slate-900 ${
                      done ? "text-slate-400 line-through" : ""
                    }`}
                  >
                    {item.title}
                    {item.isOverdue && !done && (
                      <span className="ml-2 text-xs font-medium text-red-600">Overdue</span>
                    )}
                  </p>
                  <p className="truncate text-sm text-slate-500">
                    {TYPE_LABEL[item.type]}
                    {item.subtitle && ` · ${item.subtitle}`}
                    {item.location && ` · ${item.location}`}
                    {` · ${timeRangeOf(item)}`}
                  </p>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

/** Read-only card for the things the calendar shows but does not own. */
function ItemDetail({ item, onClose, onToggle }) {
  const assignment = item.type === TYPE.ASSIGNMENT;
  const done = isDone(item);

  const links = [
    assignment && { href: "/assignments", label: "Open in Assignments" },
    item.courseId && {
      href: `/courses/${item.courseId}/materials`,
      label: "Course materials",
    },
  ].filter(Boolean);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
      <div className="absolute inset-0" onClick={onClose} aria-hidden="true" />

      <div className="relative w-full max-w-sm rounded-xl border border-slate-200 bg-white shadow-xl">
        <div
          className="h-1.5 rounded-t-xl"
          style={{ backgroundColor: colorOf(item) }}
        />
        <div className="px-5 py-4">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {TYPE_LABEL[item.type]}
          </p>
          <h2 className={`mt-1 text-lg font-medium ${done ? "text-slate-400 line-through" : "text-slate-900"}`}>
            {item.title}
          </h2>

          <dl className="mt-3 space-y-1.5 text-sm text-slate-600">
            {item.subtitle && <div>{item.subtitle}</div>}
            {item.location && <div>{item.location}</div>}
            <div>{longDate(item.date)}</div>
            <div>{timeRangeOf(item)}</div>
            {item.isOverdue && !done && <div className="font-medium text-red-600">Overdue</div>}
          </dl>

          {assignment && (
            <label className="mt-4 flex w-fit cursor-pointer items-center gap-2 text-sm font-medium text-slate-700">
              <AssignmentCheckbox checked={done} onChange={() => onToggle(item)} />
              {done ? "Completed" : "Mark as done"}
            </label>
          )}
        </div>

        <div className="flex items-center justify-between gap-2 border-t border-slate-200 px-5 py-3">
          <div className="flex flex-wrap gap-4">
            {links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-sm font-medium text-indigo-600 hover:underline"
              >
                {link.label}
              </Link>
            ))}
          </div>
          <button
            onClick={onClose}
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

// --- helpers ---------------------------------------------------------------

/**
 * The days to render, and the span to fetch.
 *
 * Month view shows whole weeks, so it usually includes a few days either side
 * of the month - those are fetched too, otherwise the trailing cells look
 * empty when they are not.
 */
function buildRange(anchor, view) {
  let start;
  let dayCount;

  if (view === "day") {
    start = new Date(anchor);
    dayCount = 1;
  } else if (view === "week") {
    start = addDays(anchor, -anchor.getDay());
    dayCount = 7;
  } else {
    const firstOfMonth = new Date(anchor.getFullYear(), anchor.getMonth(), 1);
    start = addDays(firstOfMonth, -firstOfMonth.getDay());

    const lastOfMonth = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0);
    const end = addDays(lastOfMonth, 6 - lastOfMonth.getDay());

    dayCount = Math.round((end - start) / 86400000) + 1;
  }

  start.setHours(0, 0, 0, 0);

  const gridDays = Array.from({ length: dayCount }, (_, i) => addDays(start, i));

  return { rangeStart: gridDays[0], rangeEnd: gridDays[gridDays.length - 1], gridDays };
}

function headingFor(anchor, view, rangeStart, rangeEnd) {
  if (view === "day") {
    return anchor.toLocaleDateString(undefined, {
      weekday: "long",
      month: "long",
      day: "numeric",
      year: "numeric",
    });
  }

  if (view === "month") {
    return anchor.toLocaleDateString(undefined, { month: "long", year: "numeric" });
  }

  const sameMonth = rangeStart.getMonth() === rangeEnd.getMonth();
  const startLabel = rangeStart.toLocaleDateString(undefined, { month: "short", day: "numeric" });
  const endLabel = rangeEnd.toLocaleDateString(undefined, {
    month: sameMonth ? undefined : "short",
    day: "numeric",
    year: "numeric",
  });

  return `${startLabel} - ${endLabel}`;
}

/** A fresh event form, starting at the slot that was clicked. */
function blankEvent(dateStr, timeStr) {
  const start = timeStr || "09:00";
  const end = toTimeValue((minutesOf(start) ?? 540) + 60);

  return {
    key: `new-${dateStr}-${start}-${Date.now()}`,
    id: null,
    title: "",
    isAllDay: false,
    startDate: dateStr,
    endDate: dateStr,
    startTime: start,
    endTime: end,
    eventType: 4,
    isRecurring: false,
    recurrenceDays: [],
    location: "",
    colorHex: "#0ea5e9",
    description: "",
  };
}

/** Turns a saved event back into the shape the dialog edits. */
function eventToForm(dto) {
  return {
    key: `event-${dto.id}`,
    id: dto.id,
    title: dto.title,
    isAllDay: dto.isAllDay,
    startDate: dto.startDateTime.slice(0, 10),
    endDate: dto.endDateTime.slice(0, 10),
    startTime: dto.startDateTime.slice(11, 16),
    endTime: dto.endDateTime.slice(11, 16),
    eventType: dto.eventType,
    isRecurring: dto.isRecurring,
    recurrenceDays: parseByDay(dto.recurrenceRule),
    location: dto.location || "",
    colorHex: dto.colorHex || "#0ea5e9",
    description: dto.description || "",
  };
}

function parseByDay(rule) {
  const match = /BYDAY=([^;]*)/i.exec(rule || "");
  return match ? match[1].split(",").filter(Boolean) : [];
}

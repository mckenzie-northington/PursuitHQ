'use client';

import { useEffect, useState } from "react";
import { assignments as assignmentsApi, courses as coursesApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import AssignmentCheckbox from "@/components/calendar/AssignmentCheckbox";

// Matches the AssignmentStatus enum in the API.
const STATUS = { 0: "Not started", 1: "In progress", 2: "Completed" };

const BLANK = { courseId: "", title: "", description: "", dueDate: "", status: 0, grade: "" };

/**
 * Turns what the API sends into what a datetime-local input wants.
 *
 * The input only accepts "YYYY-MM-DDTHH:mm" and silently shows blank for
 * anything else - including the seconds the API includes. Cutting the string
 * rather than going through Date() on purpose: these are wall-clock times, and
 * parsing then re-formatting invites a time-zone shift into a value that was
 * never an instant to begin with.
 */
function toInputValue(dueDate) {
  if (!dueDate) return "";
  const [date, time = "00:00"] = String(dueDate).split("T");
  return `${date}T${time.slice(0, 5)}`;
}

/**
 * A clock that ticks, so anything comparing against "now" stays true.
 *
 * The API sends an `isOverdue` flag, but it is a snapshot of the instant the
 * list was fetched: open this page at 9am and something due at noon would still
 * read as on time at 3pm, because nothing asked again. Working it out here from
 * the due date means the deadline flips by itself, to the minute, without a
 * reload.
 *
 * Every minute rather than every second - a deadline is never so precise that
 * the second matters, and a re-render a second is a waste of a battery.
 */
function useNow(intervalMs = 60_000) {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);

  return now;
}

/**
 * How late, in words.
 *
 * "Overdue" alone does not say whether you missed it by ten minutes or ten
 * days, and those call for very different reactions.
 */
function describeLate(dueDate, now) {
  const minutes = Math.floor((now - dueDate) / 60_000);

  if (minutes < 60) return minutes <= 1 ? "just now" : `${minutes} min ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return hours === 1 ? "1 hour ago" : `${hours} hours ago`;

  const days = Math.floor(hours / 24);
  return days === 1 ? "yesterday" : `${days} days ago`;
}

export default function AssignmentsPage() {
  const { user, loading } = useAuth();
  const [items, setItems] = useState([]);
  const [courses, setCourses] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState({ courseId: "", status: "" });

  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(BLANK);
  const [busy, setBusy] = useState(false);

  /** The assignment being edited, or null when adding a new one. */
  const [editingId, setEditingId] = useState(null);

  const now = useNow();

  async function refresh() {
    try {
      const [a, c] = await Promise.all([assignmentsApi.list(filter), coursesApi.list()]);
      setItems(a);
      setCourses(c);
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }

  useEffect(() => {
    if (loading || !user) return;
    refresh();
  }, [loading, user, filter.courseId, filter.status]);

  function startAdding() {
    setEditingId(null);
    setForm(BLANK);
    setShowForm(true);
  }

  function startEditing(a) {
    setEditingId(a.id);
    setForm({
      courseId: String(a.courseId ?? ""),
      title: a.title ?? "",
      description: a.description ?? "",
      dueDate: toInputValue(a.dueDate),
      status: a.status ?? 0,
      grade: a.grade ?? "",
    });
    setShowForm(true);
    setError("");
  }

  function closeForm() {
    setShowForm(false);
    setEditingId(null);
    setForm(BLANK);
  }

  async function saveAssignment(e) {
    e.preventDefault();
    setBusy(true);
    setError("");

    // Sent exactly as typed. Converting to UTC here would store a different
    // wall-clock time than the student picked, and an 11:59 PM deadline would
    // land on the next day in the calendar.
    const payload = {
      title: form.title,
      description: form.description || null,
      dueDate: form.dueDate,
      status: Number(form.status),
    };

    try {
      if (editingId) {
        // No courseId: the API's update deliberately does not move an
        // assignment between courses, so sending one would be ignored and
        // look like a bug.
        await assignmentsApi.update(editingId, {
          ...payload,
          grade: form.grade.trim() || null,
        });
      } else {
        await assignmentsApi.create({ ...payload, courseId: Number(form.courseId) });
      }

      closeForm();
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  /**
   * Saves a new status and updates the row in place.
   *
   * This patches only the status rather than sending a whole assignment back,
   * so nothing else can be overwritten with a stale value on the way through.
   */
  async function setStatus(a, next) {
    const previous = items;

    setItems((current) =>
      current.map((row) =>
        row.id === a.id
          ? { ...row, status: next }
          : row
      )
    );

    try {
      await assignmentsApi.setStatus(a.id, next);
    } catch (err) {
      setItems(previous);
      setError(err.message);
    }
  }

  function toggleDone(a) {
    setStatus(a, a.status === 2 ? 0 : 2);
  }

  function cycleStatus(a) {
    setStatus(a, a.status === 2 ? 0 : a.status + 1);
  }

  async function remove(a) {
    if (!confirm(`Delete "${a.title}"?`)) return;
    try {
      await assignmentsApi.remove(a.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-5xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const input =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Assignments</h1>
          <p className="mt-1 text-sm text-slate-600">Everything due across your courses.</p>
        </div>
        <button
          onClick={() => (showForm ? closeForm() : startAdding())}
          disabled={courses.length === 0}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          title={courses.length === 0 ? "Add a course first" : ""}
        >
          Add assignment
        </button>
      </div>

      {courses.length === 0 && (
        <div className="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Add a course first — assignments belong to a course.
        </div>
      )}

      {error && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
      )}

      {showForm && courses.length > 0 && (
        <form onSubmit={saveAssignment} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-medium">{editingId ? "Edit assignment" : "New assignment"}</h2>

          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <label className="block text-sm font-medium text-slate-700">Course</label>
              <select
                required
                disabled={editingId !== null}
                value={form.courseId}
                onChange={(e) => setForm({ ...form, courseId: e.target.value })}
                className={`${input} ${editingId !== null ? "opacity-60" : ""}`}
              >
                <option value="">Select a course</option>
                {courses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
              {editingId !== null && (
                <p className="mt-1 text-xs text-slate-500">
                  An assignment cannot be moved to another course. Delete it and add it again.
                </p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Title</label>
              <input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} className={input} placeholder="Binary Tree Project" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Due date</label>
              <input type="datetime-local" required value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} className={input} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Status</label>
              <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })} className={input}>
                {Object.entries(STATUS).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
              </select>
            </div>
            {editingId !== null && (
              <div>
                <label className="block text-sm font-medium text-slate-700">Grade</label>
                <input
                  value={form.grade}
                  onChange={(e) => setForm({ ...form, grade: e.target.value })}
                  className={input}
                  placeholder="94, A-, optional"
                />
              </div>
            )}
            <div className="sm:col-span-2">
              <label className="block text-sm font-medium text-slate-700">Description</label>
              <textarea rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className={input} placeholder="Optional" />
            </div>
          </div>

          <div className="mt-5 flex gap-2">
            <button type="submit" disabled={busy} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {busy ? "Saving..." : editingId ? "Save changes" : "Create assignment"}
            </button>
            <button type="button" onClick={closeForm} className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
              Cancel
            </button>
          </div>
        </form>
      )}

      <div className="mt-8 flex flex-wrap gap-3">
        <select value={filter.courseId} onChange={(e) => setFilter({ ...filter, courseId: e.target.value })} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm">
          <option value="">All courses</option>
          {courses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select value={filter.status} onChange={(e) => setFilter({ ...filter, status: e.target.value })} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm">
          <option value="">All statuses</option>
          {Object.entries(STATUS).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
        </select>
      </div>

      <div className="mt-4">
        {items.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center text-slate-600">
            No assignments to show.
          </div>
        ) : (
          <ul className="divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
            {items.map((a) => {
              // Worked out here rather than trusting a.isOverdue, which was
              // true (or not) whenever the list happened to load.
              const dueDate = new Date(a.dueDate);
              const overdue = a.status !== 2 && dueDate < now;

              return (
              <li key={a.id} className="flex items-center justify-between gap-4 px-4 py-3">
                <div className="flex min-w-0 items-center gap-3">
                  <AssignmentCheckbox checked={a.status === 2} onChange={() => toggleDone(a)} />
                  <div className="min-w-0">
                    <p
                      className={`truncate font-medium ${
                        a.status === 2 ? "text-slate-400 line-through" : "text-slate-900"
                      }`}
                    >
                      {a.title}
                    </p>
                    <p className="text-sm text-slate-500">
                      {a.courseName} · due {dueDate.toLocaleString()}
                      {overdue && (
                        <span className="ml-2 font-medium text-red-600">
                          Overdue · {describeLate(dueDate, now)}
                        </span>
                      )}
                    </p>
                  </div>
                </div>

                <div className="flex shrink-0 items-center gap-2">
                  <button
                    onClick={() => cycleStatus(a)}
                    className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                      a.status === 2
                        ? "bg-green-100 text-green-700 hover:bg-green-200"
                        : a.status === 1
                        ? "bg-amber-100 text-amber-700 hover:bg-amber-200"
                        : "bg-slate-100 text-slate-700 hover:bg-slate-200"
                    }`}
                    title="Click to change status"
                  >
                    {STATUS[a.status]}
                  </button>
                  <button
                    onClick={() => startEditing(a)}
                    className="text-sm text-slate-500 hover:text-indigo-600"
                  >
                    Edit
                  </button>
                  <button onClick={() => remove(a)} className="text-sm text-slate-400 hover:text-red-600">
                    Delete
                  </button>
                </div>
              </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}

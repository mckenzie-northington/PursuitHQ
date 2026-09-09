'use client';

import { useEffect, useState } from "react";
import Link from "next/link";
import { courses as coursesApi } from "@/lib/api";
import { formatTime } from "@/lib/calendar";
import ColorPicker from "@/components/ColorPicker";
import { useSavedColors } from "@/lib/useSavedColors";
import { useAuth } from "@/components/AuthProvider";

const DAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const EMPTY_COURSE = {
  name: "",
  professor: "",
  semester: "",
  startDate: "",
  endDate: "",
  creditHours: "",
  colorHex: "#6366f1",
};

export default function CoursesPage() {
  const { user, loading } = useAuth();
  const [courses, setCourses] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const [form, setForm] = useState(EMPTY_COURSE);
  const [editingId, setEditingId] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [busy, setBusy] = useState(false);

  const palette = useSavedColors();

  const [scheduleFor, setScheduleFor] = useState(null);
  const [scheduleForm, setScheduleForm] = useState({
    dayOfWeek: 1, startTime: "10:00", endTime: "11:15", location: "",
  });

  async function refresh() {
    try {
      setCourses(await coursesApi.list());
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
  }, [loading, user]);

  function startAdd() {
    setForm(EMPTY_COURSE);
    setEditingId(null);
    setShowForm(true);
  }

  function startEdit(course) {
    setForm({
      name: course.name,
      professor: course.professor || "",
      semester: course.semester,
      startDate: course.startDate || "",
      endDate: course.endDate || "",
      creditHours: course.creditHours ?? "",
      colorHex: course.colorHex || "#6366f1",
    });
    setEditingId(course.id);
    setShowForm(true);
  }

  async function saveCourse(e) {
    e.preventDefault();
    setBusy(true);
    setError("");

    const payload = {
      ...form,
      creditHours: form.creditHours === "" ? null : Number(form.creditHours),
      // An empty date input is "", which the API would reject as a bad date.
      startDate: form.startDate || null,
      endDate: form.endDate || null,
    };

    if (payload.startDate && payload.endDate && payload.endDate < payload.startDate) {
      setBusy(false);
      setError("The last day of class has to be on or after the first day.");
      return;
    }

    try {
      if (editingId) {
        await coursesApi.update(editingId, payload);
      } else {
        await coursesApi.create(payload);
      }
      setShowForm(false);
      setForm(EMPTY_COURSE);
      setEditingId(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function removeCourse(course) {
    if (!confirm(`Delete "${course.name}"? Its assignments and materials go too.`)) return;
    try {
      await coursesApi.remove(course.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function addSchedule(e) {
    e.preventDefault();
    try {
      await coursesApi.schedules.create(scheduleFor, {
        dayOfWeek: Number(scheduleForm.dayOfWeek),
        startTime: scheduleForm.startTime + ":00",
        endTime: scheduleForm.endTime + ":00",
        location: scheduleForm.location || null,
      });
      setScheduleFor(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  async function removeSchedule(courseId, id) {
    try {
      await coursesApi.schedules.remove(courseId, id);
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
          <h1 className="text-2xl font-semibold">Courses</h1>
          <p className="mt-1 text-sm text-slate-600">The classes you are taking and when they meet.</p>
        </div>
        <button
          onClick={startAdd}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Add course
        </button>
      </div>

      {error && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {showForm && (
        <form onSubmit={saveCourse} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-medium">{editingId ? "Edit course" : "New course"}</h2>

          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <label className="block text-sm font-medium text-slate-700">Course name</label>
              <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className={input} placeholder="Data Structures" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Professor</label>
              <input value={form.professor} onChange={(e) => setForm({ ...form, professor: e.target.value })} className={input} placeholder="Dr. Smith" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Semester</label>
              <input required value={form.semester} onChange={(e) => setForm({ ...form, semester: e.target.value })} className={input} placeholder="Fall 2026" />
            </div>
            <div className="grid grid-cols-2 gap-3 sm:col-span-2">
              <div>
                <label className="block text-sm font-medium text-slate-700">First day of class</label>
                <input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} className={input} />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700">Last day of class</label>
                <input type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} className={input} />
              </div>
              <p className="col-span-2 -mt-1 text-xs text-slate-500">
                Optional, but without them this course keeps repeating on the calendar
                for every future week.
              </p>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Credits</label>
              <input type="number" min="0" max="12" value={form.creditHours} onChange={(e) => setForm({ ...form, creditHours: e.target.value })} className={input} />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-sm font-medium text-slate-700">Color</label>
              <ColorPicker
                value={form.colorHex}
                onChange={(colorHex) => setForm({ ...form, colorHex })}
                saved={palette.colors}
                onSave={palette.add}
                onRemove={palette.remove}
              />
            </div>
          </div>

          <div className="mt-5 flex gap-2">
            <button type="submit" disabled={busy} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {busy ? "Saving..." : editingId ? "Save changes" : "Create course"}
            </button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
              Cancel
            </button>
          </div>
        </form>
      )}

      <div className="mt-8 space-y-4">
        {courses.length === 0 && !showForm && (
          <div className="rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center">
            <p className="text-slate-600">No courses yet.</p>
            <button onClick={startAdd} className="mt-3 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
              Add your first course
            </button>
          </div>
        )}

        {courses.map((course) => (
          <div key={course.id} className="rounded-xl border border-slate-200 bg-white p-5">
            <div className="flex items-start justify-between gap-4">
              <div className="flex items-start gap-3">
                <span className="mt-1.5 h-3 w-3 shrink-0 rounded-full" style={{ backgroundColor: course.colorHex || "#94a3b8" }} />
                <div>
                  <h3 className="font-medium text-slate-900">{course.name}</h3>
                  <p className="text-sm text-slate-500">
                    {course.professor || "No professor listed"} · {course.semester}
                    {course.creditHours ? ` · ${course.creditHours} credits` : ""}
                  </p>
                  {(course.startDate || course.endDate) && (
                    <p className="text-xs text-slate-400">
                      {formatTerm(course.startDate, course.endDate)}
                    </p>
                  )}
                </div>
              </div>

              <div className="flex shrink-0 gap-2">
                <Link
                  href={`/courses/${course.id}/materials`}
                  className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
                >
                  Materials
                </Link>
                <Link
                  href={`/courses/${course.id}/flashcards`}
                  className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
                >
                  Flashcards
                </Link>
                <button onClick={() => startEdit(course)} className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
                  Edit
                </button>
                <button onClick={() => removeCourse(course)} className="rounded-md border border-red-200 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50">
                  Delete
                </button>
              </div>
            </div>

            <div className="mt-4 border-t border-slate-100 pt-4">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium text-slate-700">Meeting times</p>
                <button
                  onClick={() => setScheduleFor(scheduleFor === course.id ? null : course.id)}
                  className="text-sm font-medium text-indigo-600 hover:underline"
                >
                  {scheduleFor === course.id ? "Cancel" : "Add time"}
                </button>
              </div>

              {course.schedules.length === 0 ? (
                <p className="mt-2 text-sm text-slate-500">No meeting times added.</p>
              ) : (
                <ul className="mt-2 space-y-1">
                  {course.schedules.map((s) => (
                    <li key={s.id} className="flex items-center justify-between text-sm">
                      <span className="text-slate-700">
                        {DAYS[s.dayOfWeek]}
                        {s.location ? ` · ${s.location}` : ""}
                        {` · ${formatTime(s.startTime)} - ${formatTime(s.endTime)}`}
                      </span>
                      <button onClick={() => removeSchedule(course.id, s.id)} className="text-slate-400 hover:text-red-600">
                        Remove
                      </button>
                    </li>
                  ))}
                </ul>
              )}

              {scheduleFor === course.id && (
                <form onSubmit={addSchedule} className="mt-3 grid gap-3 rounded-lg bg-slate-50 p-3 sm:grid-cols-4">
                  <select value={scheduleForm.dayOfWeek} onChange={(e) => setScheduleForm({ ...scheduleForm, dayOfWeek: e.target.value })} className="rounded-md border border-slate-300 px-2 py-1.5 text-sm">
                    {DAYS.map((d, i) => <option key={i} value={i}>{d}</option>)}
                  </select>
                  <input type="time" value={scheduleForm.startTime} onChange={(e) => setScheduleForm({ ...scheduleForm, startTime: e.target.value })} className="rounded-md border border-slate-300 px-2 py-1.5 text-sm" />
                  <input type="time" value={scheduleForm.endTime} onChange={(e) => setScheduleForm({ ...scheduleForm, endTime: e.target.value })} className="rounded-md border border-slate-300 px-2 py-1.5 text-sm" />
                  <div className="flex gap-2">
                    <input value={scheduleForm.location} onChange={(e) => setScheduleForm({ ...scheduleForm, location: e.target.value })} placeholder="Room" className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm" />
                    <button type="submit" className="shrink-0 rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700">
                      Add
                    </button>
                  </div>
                </form>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

/** "Aug 25 - Dec 12, 2026", or one-sided if only one date is set. */
function formatTerm(startDate, endDate) {
  const label = (value, withYear) => {
    if (!value) return null;
    const [y, m, d] = value.split("-").map(Number);
    return new Date(y, m - 1, d).toLocaleDateString(undefined, {
      month: "short",
      day: "numeric",
      year: withYear ? "numeric" : undefined,
    });
  };

  if (startDate && endDate) return `${label(startDate, false)} - ${label(endDate, true)}`;
  if (startDate) return `Starts ${label(startDate, true)}`;
  return `Ends ${label(endDate, true)}`;
}

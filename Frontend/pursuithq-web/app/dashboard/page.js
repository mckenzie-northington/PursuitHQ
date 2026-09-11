'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { assignments as assignmentsApi, dashboard as dashboardApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import AssignmentCheckbox from "@/components/calendar/AssignmentCheckbox";
import { TYPE_LABEL, colorOf, fromIso, timeRangeOf } from "@/lib/calendar";

export default function DashboardPage() {
  const { user, loading } = useAuth();

  const [data, setData] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      setData(await dashboardApi.get());
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, []);

  useEffect(() => {
    if (loading || !user) return;
    load();
  }, [loading, user, load]);

  /**
   * Ticks an assignment off without leaving the page.
   *
   * Updates the screen first and saves after, so the tick is instant. A failed
   * save reloads, which puts the row back where the database says it belongs.
   */
  async function toggle(assignment) {
    const done = assignment.status === 2;
    const next = done ? 0 : 2;

    setData((d) => ({
      ...d,
      dueSoon: d.dueSoon.map((a) =>
        a.id === assignment.id
          ? { ...a, status: next, isOverdue: next === 2 ? false : a.isOverdue }
          : a
      ),
    }));

    try {
      await assignmentsApi.setStatus(assignment.id, next);
    } catch (err) {
      setError(`Could not update "${assignment.title}" — ${err.message}`);
      await load();
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-5xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (!data) {
    return (
      <div className="mx-auto max-w-5xl px-6 py-10">
        <p className="text-slate-600">{error || "Could not load your dashboard."}</p>
      </div>
    );
  }

  const today = fromIso(data.today);
  const empty =
    data.courses.length === 0 &&
    data.todaysSchedule.length === 0 &&
    data.dueSoon.length === 0;

  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <h1 className="text-2xl font-semibold">
        {greeting()}, {data.firstName}
      </h1>
      <p className="mt-1 text-sm text-slate-600">
        {today.toLocaleDateString(undefined, {
          weekday: "long",
          month: "long",
          day: "numeric",
        })}
        {data.counts.overdue > 0 && (
          <span className="ml-2 font-medium text-red-600">
            · {data.counts.overdue} overdue
          </span>
        )}
      </p>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      {empty ? (
        <div className="mt-8 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center">
          <p className="text-slate-600">Nothing here yet.</p>
          <p className="mt-1 text-sm text-slate-500">
            Add a course and PursuitHQ has something to work with.
          </p>
          <Link
            href="/courses"
            className="mt-4 inline-block rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
          >
            Add your first course
          </Link>
        </div>
      ) : (
        <>
          <div className="mt-6 grid gap-6 lg:grid-cols-2">
            {/* Today */}
            <section className="rounded-xl border border-slate-200 bg-white p-5">
              <div className="flex items-center justify-between">
                <h2 className="font-medium text-slate-900">Today</h2>
                <Link href="/calendar" className="text-sm font-medium text-indigo-600 hover:underline">
                  Calendar
                </Link>
              </div>

              {data.todaysSchedule.length === 0 ? (
                <p className="mt-3 text-sm text-slate-500">Nothing scheduled.</p>
              ) : (
                <ul className="mt-3 space-y-2.5">
                  {data.todaysSchedule.map((item) => (
                    <li key={item.id} className="flex items-start gap-3">
                      <span
                        className="mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full"
                        style={{ backgroundColor: colorOf(item) }}
                      />
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-slate-900">
                          {item.title}
                        </p>
                        <p className="truncate text-xs text-slate-500">
                          {TYPE_LABEL[item.type]}
                          {item.location && ` · ${item.location}`}
                          {` · ${timeRangeOf(item)}`}
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            {/* Due soon */}
            <section className="rounded-xl border border-slate-200 bg-white p-5">
              <div className="flex items-center justify-between">
                <h2 className="font-medium text-slate-900">Due this week</h2>
                <Link
                  href="/assignments"
                  className="text-sm font-medium text-indigo-600 hover:underline"
                >
                  All assignments
                </Link>
              </div>

              {data.dueSoon.length === 0 ? (
                <p className="mt-3 text-sm text-slate-500">Nothing due in the next week.</p>
              ) : (
                <ul className="mt-3 space-y-2">
                  {data.dueSoon.map((assignment) => {
                    const done = assignment.status === 2;

                    return (
                      <li key={assignment.id} className="flex items-start gap-3">
                        <span className="mt-0.5">
                          <AssignmentCheckbox
                            checked={done}
                            onChange={() => toggle(assignment)}
                          />
                        </span>
                        <div className="min-w-0 flex-1">
                          <p
                            className={`truncate text-sm font-medium ${
                              done ? "text-slate-400 line-through" : "text-slate-900"
                            }`}
                          >
                            {assignment.title}
                          </p>
                          <p className="truncate text-xs text-slate-500">
                            {assignment.courseName} · {dueLabel(assignment.dueDate)}
                            {assignment.isOverdue && !done && (
                              <span className="ml-1 font-medium text-red-600">Overdue</span>
                            )}
                          </p>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </section>
          </div>

          {/* Study tools */}
          {(data.recentDecks.length > 0 || data.recentTests.length > 0) && (
            <section className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
              <div className="flex items-center justify-between">
                <h2 className="font-medium text-slate-900">Pick up where you left off</h2>
                <Link href="/study" className="text-sm font-medium text-indigo-600 hover:underline">
                  Study
                </Link>
              </div>

              <div className="mt-3 grid gap-2 sm:grid-cols-2">
                {data.recentDecks.map((deck) => (
                  <Link
                    key={`deck-${deck.id}`}
                    href={`/decks/${deck.id}`}
                    className="rounded-lg border border-slate-200 px-3 py-2.5 transition hover:bg-slate-50"
                  >
                    <p className="truncate text-sm font-medium text-slate-900">{deck.title}</p>
                    <p className="truncate text-xs text-slate-500">
                      {deck.courseName && `${deck.courseName} · `}
                      {deck.cardCount} cards ·{" "}
                      {deck.accuracy === null ? "not studied yet" : `${deck.accuracy}% right`}
                    </p>
                  </Link>
                ))}

                {data.recentTests.map((test) => (
                  <Link
                    key={`test-${test.id}`}
                    href={`/tests/${test.id}`}
                    className="rounded-lg border border-slate-200 px-3 py-2.5 transition hover:bg-slate-50"
                  >
                    <p className="truncate text-sm font-medium text-slate-900">{test.title}</p>
                    <p className="truncate text-xs text-slate-500">
                      {test.courseName && `${test.courseName} · `}
                      {test.questionCount} questions ·{" "}
                      {test.attemptCount === 0 ? "not taken yet" : `best ${test.bestScore}%`}
                    </p>
                  </Link>
                ))}
              </div>
            </section>
          )}

          {/* Courses */}
          <section className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
            <div className="flex items-center justify-between">
              <h2 className="font-medium text-slate-900">Your courses</h2>
              <Link href="/courses" className="text-sm font-medium text-indigo-600 hover:underline">
                Manage
              </Link>
            </div>

            {data.courses.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No courses yet.</p>
            ) : (
              <ul className="mt-3 grid gap-2 sm:grid-cols-2">
                {data.courses.map((course) => (
                  <li key={course.id}>
                    <Link
                      href={`/courses/${course.id}/materials`}
                      className="flex items-center gap-3 rounded-lg border border-slate-200 px-3 py-2.5 transition hover:bg-slate-50"
                    >
                      <span
                        className="h-3 w-3 shrink-0 rounded-full"
                        style={{ backgroundColor: course.colorHex || "#94a3b8" }}
                      />
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-slate-900">
                          {course.name}
                        </p>
                        <p className="truncate text-xs text-slate-500">
                          {course.professor || "No professor listed"}
                          {course.openAssignments > 0 &&
                            ` · ${course.openAssignments} open`}
                        </p>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function greeting() {
  const hour = new Date().getHours();

  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";
  return "Good evening";
}

/** "Today, 11:59 PM", "Tomorrow", or "Thu" - relative reads faster than a date. */
function dueLabel(value) {
  const due = new Date(value);

  const startOfDay = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
  const days = Math.round((startOfDay(due) - startOfDay(new Date())) / 86400000);

  const time = due.toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });

  if (days === 0) return `due today, ${time}`;
  if (days === 1) return `due tomorrow, ${time}`;
  if (days < 0) return `was due ${due.toLocaleDateString(undefined, { month: "short", day: "numeric" })}`;

  return `due ${due.toLocaleDateString(undefined, { weekday: "short" })}, ${time}`;
}

'use client';

import { useEffect, useState } from "react";
import Link from "next/link";
import { courses as coursesApi, assignments as assignmentsApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function DashboardPage() {
  const { user, loading } = useAuth();
  const [courses, setCourses] = useState([]);
  const [assignments, setAssignments] = useState([]);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (loading || !user) return;

    Promise.all([coursesApi.list(), assignmentsApi.list()])
      .then(([c, a]) => {
        setCourses(c);
        setAssignments(a);
      })
      .catch(() => {})
      .finally(() => setReady(true));
  }, [loading, user]);

  if (loading || !ready) {
    return <div className="mx-auto max-w-5xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const upcoming = assignments
    .filter((a) => a.status !== 2)
    .slice(0, 5);

  const overdue = assignments.filter((a) => a.isOverdue).length;

  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <h1 className="text-2xl font-semibold">Welcome back, {user?.firstName}</h1>
      <p className="mt-1 text-sm text-slate-600">Here is where things stand.</p>

      <div className="mt-8 grid gap-4 sm:grid-cols-3">
        <StatCard label="Courses" value={courses.length} href="/courses" />
        <StatCard label="Open assignments" value={assignments.filter((a) => a.status !== 2).length} href="/assignments" />
        <StatCard label="Overdue" value={overdue} href="/assignments" tone={overdue > 0 ? "warn" : "normal"} />
      </div>

      <section className="mt-10">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Coming up</h2>
          <Link href="/assignments" className="text-sm font-medium text-indigo-600 hover:underline">
            View all
          </Link>
        </div>

        {upcoming.length === 0 ? (
          <EmptyState
            message="Nothing due right now."
            action={{ href: "/assignments", label: "Add an assignment" }}
          />
        ) : (
          <ul className="divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
            {upcoming.map((a) => (
              <li key={a.id} className="flex items-center justify-between px-4 py-3">
                <div>
                  <p className="font-medium text-slate-900">{a.title}</p>
                  <p className="text-sm text-slate-500">{a.courseName}</p>
                </div>
                <span className={`text-sm ${a.isOverdue ? "font-medium text-red-600" : "text-slate-600"}`}>
                  {new Date(a.dueDate).toLocaleDateString()}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="mt-10">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Your courses</h2>
          <Link href="/courses" className="text-sm font-medium text-indigo-600 hover:underline">
            Manage
          </Link>
        </div>

        {courses.length === 0 ? (
          <EmptyState message="No courses yet." action={{ href: "/courses", label: "Add your first course" }} />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {courses.map((c) => (
              <Link key={c.id} href={`/courses/${c.id}/materials`} className="rounded-xl border border-slate-200 bg-white p-4 transition hover:border-slate-300">
                <div className="flex items-start gap-3">
                  <span
                    className="mt-1 h-3 w-3 shrink-0 rounded-full"
                    style={{ backgroundColor: c.colorHex || "#94a3b8" }}
                  />
                  <div>
                    <p className="font-medium text-slate-900">{c.name}</p>
                    <p className="text-sm text-slate-500">
                      {c.professor || "No professor listed"} · {c.semester}
                    </p>
                    <p className="mt-1 text-xs text-slate-500">
                      {c.assignmentCount} assignment{c.assignmentCount === 1 ? "" : "s"}
                    </p>
                  </div>
                </div>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function StatCard({ label, value, href, tone = "normal" }) {
  return (
    <Link
      href={href}
      className="rounded-xl border border-slate-200 bg-white p-5 transition hover:border-slate-300"
    >
      <p className="text-sm text-slate-600">{label}</p>
      <p className={`mt-1 text-3xl font-semibold ${tone === "warn" ? "text-red-600" : "text-slate-900"}`}>
        {value}
      </p>
    </Link>
  );
}

function EmptyState({ message, action }) {
  return (
    <div className="rounded-xl border border-dashed border-slate-300 bg-white px-6 py-10 text-center">
      <p className="text-sm text-slate-600">{message}</p>
      {action && (
        <Link
          href={action.href}
          className="mt-3 inline-block rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          {action.label}
        </Link>
      )}
    </div>
  );
}

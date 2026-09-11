'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { courses as coursesApi, study as studyApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Markdown from "@/components/Markdown";
import { downloadText, safeFileName } from "@/lib/download";

export default function CourseGuidesPage() {
  const { id } = useParams();
  const courseId = Number(id);
  const { user, loading } = useAuth();

  const [course, setCourse] = useState(null);
  const [guides, setGuides] = useState([]);
  const [open, setOpen] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      const [courseData, list] = await Promise.all([
        coursesApi.get(courseId),
        studyApi.guides(courseId),
      ]);

      setCourse(courseData);
      setGuides(list);

      // Open the newest by default - a list of titles is not much use on its
      // own, and the common case is coming back to the one you just made.
      if (list.length > 0) setOpen(await studyApi.guide(list[0].id));
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [courseId]);

  useEffect(() => {
    if (loading || !user) return;
    load();
  }, [loading, user, load]);

  async function show(guide) {
    try {
      setOpen(await studyApi.guide(guide.id));
    } catch (err) {
      setError(err.message);
    }
  }

  async function remove(guide) {
    if (!confirm(`Delete "${guide.title}"?`)) return;

    try {
      await studyApi.removeGuide(guide.id);

      const remaining = guides.filter((g) => g.id !== guide.id);
      setGuides(remaining);
      if (open?.id === guide.id) setOpen(null);
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-5xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <Link href="/study" className="text-sm text-indigo-600 hover:underline">
        &larr; Study
      </Link>

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Study guides</h1>
          <p className="mt-1 text-sm text-slate-600">{course?.name}</p>
        </div>
        <Link
          href={`/courses/${courseId}/tests`}
          className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
        >
          Practice tests
        </Link>
      </div>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      {guides.length === 0 ? (
        <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center text-slate-600">
          <p>No study guides saved yet.</p>
          <Link href="/study" className="mt-2 inline-block font-medium text-indigo-600 hover:underline">
            Ask for one in Study
          </Link>
        </div>
      ) : (
        <div className="mt-6 grid gap-6 lg:grid-cols-[16rem_1fr]">
          <ul className="space-y-1">
            {guides.map((g) => (
              <li key={g.id} className="group flex items-center gap-0.5">
                <button
                  onClick={() => show(g)}
                  className={`min-w-0 flex-1 rounded-md px-2 py-1.5 text-left text-sm transition ${
                    open?.id === g.id
                      ? "bg-indigo-50 text-indigo-700"
                      : "text-slate-600 hover:bg-slate-100"
                  }`}
                >
                  <span className="block truncate">{g.title}</span>
                  <span className="block text-xs text-slate-400">
                    {new Date(g.createdAt).toLocaleDateString()}
                  </span>
                </button>
                <button
                  onClick={() => remove(g)}
                  title="Delete"
                  className="hidden px-1 text-xs text-slate-400 hover:text-red-600 group-hover:block"
                >
                  ✕
                </button>
              </li>
            ))}
          </ul>

          <div className="rounded-xl border border-slate-200 bg-white">
            {open ? (
              <>
                <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-5 py-3">
                  <h2 className="min-w-0 truncate font-medium text-slate-900">{open.title}</h2>
                  <button
                    onClick={() => downloadText(open.content, safeFileName(open.title, "md"))}
                    className="shrink-0 rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                  >
                    Download
                  </button>
                </div>
                <div className="px-5 py-4">
                  <Markdown text={open.content} />
                </div>
              </>
            ) : (
              <p className="px-5 py-12 text-center text-slate-500">
                Pick a guide to read it.
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

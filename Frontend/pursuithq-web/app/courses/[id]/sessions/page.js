'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { courses as coursesApi, study as studyApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function CourseSessionsPage() {
  const { id } = useParams();
  const courseId = Number(id);
  const { user, loading } = useAuth();
  const router = useRouter();

  const [course, setCourse] = useState(null);
  const [sessions, setSessions] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [renamingId, setRenamingId] = useState(null);
  const [renameText, setRenameText] = useState("");

  const load = useCallback(async () => {
    try {
      const [courseData, list] = await Promise.all([
        coursesApi.get(courseId),
        studyApi.conversations(courseId),
      ]);

      setCourse(courseData);
      setSessions(list);
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

  async function saveRename(e) {
    e.preventDefault();
    const title = renameText.trim();
    if (!title) return;

    try {
      const updated = await studyApi.rename(renamingId, title);
      setSessions((list) => list.map((s) => (s.id === updated.id ? { ...s, title: updated.title } : s)));
      setRenamingId(null);
    } catch (err) {
      setError(err.message);
    }
  }

  async function remove(session) {
    if (!confirm(`Delete "${session.title}" and everything in it?`)) return;

    try {
      await studyApi.removeConversation(session.id);
      setSessions((list) => list.filter((s) => s.id !== session.id));
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-4xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-4xl px-6 py-10">
      <Link href="/study" className="text-sm text-indigo-600 hover:underline">
        &larr; Study
      </Link>

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Study sessions</h1>
          <p className="mt-1 text-sm text-slate-600">{course?.name}</p>
        </div>
        <div className="flex gap-2">
          <Link
            href={`/courses/${courseId}/guides`}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
          >
            Study guides
          </Link>
          <Link
            href={`/courses/${courseId}/tests`}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
          >
            Practice tests
          </Link>
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

      {sessions.length === 0 ? (
        <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center text-slate-600">
          <p>No sessions yet.</p>
          <Link href="/study" className="mt-2 inline-block font-medium text-indigo-600 hover:underline">
            Start one in Study
          </Link>
        </div>
      ) : (
        <ul className="mt-6 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {sessions.map((session) => (
            <li key={session.id} className="flex items-center justify-between gap-4 px-4 py-3">
              {renamingId === session.id ? (
                <form onSubmit={saveRename} className="flex flex-1 gap-2">
                  <input
                    autoFocus
                    value={renameText}
                    onChange={(e) => setRenameText(e.target.value)}
                    className="flex-1 rounded-md border border-indigo-400 px-3 py-1.5 text-sm outline-none"
                  />
                  <button
                    type="submit"
                    className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white"
                  >
                    Save
                  </button>
                  <button
                    type="button"
                    onClick={() => setRenamingId(null)}
                    className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700"
                  >
                    Cancel
                  </button>
                </form>
              ) : (
                <>
                  <button
                    onClick={() => router.push("/study")}
                    className="min-w-0 flex-1 text-left"
                  >
                    <p className="truncate font-medium text-slate-900">{session.title}</p>
                    <p className="text-sm text-slate-500">
                      {session.messageCount} messages · last used{" "}
                      {new Date(session.updatedAt).toLocaleDateString()}
                    </p>
                  </button>

                  <div className="flex shrink-0 gap-3 text-sm">
                    <button
                      onClick={() => {
                        setRenamingId(session.id);
                        setRenameText(session.title);
                      }}
                      className="text-indigo-600 hover:underline"
                    >
                      Rename
                    </button>
                    <button
                      onClick={() => remove(session)}
                      className="text-slate-400 transition hover:text-red-600"
                    >
                      Delete
                    </button>
                  </div>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

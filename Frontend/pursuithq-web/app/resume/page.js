'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { resumes as resumesApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function ResumesPage() {
  const { user, loading } = useAuth();
  const router = useRouter();

  const [list, setList] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [importing, setImporting] = useState(false);
  const [creating, setCreating] = useState(false);

  const fileInput = useRef(null);

  const load = useCallback(async () => {
    try {
      setList(await resumesApi.list());
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

  async function handleFile(e) {
    const file = e.target.files?.[0];
    // Clearing the input matters: picking the same file twice in a row fires
    // no change event otherwise, and it looks like the button stopped working.
    e.target.value = "";
    if (!file) return;

    setImporting(true);
    setError("");

    try {
      const created = await resumesApi.import(file);
      router.push(`/resume/${created.id}`);
    } catch (err) {
      setError(err.message);
      setImporting(false);
    }
  }

  async function startBlank() {
    setCreating(true);
    setError("");

    try {
      const created = await resumesApi.create({
        title: "My resume",
        content: {
          contact: { name: `${user.firstName} ${user.lastName}`, email: user.email },
          education: [],
          experience: [],
          projects: [],
          skills: [],
        },
      });

      router.push(`/resume/${created.id}`);
    } catch (err) {
      setError(err.message);
      setCreating(false);
    }
  }

  async function remove(resume) {
    if (!confirm(`Delete "${resume.title}"?`)) return;

    try {
      await resumesApi.remove(resume.id);
      setList((current) => current.filter((r) => r.id !== resume.id));
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-4xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-4xl px-6 py-10">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Resume</h1>
          <p className="mt-1 text-sm text-slate-600">
            Build one here, or bring the one you already have.
          </p>
        </div>
        <Link
          href="/resume/jobs"
          className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
        >
          Saved jobs
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

      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        <button
          onClick={() => fileInput.current?.click()}
          disabled={importing}
          className="rounded-xl border border-slate-200 bg-white p-5 text-left transition hover:border-indigo-400 disabled:opacity-60"
        >
          <p className="font-medium text-slate-900">
            {importing ? "Reading your resume..." : "Upload what you have"}
          </p>
          <p className="mt-1 text-sm text-slate-600">
            PDF or Word. AI reads it into sections you can edit — it takes up to a minute,
            and you should check what it got.
          </p>
        </button>

        <input
          ref={fileInput}
          type="file"
          accept=".pdf,.docx,.txt,.md"
          onChange={handleFile}
          className="hidden"
        />

        <button
          onClick={startBlank}
          disabled={creating}
          className="rounded-xl border border-slate-200 bg-white p-5 text-left transition hover:border-indigo-400 disabled:opacity-60"
        >
          <p className="font-medium text-slate-900">Start from blank</p>
          <p className="mt-1 text-sm text-slate-600">
            Fill in the sections yourself. Your name and email are filled in from your
            account.
          </p>
        </button>
      </div>

      <h2 className="mt-8 font-medium text-slate-900">Your resumes</h2>

      {list.length === 0 ? (
        <div className="mt-3 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-10 text-center text-slate-600">
          None yet.
        </div>
      ) : (
        <ul className="mt-3 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {list.map((resume) => (
            <li key={resume.id} className="flex items-center justify-between gap-4 px-4 py-3">
              <Link href={`/resume/${resume.id}`} className="min-w-0 flex-1">
                <p className="truncate font-medium text-slate-900">{resume.title}</p>
                <p className="truncate text-sm text-slate-500">
                  {resume.ownerName && `${resume.ownerName} · `}
                  {resume.experienceCount} experience · {resume.projectCount} projects · updated{" "}
                  {new Date(resume.lastUpdated).toLocaleDateString()}
                </p>
              </Link>

              <div className="flex shrink-0 items-center gap-2">
                <Link
                  href={`/resume/${resume.id}`}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
                >
                  Open
                </Link>
                <button
                  onClick={() => remove(resume)}
                  className="text-sm text-slate-400 transition hover:text-red-600"
                >
                  Delete
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

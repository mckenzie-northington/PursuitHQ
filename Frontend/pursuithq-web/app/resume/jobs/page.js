'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { savedJobs as savedJobsApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

/**
 * Jobs kept from the resume matcher.
 *
 * Each row is a posting, the link back to it, and the score the resume got.
 * Opening one shows the breakdown as it stood when it was saved - including any
 * line whose verdict was overridden, because that is the answer that was
 * settled on.
 */
export default function SavedJobsPage() {
  const { user, loading } = useAuth();

  const [jobs, setJobs] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [open, setOpen] = useState(null);

  const refresh = useCallback(async () => {
    try {
      setJobs(await savedJobsApi.list());
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, []);

  useEffect(() => {
    if (loading || !user) return;
    refresh();
  }, [loading, user, refresh]);

  async function openJob(id) {
    if (open?.id === id) {
      setOpen(null);
      return;
    }

    try {
      setOpen(await savedJobsApi.get(id));
    } catch (err) {
      setError(err.message);
    }
  }

  async function remove(job) {
    if (!confirm(`Remove "${job.title}" from your saved jobs?`)) return;

    try {
      await savedJobsApi.remove(job.id);
      if (open?.id === job.id) setOpen(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-4xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-4xl px-6 py-10">
      <Link href="/resume" className="text-sm text-indigo-600 hover:underline">
        &larr; Resumes
      </Link>

      <h1 className="mt-2 text-2xl font-semibold">Saved jobs</h1>
      <p className="mt-1 text-sm text-slate-600">
        Postings you kept, and how your resume scored against each one.
      </p>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      {jobs.length === 0 ? (
        <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center text-slate-600">
          <p>Nothing saved yet.</p>
          <p className="mt-1 text-sm">
            Open a resume, match it to a job, and press Save this job.
          </p>
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {jobs.map((job) => (
            <li key={job.id} className="rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium text-slate-900">{job.title}</p>
                  <p className="text-sm text-slate-600">
                    {job.company || "Unknown company"}
                    {job.resumeTitle ? ` · scored against ${job.resumeTitle}` : ""}
                  </p>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Saved {new Date(job.savedAt).toLocaleDateString()}
                  </p>
                </div>

                <div className="flex shrink-0 items-center gap-3">
                  <ScoreBadge score={job.score} />
                </div>
              </div>

              <div className="mt-3 flex flex-wrap items-center gap-3 text-sm">
                {job.url && (
                  <a
                    href={job.url}
                    target="_blank"
                    rel="noreferrer noopener"
                    className="font-medium text-indigo-600 hover:underline"
                  >
                    Open posting
                  </a>
                )}

                <button
                  onClick={() => openJob(job.id)}
                  className="font-medium text-slate-600 hover:text-slate-900"
                >
                  {open?.id === job.id ? "Hide details" : "Details"}
                </button>

                <button
                  onClick={() => remove(job)}
                  className="text-slate-400 hover:text-red-600"
                >
                  Remove
                </button>
              </div>

              {open?.id === job.id && <SavedJobDetail job={open} />}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ScoreBadge({ score }) {
  const tone =
    score >= 75
      ? "border-green-200 bg-green-50 text-green-700"
      : score >= 50
      ? "border-amber-200 bg-amber-50 text-amber-800"
      : "border-red-200 bg-red-50 text-red-700";

  return (
    <span className={`rounded-full border px-3 py-1 text-sm font-semibold ${tone}`}>
      {score}%
    </span>
  );
}

function SavedJobDetail({ job }) {
  const match = job.match;

  return (
    <div className="mt-4 border-t border-slate-200 pt-4">
      {match?.summary && <p className="text-sm text-slate-700">{match.summary}</p>}

      {match?.requirements?.length > 0 ? (
        <ul className="mt-3 space-y-1.5">
          {match.requirements.map((requirement, i) => (
            <li key={i} className="flex items-start gap-2 text-sm">
              <span
                className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${
                  requirement.status === "met"
                    ? "bg-green-500"
                    : requirement.status === "partial"
                    ? "bg-amber-500"
                    : "bg-red-500"
                }`}
              />
              <span className="text-slate-700">
                {requirement.requirement}
                {requirement.isRequired && (
                  <span className="ml-1.5 text-xs text-slate-400">required</span>
                )}
                {requirement.overridden && (
                  <span className="ml-1.5 text-xs text-indigo-600">your call</span>
                )}
              </span>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-2 text-sm text-slate-500">
          The breakdown for this one could not be read. The score and the posting are
          still here.
        </p>
      )}

      {job.postingText && (
        <details className="mt-4">
          <summary className="cursor-pointer text-sm font-medium text-slate-600 hover:text-slate-900">
            The posting as it was scored
          </summary>
          {/*
            Kept because links die. Once the role closes there is no way back to
            what the score was measuring, and a number with nothing behind it is
            not worth keeping.
          */}
          <pre className="mt-2 max-h-80 overflow-auto whitespace-pre-wrap rounded-md bg-slate-50 p-3 text-xs text-slate-600">
            {job.postingText}
          </pre>
        </details>
      )}
    </div>
  );
}

'use client';

import { useState } from "react";
import Link from "next/link";
import { jobSearch } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function JobSearchPage() {
  const { user, loading } = useAuth();

  const [form, setForm] = useState({
    query: "",
    location: "",
    contractTime: "",
  });
  const [results, setResults] = useState(null);
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [savingId, setSavingId] = useState(null);
  const [expanded, setExpanded] = useState(null);

  async function runSearch(e, nextPage = 1) {
    e?.preventDefault();
    if (!form.query.trim()) return;

    setBusy(true);
    setError("");

    try {
      const data = await jobSearch.search({ ...form, page: nextPage });
      setResults(data);
      setPage(nextPage);
      window.scrollTo({ top: 0, behavior: "smooth" });
    } catch (err) {
      // Show the server's detail too - "something went wrong" alone is useless
      // when you are the one who has to fix it.
      const detail = err.details ? Object.values(err.details).flat().join(" ") : "";
      setError(detail ? `${err.message} — ${detail}` : err.message);
      setResults(null);
    } finally {
      setBusy(false);
    }
  }

  async function save(job) {
    setSavingId(job.externalId);
    try {
      await jobSearch.saveToTracker({
        externalId: job.externalId,
        company: job.company,
        role: job.title,
        applyUrl: job.applyUrl,
        type: guessType(job),
      });

      // Flip the button locally rather than re-running the search, which would
      // spend another call from the daily quota.
      setResults((prev) => ({
        ...prev,
        results: prev.results.map((r) =>
          r.externalId === job.externalId ? { ...r, alreadySaved: true } : r
        ),
      }));
    } catch (err) {
      setError(err.message);
    } finally {
      setSavingId(null);
    }
  }

  if (loading) {
    return <div className="mx-auto max-w-4xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const input =
    "w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="mx-auto max-w-4xl px-6 py-10">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Find Jobs</h1>
          <p className="mt-1 text-sm text-slate-600">
            Search real postings, then save the ones worth applying to.
          </p>
        </div>
        <Link href="/applications" className="text-sm font-medium text-indigo-600 hover:underline">
          My tracker &rarr;
        </Link>
      </div>

      <form onSubmit={(e) => runSearch(e, 1)} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
        <div className="grid gap-3 sm:grid-cols-[2fr_1fr_1fr]">
          <div>
            <label className="block text-sm font-medium text-slate-700">What</label>
            <input
              required
              value={form.query}
              onChange={(e) => setForm({ ...form, query: e.target.value })}
              placeholder="Software Engineering Intern"
              className={`mt-1 ${input}`}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Where</label>
            <input
              value={form.location}
              onChange={(e) => setForm({ ...form, location: e.target.value })}
              placeholder="Any location"
              className={`mt-1 ${input}`}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700">Hours</label>
            <select
              value={form.contractTime}
              onChange={(e) => setForm({ ...form, contractTime: e.target.value })}
              className={`mt-1 ${input}`}
            >
              <option value="">Any</option>
              <option value="internship">Internship</option>
              <option value="full_time">Full time</option>
              <option value="part_time">Part time</option>
            </select>
          </div>
        </div>

        <button
          type="submit"
          disabled={busy}
          className="mt-4 rounded-md bg-indigo-600 px-5 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {busy ? "Searching..." : "Search"}
        </button>
      </form>

      {error && (
        <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {results && (
        <>
          <p className="mt-6 text-sm text-slate-600">
            {results.totalResults.toLocaleString()} postings found · page {results.page}
          </p>

          {results.results.length === 0 ? (
            <div className="mt-4 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center text-sm text-slate-600">
              Nothing matched. Try a broader term, or clear the location.
            </div>
          ) : (
            <ul className="mt-4 space-y-3">
              {results.results.map((job) => (
                <li key={job.externalId} className="rounded-xl border border-slate-200 bg-white p-5">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <h2 className="font-medium text-slate-900">{job.title}</h2>
                      <p className="text-sm text-slate-600">
                        {job.company}
                        {job.location && ` · ${job.location}`}
                      </p>

                      <div className="mt-2 flex flex-wrap items-center gap-1.5 text-[11px]">
                        {job.contractTime && (
                          <span className="rounded bg-slate-100 px-1.5 py-0.5 text-slate-600">
                            {job.contractTime.replace("_", " ")}
                          </span>
                        )}
                        {(job.salaryMin || job.salaryMax) && (
                          <span className="rounded bg-green-100 px-1.5 py-0.5 text-green-800">
                            {formatSalary(job.salaryMin, job.salaryMax)}
                          </span>
                        )}
                        {job.postedAt && (
                          <span className="rounded bg-slate-100 px-1.5 py-0.5 text-slate-600">
                            posted {new Date(job.postedAt).toLocaleDateString()}
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="flex shrink-0 gap-2">
                      <a
                        href={job.applyUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700"
                      >
                        Apply
                      </a>
                      <button
                        onClick={() => save(job)}
                        disabled={job.alreadySaved || savingId === job.externalId}
                        className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                      >
                        {job.alreadySaved ? "Saved" : savingId === job.externalId ? "Saving..." : "Save"}
                      </button>
                    </div>
                  </div>

                  {job.description && (
                    <div className="mt-3 text-sm text-slate-600">
                      <p className={expanded === job.externalId ? "" : "line-clamp-2"}>
                        {job.description}
                      </p>
                      <button
                        onClick={() => setExpanded(expanded === job.externalId ? null : job.externalId)}
                        className="mt-1 text-xs font-medium text-indigo-600 hover:underline"
                      >
                        {expanded === job.externalId ? "Show less" : "Show more"}
                      </button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}

          <div className="mt-6 flex items-center justify-between">
            <button
              onClick={(e) => runSearch(e, page - 1)}
              disabled={page <= 1 || busy}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-40"
            >
              Previous
            </button>
            <span className="text-sm text-slate-500">Page {page}</span>
            <button
              onClick={(e) => runSearch(e, page + 1)}
              disabled={busy || results.results.length === 0}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-40"
            >
              Next
            </button>
          </div>
        </>
      )}

      {!results && !error && (
        <div className="mt-8 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center">
          <p className="text-sm text-slate-600">Search above to see openings.</p>
          <p className="mx-auto mt-2 max-w-md text-xs text-slate-500">
            Apply always opens the employer&apos;s own posting. PursuitHQ never submits
            an application for you.
          </p>
        </div>
      )}
    </div>
  );
}

/** Guesses the tracker type from the posting; defaults to Internship. */
function guessType(job) {
  const text = `${job.title} ${job.contractTime ?? ""}`.toLowerCase();
  if (text.includes("intern")) return 0;
  if (text.includes("part_time") || text.includes("part time")) return 1;
  return 2;
}

function formatSalary(min, max) {
  const fmt = (n) => `$${Math.round(n).toLocaleString(undefined, { maximumFractionDigits: 0 })}`;
  if (min && max && min !== max) return `${fmt(min)} – ${fmt(max)}`;
  return fmt(min || max);
}

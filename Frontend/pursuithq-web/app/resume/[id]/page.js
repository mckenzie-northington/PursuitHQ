'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { resumes as resumesApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

const SEVERITY = {
  high: { label: "Important", chip: "bg-red-50 text-red-700 border-red-200" },
  medium: { label: "Worth fixing", chip: "bg-amber-50 text-amber-800 border-amber-200" },
  low: { label: "Minor", chip: "bg-slate-100 text-slate-600 border-slate-200" },
};

const BLANK_EDUCATION = { school: "", degree: "", location: "", startDate: "", endDate: "", gpa: "", details: [] };
const BLANK_EXPERIENCE = { title: "", organization: "", location: "", startDate: "", endDate: "", bullets: [""] };
const BLANK_PROJECT = { name: "", link: "", technologies: "", bullets: [""] };

export default function ResumeEditorPage() {
  const { id } = useParams();
  const resumeId = Number(id);
  const { user, loading } = useAuth();

  const [title, setTitle] = useState("");
  const [content, setContent] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState(null);
  const [dirty, setDirty] = useState(false);

  const [review, setReview] = useState(null);
  const [reviewing, setReviewing] = useState(false);

  const load = useCallback(async () => {
    try {
      const resume = await resumesApi.get(resumeId);
      setTitle(resume.title);
      setContent(resume.content);
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [resumeId]);

  useEffect(() => {
    if (loading || !user) return;
    load();
  }, [loading, user, load]);

  // Leaving with unsaved edits loses them, and this page is long enough that
  // you can easily forget you were editing.
  useEffect(() => {
    if (!dirty) return;

    const warn = (e) => {
      e.preventDefault();
      e.returnValue = "";
    };

    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  function edit(updater) {
    setContent((c) => updater(structuredClone(c)));
    setDirty(true);
  }

  async function save() {
    setSaving(true);
    setError("");

    try {
      await resumesApi.update(resumeId, { title: title.trim() || "Untitled", content });
      setSavedAt(new Date());
      setDirty(false);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  async function runReview() {
    setReviewing(true);
    setError("");

    try {
      // Saved first, so the review is of what is on screen rather than of
      // whatever was last written to the database.
      if (dirty) await save();
      setReview(await resumesApi.review(resumeId));
    } catch (err) {
      setError(err.message);
    } finally {
      setReviewing(false);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (!content) {
    return (
      <div className="mx-auto max-w-6xl px-6 py-10">
        <p className="text-slate-600">{error || "That resume could not be loaded."}</p>
        <Link href="/resume" className="mt-3 inline-block text-indigo-600 hover:underline">
          Back to resumes
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
      <div className="print-hide">
        <Link href="/resume" className="text-sm text-indigo-600 hover:underline">
          &larr; Resumes
        </Link>

        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <input
            value={title}
            onChange={(e) => {
              setTitle(e.target.value);
              setDirty(true);
            }}
            className="min-w-0 flex-1 border-b border-transparent bg-transparent text-2xl font-semibold outline-none transition hover:border-slate-300 focus:border-indigo-500"
          />

          <div className="flex shrink-0 items-center gap-2">
            <span className="text-xs text-slate-500">
              {dirty ? "Unsaved changes" : savedAt ? `Saved ${savedAt.toLocaleTimeString()}` : ""}
            </span>
            <button
              onClick={runReview}
              disabled={reviewing}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
            >
              {reviewing ? "Checking..." : "Check my resume"}
            </button>
            <button
              onClick={() => window.print()}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Print / PDF
            </button>
            <button
              onClick={save}
              disabled={saving || !dirty}
              className="rounded-md bg-indigo-600 px-4 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {saving ? "Saving..." : "Save"}
            </button>
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

        {review && <ReviewPanel review={review} onClose={() => setReview(null)} />}
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <div className="print-hide space-y-5">
          <Editor content={content} edit={edit} />
        </div>

        <div className="lg:sticky lg:top-6 lg:self-start">
          <p className="print-hide mb-2 text-xs font-medium uppercase tracking-wide text-slate-400">
            Preview
          </p>
          <Sheet content={content} />
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ review */

function ReviewPanel({ review, onClose }) {
  const order = { high: 0, medium: 1, low: 2 };
  const findings = [...review.findings].sort(
    (a, b) => (order[a.severity] ?? 1) - (order[b.severity] ?? 1)
  );

  return (
    <div className="mt-4 rounded-xl border border-slate-200 bg-white p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="font-medium text-slate-900">What I found</h2>
          {review.summary && <p className="mt-1 text-sm text-slate-600">{review.summary}</p>}
        </div>
        <button
          onClick={onClose}
          className="shrink-0 rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          aria-label="Close"
        >
          ✕
        </button>
      </div>

      {review.strengths?.length > 0 && (
        <ul className="mt-3 space-y-1">
          {review.strengths.map((strength, i) => (
            <li key={i} className="text-sm text-green-700">
              <span className="mr-1.5">✓</span>
              {strength}
            </li>
          ))}
        </ul>
      )}

      {findings.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">Nothing flagged.</p>
      ) : (
        <ul className="mt-4 space-y-3">
          {findings.map((finding, i) => {
            const tone = SEVERITY[finding.severity] ?? SEVERITY.medium;

            return (
              <li key={i} className="rounded-lg border border-slate-200 p-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`rounded-full border px-2 py-0.5 text-[11px] font-medium ${tone.chip}`}>
                    {tone.label}
                  </span>
                  <span className="text-[11px] uppercase tracking-wide text-slate-400">
                    {finding.section}
                  </span>
                  {/*
                    Rule findings are checkable facts; AI findings are judgement.
                    Worth telling apart before you act on one.
                  */}
                  <span className="text-[11px] text-slate-400">
                    {finding.fromRule ? "format check" : "written review"}
                  </span>
                </div>

                <p className="mt-1.5 text-sm font-medium text-slate-900">{finding.issue}</p>
                {finding.suggestion && (
                  <p className="mt-1 text-sm text-slate-600">{finding.suggestion}</p>
                )}
                {finding.example && (
                  <p className="mt-2 rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-700">
                    <span className="mr-1 text-xs uppercase tracking-wide text-slate-400">Try</span>
                    {finding.example}
                  </p>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ editor */

const field =
  "w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

function Editor({ content, edit }) {
  return (
    <>
      <Card title="Contact">
        <div className="grid gap-2 sm:grid-cols-2">
          {[
            ["name", "Name"],
            ["email", "Email"],
            ["phone", "Phone"],
            ["location", "Location"],
            ["linkedIn", "LinkedIn"],
            ["gitHub", "GitHub"],
            ["website", "Website"],
          ].map(([key, label]) => (
            <label key={key} className="block">
              <span className="text-xs text-slate-500">{label}</span>
              <input
                value={content.contact?.[key] ?? ""}
                onChange={(e) => edit((c) => {
                  c.contact = { ...c.contact, [key]: e.target.value };
                  return c;
                })}
                className={field}
              />
            </label>
          ))}
        </div>
      </Card>

      <Card title="Summary">
        <textarea
          rows={3}
          value={content.summary ?? ""}
          onChange={(e) => edit((c) => { c.summary = e.target.value; return c; })}
          placeholder="Optional. Two or three lines on what you are looking for."
          className={field}
        />
      </Card>

      <Card
        title="Education"
        onAdd={() => edit((c) => { c.education.push({ ...BLANK_EDUCATION }); return c; })}
      >
        {content.education.map((entry, i) => (
          <Entry key={i} onRemove={() => edit((c) => { c.education.splice(i, 1); return c; })}>
            <div className="grid gap-2 sm:grid-cols-2">
              <Field label="School" value={entry.school} onChange={(v) => edit((c) => { c.education[i].school = v; return c; })} />
              <Field label="Degree" value={entry.degree} onChange={(v) => edit((c) => { c.education[i].degree = v; return c; })} />
              <Field label="Start" value={entry.startDate} onChange={(v) => edit((c) => { c.education[i].startDate = v; return c; })} placeholder="Aug 2024" />
              <Field label="End" value={entry.endDate} onChange={(v) => edit((c) => { c.education[i].endDate = v; return c; })} placeholder="Expected May 2027" />
              <Field label="Location" value={entry.location} onChange={(v) => edit((c) => { c.education[i].location = v; return c; })} />
              <Field label="GPA" value={entry.gpa} onChange={(v) => edit((c) => { c.education[i].gpa = v; return c; })} />
            </div>
            <Bullets
              label="Details"
              items={entry.details}
              onChange={(items) => edit((c) => { c.education[i].details = items; return c; })}
            />
          </Entry>
        ))}
      </Card>

      <Card
        title="Experience"
        onAdd={() => edit((c) => { c.experience.push({ ...BLANK_EXPERIENCE, bullets: [""] }); return c; })}
      >
        {content.experience.map((entry, i) => (
          <Entry key={i} onRemove={() => edit((c) => { c.experience.splice(i, 1); return c; })}>
            <div className="grid gap-2 sm:grid-cols-2">
              <Field label="Title" value={entry.title} onChange={(v) => edit((c) => { c.experience[i].title = v; return c; })} />
              <Field label="Organization" value={entry.organization} onChange={(v) => edit((c) => { c.experience[i].organization = v; return c; })} />
              <Field label="Start" value={entry.startDate} onChange={(v) => edit((c) => { c.experience[i].startDate = v; return c; })} placeholder="Jun 2025" />
              <Field label="End" value={entry.endDate} onChange={(v) => edit((c) => { c.experience[i].endDate = v; return c; })} placeholder="Present" />
              <Field label="Location" value={entry.location} onChange={(v) => edit((c) => { c.experience[i].location = v; return c; })} />
            </div>
            <Bullets
              label="What you did"
              items={entry.bullets}
              onChange={(items) => edit((c) => { c.experience[i].bullets = items; return c; })}
            />
          </Entry>
        ))}
      </Card>

      <Card
        title="Projects"
        onAdd={() => edit((c) => { c.projects.push({ ...BLANK_PROJECT, bullets: [""] }); return c; })}
      >
        {content.projects.map((entry, i) => (
          <Entry key={i} onRemove={() => edit((c) => { c.projects.splice(i, 1); return c; })}>
            <div className="grid gap-2 sm:grid-cols-2">
              <Field label="Name" value={entry.name} onChange={(v) => edit((c) => { c.projects[i].name = v; return c; })} />
              <Field label="Link" value={entry.link} onChange={(v) => edit((c) => { c.projects[i].link = v; return c; })} />
              <Field label="Built with" value={entry.technologies} onChange={(v) => edit((c) => { c.projects[i].technologies = v; return c; })} />
            </div>
            <Bullets
              label="What it does"
              items={entry.bullets}
              onChange={(items) => edit((c) => { c.projects[i].bullets = items; return c; })}
            />
          </Entry>
        ))}
      </Card>

      <Card title="Skills">
        <textarea
          rows={3}
          value={content.skills.join(", ")}
          onChange={(e) => edit((c) => {
            // Split on save rather than per keystroke, or typing a comma would
            // immediately tear the word you are in the middle of.
            c.skills = e.target.value.split(",").map((s) => s.trim()).filter(Boolean);
            return c;
          })}
          placeholder="Python, SQL, React, public speaking"
          className={field}
        />
        <p className="mt-1 text-xs text-slate-500">Separate with commas.</p>
      </Card>
    </>
  );
}

function Card({ title, onAdd, children }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{title}</h2>
        {onAdd && (
          <button onClick={onAdd} className="text-sm font-medium text-indigo-600 hover:underline">
            Add
          </button>
        )}
      </div>
      <div className="space-y-4">{children}</div>
    </section>
  );
}

function Entry({ onRemove, children }) {
  return (
    <div className="relative rounded-lg border border-slate-200 p-3">
      <button
        onClick={onRemove}
        className="absolute right-2 top-2 text-xs text-slate-400 hover:text-red-600"
        aria-label="Remove"
      >
        ✕
      </button>
      <div className="space-y-2">{children}</div>
    </div>
  );
}

function Field({ label, value, onChange, placeholder }) {
  return (
    <label className="block">
      <span className="text-xs text-slate-500">{label}</span>
      <input
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className={field}
      />
    </label>
  );
}

function Bullets({ label, items, onChange }) {
  const list = items ?? [];

  return (
    <div>
      <span className="text-xs text-slate-500">{label}</span>

      {list.map((item, i) => (
        <div key={i} className="mt-1 flex gap-1.5">
          <textarea
            rows={2}
            value={item}
            onChange={(e) => {
              const next = [...list];
              next[i] = e.target.value;
              onChange(next);
            }}
            className={field}
          />
          <button
            onClick={() => onChange(list.filter((_, j) => j !== i))}
            className="shrink-0 px-1 text-xs text-slate-400 hover:text-red-600"
            aria-label="Remove line"
          >
            ✕
          </button>
        </div>
      ))}

      <button
        onClick={() => onChange([...list, ""])}
        className="mt-1.5 text-xs font-medium text-indigo-600 hover:underline"
      >
        Add a line
      </button>
    </div>
  );
}

/* ------------------------------------------------------------------- sheet */

/**
 * The resume itself.
 *
 * This is both the on-screen preview and what prints — the print stylesheet
 * hides everything except `.resume-sheet`. One layout, so what you see is what
 * comes out, which is the whole reason for printing from the browser rather
 * than generating a document on the server.
 */
function Sheet({ content }) {
  const { contact } = content;

  const links = [contact?.linkedIn, contact?.gitHub, contact?.website].filter(Boolean);
  const details = [contact?.email, contact?.phone, contact?.location].filter(Boolean);

  return (
    <div className="resume-sheet rounded-xl border border-slate-200 bg-white p-8 text-slate-900 shadow-sm">
      <header className="text-center">
        <h1 className="text-2xl font-bold tracking-tight">
          {contact?.name || "Your name"}
        </h1>
        {details.length > 0 && (
          <p className="mt-1 text-xs text-slate-600">{details.join("  ·  ")}</p>
        )}
        {links.length > 0 && (
          <p className="text-xs text-slate-600">{links.join("  ·  ")}</p>
        )}
      </header>

      {content.summary && (
        <Section heading="Summary">
          <p className="text-sm leading-snug">{content.summary}</p>
        </Section>
      )}

      {content.education.length > 0 && (
        <Section heading="Education">
          {content.education.map((entry, i) => (
            <div key={i} className="resume-entry mb-2 last:mb-0">
              <Line
                left={entry.school}
                right={[entry.startDate, entry.endDate].filter(Boolean).join(" – ")}
              />
              <Line
                left={[entry.degree, entry.gpa && `GPA ${entry.gpa}`].filter(Boolean).join("  ·  ")}
                right={entry.location}
                muted
              />
              <BulletList items={entry.details} />
            </div>
          ))}
        </Section>
      )}

      {content.experience.length > 0 && (
        <Section heading="Experience">
          {content.experience.map((entry, i) => (
            <div key={i} className="resume-entry mb-2.5 last:mb-0">
              <Line
                left={entry.organization}
                right={[entry.startDate, entry.endDate].filter(Boolean).join(" – ")}
              />
              <Line left={entry.title} right={entry.location} muted />
              <BulletList items={entry.bullets} />
            </div>
          ))}
        </Section>
      )}

      {content.projects.length > 0 && (
        <Section heading="Projects">
          {content.projects.map((entry, i) => (
            <div key={i} className="resume-entry mb-2.5 last:mb-0">
              <Line left={entry.name} right={entry.link} />
              {entry.technologies && (
                <p className="text-xs italic text-slate-600">{entry.technologies}</p>
              )}
              <BulletList items={entry.bullets} />
            </div>
          ))}
        </Section>
      )}

      {content.skills.length > 0 && (
        <Section heading="Skills">
          <p className="text-sm leading-snug">{content.skills.join("  ·  ")}</p>
        </Section>
      )}
    </div>
  );
}

function Section({ heading, children }) {
  return (
    <section className="mt-4">
      <h2 className="border-b border-slate-400 pb-0.5 text-xs font-bold uppercase tracking-widest">
        {heading}
      </h2>
      <div className="mt-1.5">{children}</div>
    </section>
  );
}

function Line({ left, right, muted }) {
  if (!left && !right) return null;

  return (
    <div className="flex items-baseline justify-between gap-4">
      <span className={muted ? "text-sm italic text-slate-700" : "text-sm font-semibold"}>
        {left}
      </span>
      {right && <span className="shrink-0 text-xs text-slate-600">{right}</span>}
    </div>
  );
}

function BulletList({ items }) {
  const list = (items ?? []).filter((i) => i && i.trim());
  if (list.length === 0) return null;

  return (
    <ul className="mt-0.5 list-disc space-y-0.5 pl-5 text-sm leading-snug">
      {list.map((item, i) => (
        <li key={i}>{item}</li>
      ))}
    </ul>
  );
}

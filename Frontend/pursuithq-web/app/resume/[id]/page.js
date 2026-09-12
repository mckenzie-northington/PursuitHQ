'use client';

import { useCallback, useEffect, useRef, useState } from "react";
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

/**
 * The sections that come with the app.
 *
 * Contact is deliberately not here: it is the header of the page, not a
 * section, so it cannot be moved below Education or taken off. Everything else
 * is yours to arrange.
 */
const BUILT_INS = [
  { key: "summary", label: "Summary" },
  { key: "education", label: "Education" },
  { key: "experience", label: "Experience" },
  { key: "projects", label: "Projects" },
  { key: "skills", label: "Skills" },
];

const newSectionId = () => `s${Math.random().toString(36).slice(2, 10)}`;

/**
 * Fills in what an older record does not have.
 *
 * Resumes saved before sections could be moved have no layout, and keep their
 * skills as a comma-separated list. Rather than migrate the database, they are
 * brought up to date the moment they are opened, and saved back in the new
 * shape the next time you press Save.
 */
function normalise(raw) {
  const c = structuredClone(raw ?? {});

  c.contact = c.contact ?? {};
  c.education = c.education ?? [];
  c.experience = c.experience ?? [];
  c.projects = c.projects ?? [];
  c.skills = c.skills ?? [];

  c.custom = (c.custom ?? []).map((section) => ({
    id: section.id || newSectionId(),
    title: section.title ?? "",
    body: section.body ?? "",
  }));

  if (!c.skillsText && c.skills.length > 0) c.skillsText = c.skills.join(", ");
  c.skillsText = c.skillsText ?? "";

  const known = [...BUILT_INS.map((s) => s.key), ...c.custom.map((s) => `custom:${s.id}`)];

  // Unknown and duplicate keys are dropped rather than rendered, or one bad
  // save would leave the page throwing every time it was opened.
  const layout = (c.layout ?? []).filter(
    (key, i, all) => known.includes(key) && all.indexOf(key) === i
  );

  // An empty layout means this resume predates arranging, not that every
  // section was removed - so it gets the default order, not a blank page.
  c.layout = layout.length > 0 ? layout : known;

  return c;
}

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
      setContent(normalise(resume.content));
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

/* --------------------------------------------------------------- free text */

/**
 * Splits free-typed text into paragraphs and bullet lists.
 *
 * The only markup is a leading -, * or • for a bullet and ** for bold, and it
 * becomes React elements rather than HTML. Nothing typed or pasted into one of
 * these boxes can turn into markup, which is the whole reason for storing plain
 * text instead of the contents of a rich text editor.
 */
function parseRich(text) {
  const blocks = [];
  let bullets = null;

  const flush = () => {
    if (bullets) {
      blocks.push({ type: "list", items: bullets });
      bullets = null;
    }
  };

  for (const raw of String(text ?? "").replace(/\r\n/g, "\n").split("\n")) {
    const line = raw.trim();

    if (!line) {
      flush();
      continue;
    }

    const bullet = /^[-*•]\s+(.*)$/.exec(line);

    if (bullet) {
      bullets = bullets ?? [];
      bullets.push(bullet[1]);
    } else {
      flush();
      blocks.push({ type: "paragraph", text: line });
    }
  }

  flush();
  return blocks;
}

/** **bold** and *italic*, as elements - never as HTML. */
function inline(text) {
  return String(text)
    .split(/(\*\*[^*]+\*\*|\*[^*\n]+\*)/g)
    .filter(Boolean)
    .map((part, i) => {
      if (part.startsWith("**") && part.endsWith("**") && part.length > 4) {
        return <strong key={i}>{part.slice(2, -2)}</strong>;
      }

      if (part.startsWith("*") && part.endsWith("*") && part.length > 2) {
        return <em key={i}>{part.slice(1, -1)}</em>;
      }

      return <span key={i}>{part}</span>;
    });
}

function Rich({ text }) {
  const blocks = parseRich(text);
  if (blocks.length === 0) return null;

  return (
    <div className="text-sm leading-snug">
      {blocks.map((block, i) =>
        block.type === "list" ? (
          <ul key={i} className="mt-0.5 list-disc space-y-0.5 pl-5 first:mt-0">
            {block.items.map((item, j) => (
              <li key={j}>{inline(item)}</li>
            ))}
          </ul>
        ) : (
          <p key={i} className="mt-1 first:mt-0">
            {inline(block.text)}
          </p>
        )
      )}
    </div>
  );
}

/**
 * A plain textarea with a small toolbar over it.
 *
 * Bold and bullets are stored as ** and "- ", but the buttons and Ctrl+B put
 * them in for you, so there is no need to know that. Type whatever you like,
 * including blank lines; the preview on the right shows how it will print.
 */
function RichEditor({ value, onChange, placeholder, rows = 5 }) {
  const ref = useRef(null);

  const text = value ?? "";

  // Restores the selection after React re-renders with the new value, so the
  // caret does not jump to the end every time you press a button.
  function restore(start, end) {
    requestAnimationFrame(() => {
      const el = ref.current;
      if (!el) return;
      el.focus();
      el.setSelectionRange(start, end);
    });
  }

  function wrap(marker) {
    const el = ref.current;
    if (!el) return;

    const { selectionStart: start, selectionEnd: end } = el;
    const selected = text.slice(start, end);

    onChange(text.slice(0, start) + marker + selected + marker + text.slice(end));
    restore(start + marker.length, start + marker.length + selected.length);
  }

  /** Toggles "- " on every line the selection touches. */
  function toggleBullets() {
    const el = ref.current;
    if (!el) return;

    const from = text.lastIndexOf("\n", Math.max(0, el.selectionStart - 1)) + 1;

    let to = text.indexOf("\n", el.selectionEnd);
    if (to === -1) to = text.length;

    const lines = text.slice(from, to).split("\n");
    const allBulleted = lines.every((line) => !line.trim() || /^\s*[-*•]\s/.test(line));

    const changed = lines
      .map((line) => {
        if (allBulleted) return line.replace(/^\s*[-*•]\s+/, "");
        return line.trim() ? `- ${line.trim()}` : line;
      })
      .join("\n");

    onChange(text.slice(0, from) + changed + text.slice(to));
    restore(from, from + changed.length);
  }

  return (
    <div>
      <div className="mb-1 flex items-center gap-1">
        <ToolButton onClick={() => wrap("**")} label="Bold (Ctrl+B)">
          <span className="font-bold">B</span>
        </ToolButton>
        <ToolButton onClick={() => wrap("*")} label="Italic (Ctrl+I)">
          <span className="italic">I</span>
        </ToolButton>
        <ToolButton onClick={toggleBullets} label="Bullet list">
          • List
        </ToolButton>
      </div>

      <textarea
        ref={ref}
        rows={rows}
        value={text}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => {
          if (!(e.ctrlKey || e.metaKey)) return;

          if (e.key === "b" || e.key === "B") {
            e.preventDefault();
            wrap("**");
          } else if (e.key === "i" || e.key === "I") {
            e.preventDefault();
            wrap("*");
          }
        }}
        className={`${field} leading-relaxed`}
      />

      <p className="mt-1 text-xs text-slate-500">
        Type freely. A line starting with &ldquo;-&rdquo; prints as a bullet, and
        **stars** print as bold.
      </p>
    </div>
  );
}

function ToolButton({ onClick, label, children }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={label}
      aria-label={label}
      className="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-600 transition hover:bg-slate-100"
    >
      {children}
    </button>
  );
}

/* ------------------------------------------------------------------ editor */

const field =
  "w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-sm outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

function Editor({ content, edit }) {
  const layout = content.layout;

  function move(from, to) {
    if (to < 0 || to >= layout.length) return;

    edit((c) => {
      const [key] = c.layout.splice(from, 1);
      c.layout.splice(to, 0, key);
      return c;
    });
  }

  /**
   * Takes a section off the resume without throwing its contents away.
   *
   * Only the layout changes, so putting Education back is one click rather
   * than retyping four years of it - and a mis-click costs nothing.
   */
  function remove(key) {
    edit((c) => {
      c.layout = c.layout.filter((k) => k !== key);
      return c;
    });
  }

  return (
    <>
      {/* Not part of the layout: the header of the page, not a section. */}
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

      {layout.map((key, index) => (
        <SectionCard
          key={key}
          sectionKey={key}
          content={content}
          edit={edit}
          index={index}
          count={layout.length}
          onMoveUp={() => move(index, index - 1)}
          onMoveDown={() => move(index, index + 1)}
          onRemove={() => remove(key)}
        />
      ))}

      <AddSection content={content} edit={edit} />
    </>
  );
}

function SectionCard({ sectionKey, content, edit, index, count, onMoveUp, onMoveDown, onRemove }) {
  const custom = sectionKey.startsWith("custom:")
    ? content.custom.find((s) => `custom:${s.id}` === sectionKey)
    : null;

  // A layout key with nothing behind it would render an untitled, unfillable
  // card. Skipping it is quieter than crashing and it heals on the next save.
  if (sectionKey.startsWith("custom:") && !custom) return null;

  const builtIn = BUILT_INS.find((s) => s.key === sectionKey);

  const canAdd = sectionKey === "education" || sectionKey === "experience" || sectionKey === "projects";

  function add() {
    edit((c) => {
      if (sectionKey === "education") c.education.push({ ...BLANK_EDUCATION, details: [] });
      if (sectionKey === "experience") c.experience.push({ ...BLANK_EXPERIENCE, bullets: [""] });
      if (sectionKey === "projects") c.projects.push({ ...BLANK_PROJECT, bullets: [""] });
      return c;
    });
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        {custom ? (
          <input
            value={custom.title ?? ""}
            onChange={(e) => edit((c) => {
              const target = c.custom.find((s) => s.id === custom.id);
              if (target) target.title = e.target.value;
              return c;
            })}
            placeholder="Section heading"
            className="min-w-0 flex-1 border-b border-transparent bg-transparent text-sm font-semibold uppercase tracking-wide text-slate-600 outline-none transition hover:border-slate-300 focus:border-indigo-500"
          />
        ) : (
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
            {builtIn?.label ?? sectionKey}
          </h2>
        )}

        <div className="flex shrink-0 items-center gap-1">
          {canAdd && (
            <button
              onClick={add}
              className="mr-1 text-sm font-medium text-indigo-600 hover:underline"
            >
              Add
            </button>
          )}

          {/*
            Arrows rather than dragging: they work with a keyboard and on a
            phone, and there is no half-dropped state to get wrong.
          */}
          <ToolButton onClick={onMoveUp} label="Move section up">
            {index === 0 ? <span className="text-slate-300">↑</span> : "↑"}
          </ToolButton>
          <ToolButton onClick={onMoveDown} label="Move section down">
            {index === count - 1 ? <span className="text-slate-300">↓</span> : "↓"}
          </ToolButton>
          <button
            type="button"
            onClick={onRemove}
            title="Take this section off the resume"
            aria-label="Remove section"
            className="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-500 transition hover:border-red-200 hover:bg-red-50 hover:text-red-600"
          >
            ✕
          </button>
        </div>
      </div>

      <div className="space-y-4">
        <SectionBody sectionKey={sectionKey} custom={custom} content={content} edit={edit} />
      </div>
    </section>
  );
}

function SectionBody({ sectionKey, custom, content, edit }) {
  if (custom) {
    return (
      <RichEditor
        value={custom.body}
        rows={6}
        placeholder={"AWS Certified Cloud Practitioner — May 2026\n- Treasurer, Robotics Club"}
        onChange={(next) => edit((c) => {
          const target = c.custom.find((s) => s.id === custom.id);
          if (target) target.body = next;
          return c;
        })}
      />
    );
  }

  if (sectionKey === "summary") {
    return (
      <textarea
        rows={3}
        value={content.summary ?? ""}
        onChange={(e) => edit((c) => { c.summary = e.target.value; return c; })}
        placeholder="Optional. Two or three lines on what you are looking for."
        className={field}
      />
    );
  }

  if (sectionKey === "skills") {
    return (
      <RichEditor
        value={content.skillsText}
        rows={5}
        placeholder={"**Languages:** Python, C#, JavaScript\n**Tools:** Git, PostgreSQL, Figma"}
        onChange={(next) => edit((c) => {
          c.skillsText = next;

          // The old list is emptied once free text exists, so nothing can show
          // up twice if an older copy of the page ever reads this record.
          c.skills = [];
          return c;
        })}
      />
    );
  }

  if (sectionKey === "education") {
    return content.education.map((entry, i) => (
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
    ));
  }

  if (sectionKey === "experience") {
    return content.experience.map((entry, i) => (
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
    ));
  }

  if (sectionKey === "projects") {
    return content.projects.map((entry, i) => (
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
    ));
  }

  return null;
}

/**
 * Puts a section back, or makes a new one.
 *
 * Removed built-in sections are offered here by name, with their contents
 * still intact, so taking one off is never a decision you have to be sure
 * about before you make it.
 */
function AddSection({ content, edit }) {
  const missing = BUILT_INS.filter((s) => !content.layout.includes(s.key));

  function addBuiltIn(key) {
    edit((c) => {
      if (!c.layout.includes(key)) c.layout.push(key);
      return c;
    });
  }

  function addCustom() {
    edit((c) => {
      const id = newSectionId();
      c.custom.push({ id, title: "New section", body: "" });
      c.layout.push(`custom:${id}`);
      return c;
    });
  }

  return (
    <section className="rounded-xl border border-dashed border-slate-300 p-4">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-400">Add a section</h2>

      <div className="mt-2 flex flex-wrap gap-2">
        {missing.map((section) => (
          <button
            key={section.key}
            onClick={() => addBuiltIn(section.key)}
            className="rounded-md border border-slate-300 px-3 py-1 text-sm text-slate-700 transition hover:bg-slate-50"
          >
            + {section.label}
          </button>
        ))}

        <button
          onClick={addCustom}
          className="rounded-md border border-indigo-300 px-3 py-1 text-sm font-medium text-indigo-700 transition hover:bg-indigo-50"
        >
          + Your own section
        </button>
      </div>

      {missing.length > 0 && (
        <p className="mt-2 text-xs text-slate-500">
          Anything you took off kept its contents. Putting it back brings them with it.
        </p>
      )}
    </section>
  );
}

function Card({ title, children }) {
  return (
    <section className="rounded-xl border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{title}</h2>
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
 *
 * Sections come out in the order the editor put them in, and a section that is
 * not in the layout is not on the page at all.
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

      {content.layout.map((key) => (
        <SheetSection key={key} sectionKey={key} content={content} />
      ))}
    </div>
  );
}

function SheetSection({ sectionKey, content }) {
  if (sectionKey.startsWith("custom:")) {
    const custom = content.custom.find((s) => `custom:${s.id}` === sectionKey);

    // An empty section prints as a heading with nothing under it, which looks
    // like a mistake on paper. The format check says so; the page just skips it.
    if (!custom || (!custom.title?.trim() && !custom.body?.trim())) return null;

    return (
      <Section heading={custom.title?.trim() || "Untitled"}>
        <Rich text={custom.body} />
      </Section>
    );
  }

  if (sectionKey === "summary") {
    if (!content.summary) return null;

    return (
      <Section heading="Summary">
        <p className="text-sm leading-snug">{content.summary}</p>
      </Section>
    );
  }

  if (sectionKey === "skills") {
    const text = content.skillsText?.trim()
      ? content.skillsText
      : content.skills.join("  ·  ");

    if (!text.trim()) return null;

    return (
      <Section heading="Skills">
        <Rich text={text} />
      </Section>
    );
  }

  if (sectionKey === "education") {
    if (content.education.length === 0) return null;

    return (
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
    );
  }

  if (sectionKey === "experience") {
    if (content.experience.length === 0) return null;

    return (
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
    );
  }

  if (sectionKey === "projects") {
    if (content.projects.length === 0) return null;

    return (
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
    );
  }

  return null;
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

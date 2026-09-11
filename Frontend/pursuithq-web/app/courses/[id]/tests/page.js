'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  courses as coursesApi,
  flashcards as flashcardsApi,
  materials as materialsApi,
  notes as notesApi,
  quizzes as quizzesApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

const MIN_QUESTIONS = 5;
const MAX_QUESTIONS = 30;

/**
 * The question types, each one switched on or off.
 *
 * The API takes this as a sentence rather than a set of flags, because the
 * model reads "multiple choice and written questions" perfectly well and
 * because it leaves room for a request no set of checkboxes could express.
 * The toggles just build that sentence for you.
 */
const TYPES = [
  { key: "multiple_choice", label: "Multiple choice", phrase: "multiple choice" },
  { key: "true_false", label: "True / false", phrase: "true/false" },
  { key: "short_answer", label: "Free response", phrase: "free response (written)" },
];

function describe(selected) {
  const phrases = TYPES.filter((t) => selected.includes(t.key)).map((t) => t.phrase);

  if (phrases.length === 0) return "";
  if (phrases.length === 1) return `Only ${phrases[0]} questions.`;

  const last = phrases.pop();
  return `Use only these question types: ${phrases.join(", ")} and ${last}.`;
}

export default function CourseTestsPage() {
  const { id } = useParams();
  const courseId = Number(id);
  const { user, loading } = useAuth();

  const [course, setCourse] = useState(null);
  const [tests, setTests] = useState([]);
  const [sources, setSources] = useState([]);
  const [ai, setAi] = useState(null);

  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const [source, setSource] = useState("");
  const [count, setCount] = useState("10");
  const [types, setTypes] = useState(TYPES.map((t) => t.key));
  const [notes, setNotes] = useState("");
  const [generating, setGenerating] = useState(false);

  const load = useCallback(async () => {
    try {
      const [courseData, testList, files, noteList, aiStatus] = await Promise.all([
        coursesApi.get(courseId),
        quizzesApi.list(courseId),
        materialsApi.list(courseId),
        notesApi.list(courseId),
        flashcardsApi.aiStatus(),
      ]);

      setCourse(courseData);
      setTests(testList);
      setAi(aiStatus);
      setSources([
        ...files.map((f) => ({ key: `m${f.id}`, id: f.id, kind: "material", name: f.fileName })),
        ...noteList.map((n) => ({ key: `n${n.id}`, id: n.id, kind: "note", name: n.title })),
      ]);
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

  async function generate(e) {
    e.preventDefault();

    const chosen = sources.find((s) => s.key === source);
    if (!chosen) return;

    const wanted = Number(count);

    if (!Number.isInteger(wanted) || wanted < MIN_QUESTIONS || wanted > MAX_QUESTIONS) {
      setError(`Pick a number of questions between ${MIN_QUESTIONS} and ${MAX_QUESTIONS}.`);
      return;
    }

    if (types.length === 0) {
      setError("Pick at least one question type.");
      return;
    }

    setGenerating(true);
    setError("");

    try {
      const test = await quizzesApi.generate({
        courseId,
        sourceMaterialId: chosen.kind === "material" ? chosen.id : null,
        sourceNoteId: chosen.kind === "note" ? chosen.id : null,
        count: Number(count),
        style: [describe(types), notes.trim()].filter(Boolean).join(" "),
      });

      setTests((current) => [test, ...current]);
      setAi((a) => (a ? { ...a, remaining: a.remaining - 1 } : a));
      setSource("");
    } catch (err) {
      setError(err.message);
    } finally {
      setGenerating(false);
    }
  }

  async function remove(test) {
    if (!confirm(`Delete "${test.title}" and its results?`)) return;

    try {
      await quizzesApi.remove(test.id);
      setTests((current) => current.filter((t) => t.id !== test.id));
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-4xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const noSources = sources.length === 0;
  const blocked = (ai && !ai.configured) || (ai && ai.remaining <= 0);

  const field =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="mx-auto max-w-4xl px-6 py-10">
      <Link href="/courses" className="text-sm text-indigo-600 hover:underline">
        &larr; Courses
      </Link>

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Practice tests</h1>
          <p className="mt-1 text-sm text-slate-600">
            {course?.name}
            {ai?.configured && (
              <span className="text-slate-400">
                {" "}· {ai.remaining} of {ai.limit} AI requests left today
              </span>
            )}
          </p>
        </div>
        <div className="flex gap-2">
          <Link
            href={`/courses/${courseId}/flashcards`}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
          >
            Flashcards
          </Link>
          <Link
            href={`/courses/${courseId}/materials`}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
          >
            Materials
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

      <form onSubmit={generate} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-medium text-slate-900">Make a new test</h2>
        <p className="mt-1 text-sm text-slate-600">
          Written answers are graded on meaning, not wording.
        </p>

        {noSources ? (
          <div className="mt-4 rounded-md border border-dashed border-slate-300 bg-slate-50 px-4 py-6 text-center text-sm text-slate-600">
            <p>Nothing to make a test from yet.</p>
            <Link
              href={`/courses/${courseId}/materials`}
              className="mt-2 inline-block font-medium text-indigo-600 hover:underline"
            >
              Upload a lecture PDF or slides first
            </Link>
          </div>
        ) : (
          <>
            <div className="mt-4 grid gap-4 sm:grid-cols-3">
              <div className="sm:col-span-2">
                <label className="block text-sm font-medium text-slate-700">Test on</label>
                <select
                  required
                  value={source}
                  onChange={(e) => setSource(e.target.value)}
                  className={field}
                >
                  <option value="">Choose a file or note</option>
                  {sources.map((s) => (
                    <option key={s.key} value={s.key}>
                      {s.kind === "note" ? "Note: " : ""}
                      {s.name}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-700">Questions</label>
                <input
                  type="number"
                  min={MIN_QUESTIONS}
                  max={MAX_QUESTIONS}
                  required
                  value={count}
                  onChange={(e) => setCount(e.target.value)}
                  className={field}
                />
                <p className="mt-1 text-xs text-slate-500">
                  {MIN_QUESTIONS}–{MAX_QUESTIONS}
                </p>
              </div>
            </div>

            <div className="mt-4">
              <label className="block text-sm font-medium text-slate-700">Question types</label>
              <div className="mt-1.5 flex flex-wrap gap-2">
                {TYPES.map((t) => {
                  const on = types.includes(t.key);

                  return (
                    <button
                      key={t.key}
                      type="button"
                      aria-pressed={on}
                      onClick={() =>
                        setTypes((current) =>
                          on ? current.filter((k) => k !== t.key) : [...current, t.key]
                        )
                      }
                      className={`rounded-md border px-3 py-1.5 text-sm font-medium transition ${
                        on
                          ? "border-indigo-600 bg-indigo-600 text-white"
                          : "border-slate-300 text-slate-600 hover:bg-slate-50"
                      }`}
                    >
                      {on && <span className="mr-1.5">✓</span>}
                      {t.label}
                    </button>
                  );
                })}
              </div>
              {types.length === 0 && (
                <p className="mt-1.5 text-xs text-red-600">Pick at least one.</p>
              )}
            </div>

            <div className="mt-4">
              <label className="block text-sm font-medium text-slate-700">
                Anything else? <span className="font-normal text-slate-400">Optional</span>
              </label>
              <input
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="e.g. focus on the second half, or make it harder than usual"
                className={field}
              />
            </div>

            {blocked && (
              <p className="mt-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                {ai.configured
                  ? `You have used all ${ai.limit} AI requests for today. The count resets at midnight.`
                  : "AI is not set up on the server, so tests cannot be generated."}
              </p>
            )}

            <div className="mt-4 flex items-center gap-3">
              <button
                type="submit"
                disabled={!source || generating || blocked || types.length === 0}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                {generating ? "Writing questions..." : "Generate test"}
              </button>
              {generating && (
                <span className="text-sm text-slate-500">
                  Reading the document — this takes up to a minute.
                </span>
              )}
            </div>
          </>
        )}
      </form>

      <h2 className="mt-8 font-medium text-slate-900">Your tests</h2>

      {tests.length === 0 ? (
        <div className="mt-3 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-10 text-center text-slate-600">
          No tests yet.
        </div>
      ) : (
        <ul className="mt-3 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {tests.map((test) => (
            <li key={test.id} className="flex items-center justify-between gap-4 px-4 py-3">
              <Link href={`/tests/${test.id}`} className="min-w-0 flex-1">
                <p className="truncate font-medium text-slate-900">{test.title}</p>
                <p className="truncate text-sm text-slate-500">
                  {test.questionCount} questions
                  {test.attemptCount > 0
                    ? ` · taken ${test.attemptCount}× · best ${test.bestScore}%`
                    : " · not taken yet"}
                  {test.sourceName && ` · from ${test.sourceName}`}
                </p>
              </Link>

              <div className="flex shrink-0 items-center gap-2">
                <Link
                  href={`/tests/${test.id}`}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
                >
                  {test.attemptCount > 0 ? "Retake" : "Take it"}
                </Link>
                <button
                  onClick={() => remove(test)}
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

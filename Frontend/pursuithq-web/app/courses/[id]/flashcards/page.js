'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  courses as coursesApi,
  flashcards as flashcardsApi,
  materials as materialsApi,
  notes as notesApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

const COUNTS = [10, 15, 20, 30];

export default function CourseFlashcardsPage() {
  const { id } = useParams();
  const courseId = Number(id);
  const { user, loading } = useAuth();

  const [course, setCourse] = useState(null);
  const [decks, setDecks] = useState([]);
  const [sources, setSources] = useState([]);
  const [ai, setAi] = useState(null);

  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const [source, setSource] = useState("");
  const [count, setCount] = useState(15);
  const [generating, setGenerating] = useState(false);

  const load = useCallback(async () => {
    try {
      const [courseData, deckList, files, noteList, aiStatus] = await Promise.all([
        coursesApi.get(courseId),
        flashcardsApi.listDecks(courseId),
        materialsApi.list(courseId),
        notesApi.list(courseId),
        flashcardsApi.aiStatus(),
      ]);

      setCourse(courseData);
      setDecks(deckList);
      setAi(aiStatus);

      // One list for the dropdown, because to the student "what do I want to
      // study from" is one question, not two.
      setSources([
        ...files.map((f) => ({ key: `material-${f.id}`, id: f.id, kind: "material", name: f.fileName })),
        ...noteList.map((n) => ({ key: `note-${n.id}`, id: n.id, kind: "note", name: n.title })),
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

    setGenerating(true);
    setError("");

    try {
      const deck = await flashcardsApi.generate({
        courseId,
        sourceMaterialId: chosen.kind === "material" ? chosen.id : null,
        sourceNoteId: chosen.kind === "note" ? chosen.id : null,
        count: Number(count),
      });

      setDecks((current) => [deck, ...current]);
      setAi((current) => (current ? { ...current, remaining: current.remaining - 1 } : current));
      setSource("");
    } catch (err) {
      setError(err.message);
    } finally {
      setGenerating(false);
    }
  }

  async function removeDeck(deck) {
    if (!confirm(`Delete "${deck.title}" and its ${deck.cardCount} cards?`)) return;

    try {
      await flashcardsApi.removeDeck(deck.id);
      setDecks((current) => current.filter((d) => d.id !== deck.id));
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-4xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const noSources = sources.length === 0;
  const aiOff = ai && !ai.configured;
  const outOfRequests = ai && ai.remaining <= 0;
  const canGenerate = !noSources && !aiOff && !outOfRequests && source && !generating;

  return (
    <div className="mx-auto max-w-4xl px-6 py-10">
      <Link href="/courses" className="text-sm text-indigo-600 hover:underline">
        &larr; Courses
      </Link>

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Flashcards</h1>
          <p className="mt-1 text-sm text-slate-600">
            {course?.name}
            {ai?.configured && (
              <span className="text-slate-400">
                {" "}· {ai.remaining} of {ai.limit} AI requests left today
              </span>
            )}
          </p>
        </div>
        <Link
          href={`/courses/${courseId}/materials`}
          className="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
        >
          Materials
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

      {/* Generate */}
      <form onSubmit={generate} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-medium text-slate-900">Make a new deck</h2>
        <p className="mt-1 text-sm text-slate-600">
          Pick a file or note from this course and AI writes the cards from it.
        </p>

        {noSources ? (
          <div className="mt-4 rounded-md border border-dashed border-slate-300 bg-slate-50 px-4 py-6 text-center text-sm text-slate-600">
            <p>Nothing to study from yet.</p>
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
                <label className="block text-sm font-medium text-slate-700">Study from</label>
                <select
                  required
                  value={source}
                  onChange={(e) => setSource(e.target.value)}
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
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
                <label className="block text-sm font-medium text-slate-700">Cards</label>
                <select
                  value={count}
                  onChange={(e) => setCount(e.target.value)}
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
                >
                  {COUNTS.map((n) => (
                    <option key={n} value={n}>
                      {n}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {aiOff && (
              <p className="mt-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                AI is not set up on the server, so cards cannot be generated. The Ai:ApiKey user
                secret is missing.
              </p>
            )}

            {outOfRequests && !aiOff && (
              <p className="mt-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                You have used all {ai.limit} AI requests for today. The count resets at midnight.
              </p>
            )}

            <div className="mt-4 flex items-center gap-3">
              <button
                type="submit"
                disabled={!canGenerate}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                {generating ? "Writing cards..." : "Generate flashcards"}
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

      {/* Decks */}
      <h2 className="mt-8 font-medium text-slate-900">Your decks</h2>

      {decks.length === 0 ? (
        <div className="mt-3 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-10 text-center text-slate-600">
          No decks yet.
        </div>
      ) : (
        <ul className="mt-3 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {decks.map((deck) => (
            <li key={deck.id} className="flex items-center justify-between gap-4 px-4 py-3">
              <Link href={`/decks/${deck.id}`} className="min-w-0 flex-1">
                <p className="truncate font-medium text-slate-900">{deck.title}</p>
                <p className="truncate text-sm text-slate-500">
                  {deck.cardCount} {deck.cardCount === 1 ? "card" : "cards"}
                  {deck.isAiGenerated && " · AI generated"}
                  {deck.sourceName && ` · from ${deck.sourceName}`}
                </p>
              </Link>

              <div className="flex shrink-0 items-center gap-2">
                <Link
                  href={`/decks/${deck.id}`}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
                >
                  Study
                </Link>
                <button
                  onClick={() => removeDeck(deck)}
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

'use client';

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { flashcards as flashcardsApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function DeckPage() {
  const { deckId } = useParams();
  const id = Number(deckId);
  const { user, loading } = useAuth();

  const [deck, setDeck] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [mode, setMode] = useState("study");

  const load = useCallback(async () => {
    try {
      setDeck(await flashcardsApi.getDeck(id));
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [id]);

  useEffect(() => {
    if (loading || !user) return;
    load();
  }, [loading, user, load]);

  if (loading || !ready) {
    return <div className="mx-auto max-w-3xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (!deck) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <p className="text-slate-600">{error || "That deck could not be loaded."}</p>
        <Link href="/courses" className="mt-3 inline-block text-indigo-600 hover:underline">
          Back to courses
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      {deck.courseId && (
        <Link
          href={`/courses/${deck.courseId}/flashcards`}
          className="text-sm text-indigo-600 hover:underline"
        >
          &larr; {deck.courseName || "Flashcards"}
        </Link>
      )}

      <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{deck.title}</h1>
          <p className="mt-1 text-sm text-slate-600">
            {deck.cards.length} {deck.cards.length === 1 ? "card" : "cards"}
            {deck.sourceName && ` · from ${deck.sourceName}`}
          </p>
        </div>

        <div className="flex rounded-md border border-slate-300 p-0.5">
          {["study", "edit"].map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`rounded px-3 py-1 text-sm font-medium capitalize transition ${
                mode === m ? "bg-indigo-600 text-white" : "text-slate-600 hover:bg-slate-100"
              }`}
            >
              {m}
            </button>
          ))}
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

      {mode === "study" ? (
        <StudyMode deck={deck} onError={setError} />
      ) : (
        <EditMode deck={deck} setDeck={setDeck} onError={setError} />
      )}
    </div>
  );
}

/**
 * One card at a time: read the front, answer it in your head, flip, then say
 * whether you had it.
 *
 * The self-grade is the point. Flipping a card and thinking "yes, I knew that"
 * is how people convince themselves they have studied; having to press one of
 * two buttons makes it a decision, and the counters make it visible when the
 * same card keeps coming back.
 */
function StudyMode({ deck, onError }) {
  const [order, setOrder] = useState(() => deck.cards.map((_, i) => i));
  const [position, setPosition] = useState(0);
  const [flipped, setFlipped] = useState(false);
  const [score, setScore] = useState({ right: 0, wrong: 0 });

  const card = deck.cards[order[position]];
  const done = position >= order.length;

  const restart = useCallback((shuffle) => {
    const next = deck.cards.map((_, i) => i);

    if (shuffle) {
      // Fisher-Yates. Sorting by Math.random() is the common shortcut and it
      // does not produce an even shuffle.
      for (let i = next.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [next[i], next[j]] = [next[j], next[i]];
      }
    }

    setOrder(next);
    setPosition(0);
    setFlipped(false);
    setScore({ right: 0, wrong: 0 });
  }, [deck.cards]);

  async function answer(correct) {
    setScore((s) => ({
      right: s.right + (correct ? 1 : 0),
      wrong: s.wrong + (correct ? 0 : 1),
    }));
    setFlipped(false);
    setPosition((p) => p + 1);

    // Saved in the background - a slow request should never hold up the next
    // card, and a failed one is not worth stopping a study session over.
    try {
      await flashcardsApi.review(deck.id, card.id, correct);
    } catch (err) {
      onError(`Could not save your answer — ${err.message}`);
    }
  }

  if (deck.cards.length === 0) {
    return (
      <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center text-slate-600">
        This deck has no cards. Switch to Edit to add some.
      </div>
    );
  }

  if (done) {
    const total = score.right + score.wrong;
    const percent = total === 0 ? 0 : Math.round((score.right / total) * 100);

    return (
      <div className="mt-6 rounded-xl border border-slate-200 bg-white px-6 py-12 text-center">
        <p className="text-3xl font-semibold text-slate-900">{percent}%</p>
        <p className="mt-2 text-slate-600">
          {score.right} right · {score.wrong} to work on
        </p>

        <div className="mt-6 flex justify-center gap-2">
          <button
            onClick={() => restart(true)}
            className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
          >
            Shuffle and go again
          </button>
          <button
            onClick={() => restart(false)}
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            Same order
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="mt-6">
      <div className="flex items-center justify-between text-sm text-slate-500">
        <span>
          Card {position + 1} of {order.length}
        </span>
        <button onClick={() => restart(true)} className="font-medium text-indigo-600 hover:underline">
          Shuffle
        </button>
      </div>

      <div className="mt-2 h-1 overflow-hidden rounded-full bg-slate-200">
        <div
          className="h-full rounded-full bg-indigo-600 transition-all"
          style={{ width: `${(position / order.length) * 100}%` }}
        />
      </div>

      <button
        type="button"
        onClick={() => setFlipped((f) => !f)}
        className="mt-4 flex min-h-[16rem] w-full flex-col items-center justify-center rounded-xl border border-slate-200 bg-white px-8 py-10 text-center transition hover:border-slate-300"
      >
        <span className="text-xs font-medium uppercase tracking-wide text-slate-400">
          {flipped ? "Answer" : "Question"}
        </span>
        <span className="mt-3 whitespace-pre-wrap text-lg text-slate-900">
          {flipped ? card.back : card.front}
        </span>
        {!flipped && (
          <span className="mt-6 text-xs text-slate-400">Click to see the answer</span>
        )}
      </button>

      {flipped ? (
        <div className="mt-4 grid grid-cols-2 gap-3">
          <button
            onClick={() => answer(false)}
            className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700 transition hover:bg-red-100"
          >
            Missed it
          </button>
          <button
            onClick={() => answer(true)}
            className="rounded-md border border-green-200 bg-green-50 px-4 py-3 text-sm font-medium text-green-700 transition hover:bg-green-100"
          >
            Got it
          </button>
        </div>
      ) : (
        <p className="mt-4 text-center text-sm text-slate-500">
          Answer it in your head first, then flip.
        </p>
      )}
    </div>
  );
}

/** Fix what the AI got wrong, add what it missed. */
function EditMode({ deck, setDeck, onError }) {
  const [editing, setEditing] = useState(null);
  const [draft, setDraft] = useState({ front: "", back: "" });
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);

  const stats = useMemo(() => {
    const reviewed = deck.cards.filter((c) => c.timesReviewed > 0);
    if (reviewed.length === 0) return null;

    const correct = reviewed.reduce((sum, c) => sum + c.timesCorrect, 0);
    const total = reviewed.reduce((sum, c) => sum + c.timesReviewed, 0);

    return { percent: Math.round((correct / total) * 100), reviewed: reviewed.length };
  }, [deck.cards]);

  function startEdit(card) {
    setEditing(card.id);
    setAdding(false);
    setDraft({ front: card.front, back: card.back });
  }

  function startAdd() {
    setAdding(true);
    setEditing(null);
    setDraft({ front: "", back: "" });
  }

  async function save(e) {
    e.preventDefault();
    setBusy(true);

    try {
      if (adding) {
        const card = await flashcardsApi.addCard(deck.id, draft);
        setDeck({ ...deck, cards: [...deck.cards, card], cardCount: deck.cardCount + 1 });
      } else {
        const card = await flashcardsApi.updateCard(deck.id, editing, draft);
        setDeck({
          ...deck,
          cards: deck.cards.map((c) => (c.id === card.id ? card : c)),
        });
      }

      setEditing(null);
      setAdding(false);
    } catch (err) {
      onError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function remove(card) {
    if (!confirm("Delete this card?")) return;

    try {
      await flashcardsApi.removeCard(deck.id, card.id);
      setDeck({
        ...deck,
        cards: deck.cards.filter((c) => c.id !== card.id),
        cardCount: deck.cardCount - 1,
      });
    } catch (err) {
      onError(err.message);
    }
  }

  const field =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="mt-6">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-500">
          {stats
            ? `${stats.percent}% right across ${stats.reviewed} cards you have reviewed`
            : "No review history yet."}
        </p>
        <button
          onClick={startAdd}
          className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
        >
          Add card
        </button>
      </div>

      {(adding || editing !== null) && (
        <form onSubmit={save} className="mt-4 rounded-xl border border-slate-200 bg-white p-4">
          <label className="block text-xs font-medium uppercase tracking-wide text-slate-500">
            Front
          </label>
          <textarea
            required
            rows={2}
            value={draft.front}
            onChange={(e) => setDraft({ ...draft, front: e.target.value })}
            className={field}
          />

          <label className="mt-3 block text-xs font-medium uppercase tracking-wide text-slate-500">
            Back
          </label>
          <textarea
            required
            rows={3}
            value={draft.back}
            onChange={(e) => setDraft({ ...draft, back: e.target.value })}
            className={field}
          />

          <div className="mt-3 flex gap-2">
            <button
              type="submit"
              disabled={busy}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Saving..." : "Save"}
            </button>
            <button
              type="button"
              onClick={() => {
                setEditing(null);
                setAdding(false);
              }}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      <ul className="mt-4 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
        {deck.cards.map((card, index) => (
          <li key={card.id} className="flex items-start justify-between gap-4 px-4 py-3">
            <div className="min-w-0">
              <p className="text-sm font-medium text-slate-900">
                <span className="mr-2 text-slate-400">{index + 1}.</span>
                {card.front}
              </p>
              <p className="mt-1 whitespace-pre-wrap text-sm text-slate-600">{card.back}</p>
              {card.timesReviewed > 0 && (
                <p className="mt-1 text-xs text-slate-400">
                  {card.timesCorrect} of {card.timesReviewed} right
                </p>
              )}
            </div>

            <div className="flex shrink-0 gap-3 text-sm">
              <button
                onClick={() => startEdit(card)}
                className="text-indigo-600 hover:underline"
              >
                Edit
              </button>
              <button
                onClick={() => remove(card)}
                className="text-slate-400 transition hover:text-red-600"
              >
                Delete
              </button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  courses as coursesApi,
  flashcards as flashcardsApi,
  materials as materialsApi,
  notes as notesApi,
  study as studyApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Markdown from "@/components/Markdown";

const SUGGESTIONS = [
  "Make me a study guide for the midterm",
  "Explain the hardest concept in these slides",
  "Quiz me on this material",
  "What should I focus on first?",
];

export default function StudyPage() {
  const { user, loading } = useAuth();

  const [courseList, setCourseList] = useState([]);
  const [courseId, setCourseId] = useState(null);

  const [conversation, setConversation] = useState(null);
  const [conversationList, setConversationList] = useState([]);
  const [guides, setGuides] = useState([]);
  const [sources, setSources] = useState([]);
  const [ai, setAi] = useState(null);

  const [question, setQuestion] = useState("");
  const [thinking, setThinking] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [showSources, setShowSources] = useState(true);
  const [openGuide, setOpenGuide] = useState(null);

  const bottom = useRef(null);

  // --- loading -------------------------------------------------------------

  useEffect(() => {
    if (loading || !user) return;

    (async () => {
      try {
        const [list, aiStatus] = await Promise.all([
          coursesApi.list(),
          flashcardsApi.aiStatus(),
        ]);

        setCourseList(list);
        setAi(aiStatus);
        if (list.length > 0) setCourseId(list[0].id);
      } catch (err) {
        setError(err.message);
      } finally {
        setReady(true);
      }
    })();
  }, [loading, user]);

  const loadCourse = useCallback(async () => {
    if (!courseId) return;

    try {
      const [conversations, guideList, files, noteList] = await Promise.all([
        studyApi.conversations(courseId),
        studyApi.guides(courseId),
        materialsApi.list(courseId),
        notesApi.list(courseId),
      ]);

      setConversationList(conversations);
      setGuides(guideList);
      setSources([
        ...files.map((f) => ({ key: `m${f.id}`, id: f.id, kind: "material", name: f.fileName })),
        ...noteList.map((n) => ({ key: `n${n.id}`, id: n.id, kind: "note", name: n.title })),
      ]);

      // Pick up the most recent conversation rather than starting a blank one,
      // which is the whole point of saving them.
      setConversation(conversations.length > 0 ? await studyApi.conversation(conversations[0].id) : null);
      setError("");
    } catch (err) {
      setError(err.message);
    }
  }, [courseId]);

  useEffect(() => {
    loadCourse();
  }, [loadCourse]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ behavior: "smooth" });
  }, [conversation?.messages?.length, thinking]);

  // --- actions -------------------------------------------------------------

  async function ask(text) {
    const trimmed = (text ?? question).trim();
    if (!trimmed || thinking) return;

    setThinking(true);
    setError("");
    setQuestion("");

    try {
      const active = await ensureConversation();

      // Shown immediately with a temporary id; the server's copy arrives with
      // the reply. Waiting would leave the input empty and nothing on screen.
      const pending = {
        id: `pending-${Date.now()}`,
        role: 0,
        content: trimmed,
        artifactKind: 0,
      };
      setConversation((c) => ({ ...c, messages: [...(c?.messages ?? []), pending] }));

      const reply = await studyApi.ask(active.id, trimmed);

      setConversation((c) => ({ ...c, messages: [...c.messages, reply] }));
      setAi((a) => (a ? { ...a, remaining: a.remaining - 1 } : a));
    } catch (err) {
      setError(err.message);
      // Drop the optimistic question - it was never saved.
      setConversation((c) =>
        c ? { ...c, messages: c.messages.filter((m) => !String(m.id).startsWith("pending-")) } : c
      );
    } finally {
      setThinking(false);
    }
  }

  async function saveGuide(message) {
    try {
      const guide = await studyApi.saveArtifact(message.id);

      setGuides((current) => [guide, ...current]);
      setConversation((c) => ({
        ...c,
        messages: c.messages.map((m) =>
          m.id === message.id ? { ...m, savedStudyGuideId: guide.id } : m
        ),
      }));
    } catch (err) {
      setError(err.message);
    }
  }

  async function newConversation() {
    try {
      const created = await studyApi.start(courseId);
      setConversation(created);
      setConversationList((list) => [created, ...list]);
    } catch (err) {
      setError(err.message);
    }
  }

  async function openConversation(id) {
    try {
      setConversation(await studyApi.conversation(id));
    } catch (err) {
      setError(err.message);
    }
  }

  /**
   * Makes sure there is a conversation to attach things to.
   *
   * Choosing your sources before asking anything is the natural order - you
   * decide what you are working from, then you ask about it - so the
   * conversation is created the moment it is needed rather than only on the
   * first question.
   */
  async function ensureConversation() {
    if (conversation) return conversation;

    const created = await studyApi.start(courseId);
    setConversation(created);
    setConversationList((list) => [created, ...list]);

    return created;
  }

  async function toggleSource(item) {
    try {
      const active = await ensureConversation();

      const materialIds = new Set(active.sourceMaterialIds ?? []);
      const noteIds = new Set(active.sourceNoteIds ?? []);
      const set = item.kind === "material" ? materialIds : noteIds;

      if (set.has(item.id)) set.delete(item.id);
      else set.add(item.id);

      const updated = await studyApi.setSources(active.id, [...materialIds], [...noteIds]);
      setConversation((c) => ({ ...updated, messages: c?.messages ?? [] }));
    } catch (err) {
      setError(err.message);
    }
  }

  async function removeGuide(guide) {
    if (!confirm(`Delete "${guide.title}"?`)) return;

    try {
      await studyApi.removeGuide(guide.id);
      setGuides((current) => current.filter((g) => g.id !== guide.id));
      if (openGuide?.id === guide.id) setOpenGuide(null);
    } catch (err) {
      setError(err.message);
    }
  }

  async function showGuide(guide) {
    try {
      setOpenGuide(await studyApi.guide(guide.id));
    } catch (err) {
      setError(err.message);
    }
  }

  // --- render --------------------------------------------------------------

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (courseList.length === 0) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-16 text-center">
        <h1 className="text-2xl font-semibold">Study</h1>
        <p className="mt-2 text-slate-600">
          Add a course first — the tutor works from the material inside one.
        </p>
        <Link
          href="/courses"
          className="mt-4 inline-block rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Go to Courses
        </Link>
      </div>
    );
  }

  const selectedIds = new Set([
    ...(conversation?.sourceMaterialIds ?? []).map((id) => `m${id}`),
    ...(conversation?.sourceNoteIds ?? []).map((id) => `n${id}`),
  ]);

  const messages = conversation?.messages ?? [];

  return (
    <div className="mx-auto max-w-6xl px-6 py-10">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Study</h1>
          <p className="mt-1 text-sm text-slate-600">
            Ask about your own course material, and keep what is worth keeping.
            {ai?.configured && (
              <span className="text-slate-400"> · {ai.remaining} AI requests left today</span>
            )}
          </p>
        </div>

        <select
          value={courseId ?? ""}
          onChange={(e) => setCourseId(Number(e.target.value))}
          className="rounded-md border border-slate-300 px-3 py-2 text-sm"
        >
          {courseList.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_18rem]">
        {/* Chat */}
        <div className="flex min-h-[32rem] flex-col rounded-xl border border-slate-200 bg-white">
          {/* Sources */}
          <div className="border-b border-slate-200 px-4 py-2.5">
            <button
              onClick={() => setShowSources((s) => !s)}
              className="text-sm font-medium text-slate-600 hover:text-slate-900"
            >
              {selectedIds.size === 0
                ? "No material attached — click to choose"
                : `Working from ${selectedIds.size} ${selectedIds.size === 1 ? "source" : "sources"}`}
              <span className="ml-1 text-slate-400">{showSources ? "▾" : "▸"}</span>
            </button>

            {showSources && (
              <div className="mt-2 max-h-40 space-y-1 overflow-y-auto">
                {sources.length === 0 ? (
                  <p className="text-sm text-slate-500">
                    Nothing uploaded to this course yet.{" "}
                    <Link
                      href={`/courses/${courseId}/materials`}
                      className="font-medium text-indigo-600 hover:underline"
                    >
                      Add materials
                    </Link>
                  </p>
                ) : (
                  sources.map((item) => (
                    <label
                      key={item.key}
                      className="flex cursor-pointer items-center gap-2 rounded px-1 py-0.5 text-sm text-slate-700 hover:bg-slate-50"
                    >
                      <input
                        type="checkbox"
                        checked={selectedIds.has(item.key)}
                        onChange={() => toggleSource(item)}
                        className="h-4 w-4 rounded border-slate-300 text-indigo-600"
                      />
                      <span className="truncate">
                        {item.kind === "note" ? "Note: " : ""}
                        {item.name}
                      </span>
                    </label>
                  ))
                )}
              </div>
            )}
          </div>

          {/* Messages */}
          <div className="flex-1 space-y-4 overflow-y-auto px-4 py-4">
            {messages.length === 0 && !thinking && (
              <div className="py-10 text-center">
                <p className="text-slate-600">Ask anything about this course.</p>
                <div className="mt-4 flex flex-wrap justify-center gap-2">
                  {SUGGESTIONS.map((s) => (
                    <button
                      key={s}
                      onClick={() => ask(s)}
                      className="rounded-full border border-slate-300 px-3 py-1.5 text-sm text-slate-600 transition hover:border-indigo-400 hover:text-indigo-700"
                    >
                      {s}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {messages.map((message) => (
              <Message key={message.id} message={message} onSave={() => saveGuide(message)} />
            ))}

            {thinking && (
              <div className="flex items-center gap-2 text-sm text-slate-500">
                <span className="h-2 w-2 animate-pulse rounded-full bg-indigo-500" />
                Thinking...
              </div>
            )}

            <div ref={bottom} />
          </div>

          {/* Composer */}
          <form
            onSubmit={(e) => {
              e.preventDefault();
              ask();
            }}
            className="border-t border-slate-200 p-3"
          >
            <div className="flex gap-2">
              <input
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
                placeholder="Ask a question, or ask for a study guide..."
                disabled={thinking}
                className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 disabled:bg-slate-50"
              />
              <button
                type="submit"
                disabled={thinking || !question.trim()}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                Send
              </button>
            </div>
          </form>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          <div>
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-medium text-slate-900">Conversations</h2>
              <button
                onClick={newConversation}
                className="text-sm font-medium text-indigo-600 hover:underline"
              >
                New
              </button>
            </div>

            <ul className="mt-2 space-y-1">
              {conversationList.length === 0 && (
                <li className="text-sm text-slate-500">None yet.</li>
              )}
              {conversationList.map((c) => (
                <li key={c.id}>
                  <button
                    onClick={() => openConversation(c.id)}
                    className={`w-full truncate rounded-md px-2 py-1.5 text-left text-sm transition ${
                      conversation?.id === c.id
                        ? "bg-indigo-50 text-indigo-700"
                        : "text-slate-600 hover:bg-slate-100"
                    }`}
                  >
                    {c.title}
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h2 className="text-sm font-medium text-slate-900">Saved study guides</h2>

            <ul className="mt-2 space-y-1">
              {guides.length === 0 && (
                <li className="text-sm text-slate-500">
                  Ask for a study guide and save it here.
                </li>
              )}
              {guides.map((g) => (
                <li key={g.id} className="flex items-center gap-1">
                  <button
                    onClick={() => showGuide(g)}
                    className="min-w-0 flex-1 truncate rounded-md px-2 py-1.5 text-left text-sm text-slate-600 transition hover:bg-slate-100"
                  >
                    {g.title}
                  </button>
                  <button
                    onClick={() => removeGuide(g)}
                    className="px-1 text-xs text-slate-400 transition hover:text-red-600"
                    aria-label={`Delete ${g.title}`}
                  >
                    &#10005;
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div>
            <h2 className="text-sm font-medium text-slate-900">Flashcards</h2>
            <Link
              href={`/courses/${courseId}/flashcards`}
              className="mt-2 block rounded-md px-2 py-1.5 text-sm text-indigo-600 hover:bg-slate-100"
            >
              Decks for this course &rarr;
            </Link>
          </div>
        </div>
      </div>

      {openGuide && (
        <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/40 p-4 sm:items-center">
          <div className="absolute inset-0" onClick={() => setOpenGuide(null)} aria-hidden="true" />
          <div className="relative max-h-[85vh] w-full max-w-2xl overflow-y-auto rounded-xl border border-slate-200 bg-white shadow-xl">
            <div className="sticky top-0 flex items-center justify-between border-b border-slate-200 bg-white px-5 py-3">
              <h2 className="font-medium text-slate-900">{openGuide.title}</h2>
              <button
                onClick={() => setOpenGuide(null)}
                className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                aria-label="Close"
              >
                &#10005;
              </button>
            </div>
            <div className="px-5 py-4">
              <Markdown text={openGuide.content} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function Message({ message, onSave }) {
  const isUser = message.role === 0;

  if (isUser) {
    return (
      <div className="flex justify-end">
        <div className="max-w-[85%] rounded-2xl rounded-br-sm bg-indigo-600 px-4 py-2 text-sm text-white">
          {message.content}
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-[92%] space-y-3">
      <div className="rounded-2xl rounded-bl-sm bg-slate-100 px-4 py-3 text-sm text-slate-800">
        <Markdown text={message.content} />
      </div>

      {message.artifactKind === 1 && message.artifactContent && (
        <div className="rounded-xl border border-slate-200 bg-white">
          <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-2.5">
            <div className="min-w-0">
              <p className="text-[11px] font-medium uppercase tracking-wide text-slate-400">
                Study guide
              </p>
              <p className="truncate text-sm font-medium text-slate-900">
                {message.artifactTitle}
              </p>
            </div>

            {message.savedStudyGuideId ? (
              <span className="shrink-0 rounded-md bg-green-50 px-3 py-1.5 text-sm font-medium text-green-700">
                Saved
              </span>
            ) : (
              <button
                onClick={onSave}
                className="shrink-0 rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
              >
                Save
              </button>
            )}
          </div>

          <div className="max-h-80 overflow-y-auto px-4 py-3">
            <Markdown text={message.artifactContent} />
          </div>
        </div>
      )}
    </div>
  );
}

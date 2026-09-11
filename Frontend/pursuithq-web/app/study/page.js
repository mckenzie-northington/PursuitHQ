'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  courses as coursesApi,
  flashcards as flashcardsApi,
  materials as materialsApi,
  notes as notesApi,
  quizzes as quizzesApi,
  study as studyApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Markdown from "@/components/Markdown";
import TestPreview from "@/components/TestPreview";
import { downloadText, safeFileName } from "@/lib/download";

const SUGGESTIONS = [
  "Make me a study guide for the midterm",
  "Make a free response practice test",
  "Explain the hardest concept in these slides",
  "What should I focus on first?",
];

// Matches StudyArtifactKind in the API.
const GUIDE = 1;
const TEST = 2;

/** How many of each thing the sidebar shows before "View all". */
const SIDEBAR_LIMIT = 3;

export default function StudyPage() {
  const { user, loading } = useAuth();

  const [courseList, setCourseList] = useState([]);
  const [courseId, setCourseId] = useState(null);

  const [conversation, setConversation] = useState(null);
  const [conversationList, setConversationList] = useState([]);
  const [guides, setGuides] = useState([]);
  const [tests, setTests] = useState([]);
  const [sources, setSources] = useState([]);
  const [ai, setAi] = useState(null);

  const [question, setQuestion] = useState("");
  const [thinking, setThinking] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [showSources, setShowSources] = useState(true);
  const [openGuide, setOpenGuide] = useState(null);
  const [renamingId, setRenamingId] = useState(null);
  const [renameText, setRenameText] = useState("");

  const scroller = useRef(null);

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
      const [conversations, guideList, testList, files, noteList] = await Promise.all([
        studyApi.conversations(courseId),
        studyApi.guides(courseId),
        quizzesApi.list(courseId),
        materialsApi.list(courseId),
        notesApi.list(courseId),
      ]);

      setConversationList(conversations);
      setGuides(guideList);
      setTests(testList);
      setSources([
        ...files.map((f) => ({ key: `m${f.id}`, id: f.id, kind: "material", name: f.fileName })),
        ...noteList.map((n) => ({ key: `n${n.id}`, id: n.id, kind: "note", name: n.title })),
      ]);

      // Pick up where you left off rather than starting blank, which is the
      // whole point of saving conversations.
      setConversation(
        conversations.length > 0 ? await studyApi.conversation(conversations[0].id) : null
      );
      setError("");
    } catch (err) {
      setError(err.message);
    }
  }, [courseId]);

  useEffect(() => {
    loadCourse();
  }, [loadCourse]);

  // Scroll the message list, not the window - the page itself never grows.
  useEffect(() => {
    const el = scroller.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [conversation?.messages?.length, thinking]);

  // --- conversation ---------------------------------------------------------

  async function ensureConversation() {
    if (conversation) return conversation;

    const created = await studyApi.start(courseId);
    setConversation(created);
    setConversationList((list) => [created, ...list]);

    return created;
  }

  async function ask(text) {
    const trimmed = (text ?? question).trim();
    if (!trimmed || thinking) return;

    setThinking(true);
    setError("");
    setQuestion("");

    try {
      const active = await ensureConversation();

      // Shown immediately with a temporary id; the saved copy arrives with the
      // reply. Waiting would leave the box empty and nothing on screen.
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

      // The server may have titled the session from this first question.
      setConversationList(await studyApi.conversations(courseId));
    } catch (err) {
      setError(err.message);
      setConversation((c) =>
        c ? { ...c, messages: c.messages.filter((m) => !String(m.id).startsWith("pending-")) } : c
      );
    } finally {
      setThinking(false);
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

  function startRename(session) {
    setRenamingId(session.id);
    setRenameText(session.title);
  }

  async function saveRename(e) {
    e.preventDefault();
    const title = renameText.trim();
    if (!title) return;

    try {
      const updated = await studyApi.rename(renamingId, title);

      setConversationList((list) =>
        list.map((c) => (c.id === updated.id ? { ...c, title: updated.title } : c))
      );
      setConversation((c) => (c && c.id === updated.id ? { ...c, title: updated.title } : c));
      setRenamingId(null);
    } catch (err) {
      setError(err.message);
    }
  }

  async function removeConversation(session) {
    if (!confirm(`Delete "${session.title}" and everything in it?`)) return;

    try {
      await studyApi.removeConversation(session.id);

      setConversationList((list) => list.filter((c) => c.id !== session.id));
      if (conversation?.id === session.id) setConversation(null);
    } catch (err) {
      setError(err.message);
    }
  }

  // --- artifacts ------------------------------------------------------------

  async function saveArtifact(message) {
    try {
      const saved = await studyApi.saveArtifact(message.id);

      if (message.artifactKind === TEST) {
        setTests((current) => [saved, ...current]);
        setConversation((c) => ({
          ...c,
          messages: c.messages.map((m) =>
            m.id === message.id ? { ...m, savedQuizId: saved.id } : m
          ),
        }));
      } else {
        setGuides((current) => [saved, ...current]);
        setConversation((c) => ({
          ...c,
          messages: c.messages.map((m) =>
            m.id === message.id ? { ...m, savedStudyGuideId: saved.id } : m
          ),
        }));
      }
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

  // --- render ---------------------------------------------------------------

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

      <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_17rem]">
        {/*
          Fixed height, not min-height. The message list scrolls inside this
          box, so the page stays put however long the conversation gets -
          the composer never walks off the bottom of the screen.
        */}
        <div className="flex h-[calc(100vh-15rem)] min-h-[30rem] flex-col overflow-hidden rounded-xl border border-slate-200 bg-white">
          <div className="shrink-0 border-b border-slate-200 px-4 py-2.5">
            <div className="flex items-center justify-between gap-3">
              <button
                onClick={() => setShowSources((s) => !s)}
                className="min-w-0 truncate text-left text-sm font-medium text-slate-600 hover:text-slate-900"
              >
                {selectedIds.size === 0
                  ? "No material attached — click to choose"
                  : `Working from ${selectedIds.size} ${selectedIds.size === 1 ? "source" : "sources"}`}
                <span className="ml-1 text-slate-400">{showSources ? "▾" : "▸"}</span>
              </button>

              {conversation && (
                <span className="shrink-0 truncate text-xs text-slate-400">
                  {conversation.title}
                </span>
              )}
            </div>

            {showSources && (
              <div className="mt-2 max-h-32 space-y-1 overflow-y-auto">
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

          <div ref={scroller} className="flex-1 space-y-4 overflow-y-auto px-4 py-4">
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
              <Message key={message.id} message={message} onSave={() => saveArtifact(message)} />
            ))}

            {thinking && (
              <div className="flex items-center gap-2 text-sm text-slate-500">
                <span className="h-2 w-2 animate-pulse rounded-full bg-indigo-500" />
                Thinking...
              </div>
            )}
          </div>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              ask();
            }}
            className="shrink-0 border-t border-slate-200 p-3"
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

        {/* Sidebar: the few most recent of each, with a way to see the rest. */}
        <div className="space-y-6">
          <Section
            title="Sessions"
            action={{ label: "New", onClick: newConversation }}
            viewAll={`/courses/${courseId}/sessions`}
            total={conversationList.length}
            empty="None yet."
          >
            {conversationList.slice(0, SIDEBAR_LIMIT).map((c) =>
              renamingId === c.id ? (
                <li key={c.id}>
                  <form onSubmit={saveRename}>
                    <input
                      autoFocus
                      value={renameText}
                      onChange={(e) => setRenameText(e.target.value)}
                      onBlur={() => setRenamingId(null)}
                      className="w-full rounded-md border border-indigo-400 px-2 py-1 text-sm outline-none"
                    />
                  </form>
                </li>
              ) : (
                <li key={c.id} className="group flex items-center gap-0.5">
                  <button
                    onClick={() => openConversation(c.id)}
                    className={`min-w-0 flex-1 truncate rounded-md px-2 py-1.5 text-left text-sm transition ${
                      conversation?.id === c.id
                        ? "bg-indigo-50 text-indigo-700"
                        : "text-slate-600 hover:bg-slate-100"
                    }`}
                  >
                    {c.title}
                  </button>
                  <button
                    onClick={() => startRename(c)}
                    title="Rename"
                    className="hidden px-1 text-xs text-slate-400 hover:text-slate-700 group-hover:block"
                  >
                    ✎
                  </button>
                  <button
                    onClick={() => removeConversation(c)}
                    title="Delete"
                    className="hidden px-1 text-xs text-slate-400 hover:text-red-600 group-hover:block"
                  >
                    ✕
                  </button>
                </li>
              )
            )}
          </Section>

          <Section
            title="Study guides"
            viewAll={`/courses/${courseId}/guides`}
            total={guides.length}
            empty="Ask for a study guide and save it here."
          >
            {guides.slice(0, SIDEBAR_LIMIT).map((g) => (
              <li key={g.id}>
                <button
                  onClick={() => showGuide(g)}
                  className="w-full truncate rounded-md px-2 py-1.5 text-left text-sm text-slate-600 transition hover:bg-slate-100"
                >
                  {g.title}
                </button>
              </li>
            ))}
          </Section>

          <Section
            title="Practice tests"
            viewAll={`/courses/${courseId}/tests`}
            total={tests.length}
            empty="Ask for a practice test and save it here."
          >
            {tests.slice(0, SIDEBAR_LIMIT).map((t) => (
              <li key={t.id}>
                <Link
                  href={`/tests/${t.id}`}
                  className="block truncate rounded-md px-2 py-1.5 text-sm text-slate-600 transition hover:bg-slate-100"
                >
                  {t.title}
                  {t.bestScore != null && (
                    <span className="ml-1 text-slate-400">· best {t.bestScore}%</span>
                  )}
                </Link>
              </li>
            ))}
          </Section>

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
        <GuideReader guide={openGuide} onClose={() => setOpenGuide(null)} />
      )}
    </div>
  );
}

/** A sidebar block: a few recent items, and a link to the rest. */
function Section({ title, action, viewAll, total, empty, children }) {
  const shown = Array.isArray(children) ? children.length : 0;

  return (
    <div>
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-medium text-slate-900">{title}</h2>
        {action ? (
          <button
            onClick={action.onClick}
            className="text-sm font-medium text-indigo-600 hover:underline"
          >
            {action.label}
          </button>
        ) : (
          total > 0 && (
            <Link href={viewAll} className="text-sm font-medium text-indigo-600 hover:underline">
              View all
            </Link>
          )
        )}
      </div>

      <ul className="mt-2 space-y-1">
        {total === 0 ? <li className="text-sm text-slate-500">{empty}</li> : children}
      </ul>

      {total > shown && (
        <Link
          href={viewAll}
          className="mt-1 block px-2 text-xs font-medium text-slate-500 hover:text-indigo-600"
        >
          View all {total} &rarr;
        </Link>
      )}
    </div>
  );
}

function GuideReader({ guide, onClose }) {
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-900/40 p-4 sm:items-center">
      <div className="absolute inset-0" onClick={onClose} aria-hidden="true" />
      <div className="relative max-h-[85vh] w-full max-w-2xl overflow-y-auto rounded-xl border border-slate-200 bg-white shadow-xl">
        <div className="sticky top-0 flex items-center justify-between gap-3 border-b border-slate-200 bg-white px-5 py-3">
          <h2 className="min-w-0 truncate font-medium text-slate-900">{guide.title}</h2>
          <div className="flex shrink-0 items-center gap-2">
            <button
              onClick={() => downloadText(guide.content, safeFileName(guide.title, "md"))}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              Download
            </button>
            <button
              onClick={onClose}
              className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
              aria-label="Close"
            >
              ✕
            </button>
          </div>
        </div>
        <div className="px-5 py-4">
          <Markdown text={guide.content} />
        </div>
      </div>
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

      {message.artifactKind === TEST && message.artifactContent && (
        <TestPreview
          content={message.artifactContent}
          title={message.artifactTitle}
          savedQuizId={message.savedQuizId}
          onSave={onSave}
        />
      )}

      {message.artifactKind === GUIDE && message.artifactContent && (
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

            <div className="flex shrink-0 items-center gap-2">
              <button
                onClick={() =>
                  downloadText(
                    message.artifactContent,
                    safeFileName(message.artifactTitle, "md")
                  )
                }
                className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
              >
                Download
              </button>

              {message.savedStudyGuideId ? (
                <span className="rounded-md bg-green-50 px-3 py-1.5 text-sm font-medium text-green-700">
                  Saved
                </span>
              ) : (
                <button
                  onClick={onSave}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
                >
                  Save
                </button>
              )}
            </div>
          </div>

          <div className="max-h-80 overflow-y-auto px-4 py-3">
            <Markdown text={message.artifactContent} />
          </div>
        </div>
      )}
    </div>
  );
}

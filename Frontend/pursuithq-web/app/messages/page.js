'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  conversations as conversationsApi,
  connections as connectionsApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Avatar from "@/components/Avatar";
import { filterStudents } from "@/lib/studentSearch";

/**
 * While a conversation is open it is re-fetched on this interval.
 *
 * Polling, not push. SignalR is the next step and replaces exactly this timer;
 * everything else on the page stays as it is.
 */
const POLL_MS = 5_000;

export default function MessagesPage() {
  const { user, loading } = useAuth();

  const [list, setList] = useState([]);
  const [ready, setReady] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [error, setError] = useState("");
  const [creating, setCreating] = useState(false);

  const loadList = useCallback(async () => {
    try {
      const found = await conversationsApi.list();
      setList(found);
      return found;
    } catch (err) {
      setError(err.message);
      return [];
    } finally {
      setReady(true);
    }
  }, []);

  useEffect(() => {
    if (loading || !user) return;

    loadList().then((found) => {
      // Arriving from a profile's "Message" button, which passes ?c=<id>.
      // Read from the URL directly rather than useSearchParams, which would
      // drag a Suspense boundary into the page for one query parameter.
      const wanted = Number(new URLSearchParams(window.location.search).get("c"));

      if (wanted && found.some((c) => c.id === wanted)) setOpenId(wanted);
      else if (found.length > 0) setOpenId((current) => current ?? found[0].id);
    });
  }, [loading, user, loadList]);

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-6xl px-6 py-10">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Messages</h1>
        <button
          onClick={() => setCreating(true)}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
        >
          New group
        </button>
      </div>

      {error && (
        <p className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      {list.length === 0 ? (
        <div className="mt-6 rounded-xl border border-dashed border-slate-300 px-6 py-12 text-center">
          <p className="text-slate-600">No conversations yet.</p>
          <p className="mt-1 text-sm text-slate-500">
            Connect with someone under{" "}
            <Link href="/students" className="font-medium text-indigo-600 hover:underline">
              Students
            </Link>
            , then message them from their profile.
          </p>
        </div>
      ) : (
        <div className="mt-6 grid gap-4 md:grid-cols-[18rem_1fr]">
          <ul className="space-y-1 md:max-h-[34rem] md:overflow-y-auto">
            {list.map((conversation) => (
              <li key={conversation.id}>
                <button
                  onClick={() => setOpenId(conversation.id)}
                  className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition ${
                    openId === conversation.id ? "bg-indigo-50" : "hover:bg-slate-100"
                  }`}
                >
                  <Avatar
                    student={
                      conversation.isGroup
                        ? { id: `g${conversation.id}`, firstName: conversation.title }
                        : conversation.members[0]
                    }
                    size={40}
                  />

                  <span className="min-w-0 flex-1">
                    <span className="flex items-center justify-between gap-2">
                      <span className="truncate text-sm font-medium text-slate-900">
                        {conversation.title}
                      </span>
                      {conversation.unreadCount > 0 && (
                        <span className="shrink-0 rounded-full bg-red-500 px-1.5 py-0.5 text-[11px] font-semibold text-white">
                          {conversation.unreadCount}
                        </span>
                      )}
                    </span>
                    <span className="block truncate text-xs text-slate-500">
                      {conversation.lastMessage
                        ? `${
                            conversation.isGroup && conversation.lastMessageSender
                              ? `${conversation.lastMessageSender}: `
                              : ""
                          }${conversation.lastMessage}`
                        : "No messages yet"}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>

          {openId ? (
            <Thread
              key={openId}
              conversation={list.find((c) => c.id === openId)}
              onChanged={loadList}
              onClosed={() => {
                setOpenId(null);
                loadList();
              }}
            />
          ) : (
            <div className="rounded-xl border border-slate-200 bg-white p-6 text-sm text-slate-500">
              Pick a conversation.
            </div>
          )}
        </div>
      )}

      {creating && (
        <NewGroup
          onClose={() => setCreating(false)}
          onCreated={async (conversation) => {
            setCreating(false);
            await loadList();
            setOpenId(conversation.id);
          }}
        />
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ thread */

function Thread({ conversation, onChanged, onClosed }) {
  const [messages, setMessages] = useState([]);
  const [ready, setReady] = useState(false);
  const [body, setBody] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [showMembers, setShowMembers] = useState(false);

  const bottom = useRef(null);
  const id = conversation?.id;

  const load = useCallback(
    async (markRead) => {
      if (!id) return;

      try {
        const found = await conversationsApi.messages(id);

        // Only re-render when something actually changed, so the poll does not
        // fight with the text box or reset the scroll position every 5 seconds.
        setMessages((current) => {
          const same =
            current.length === found.length &&
            current[current.length - 1]?.id === found[found.length - 1]?.id;

          return same ? current : found;
        });

        if (markRead) {
          await conversationsApi.markRead(id);
          onChanged?.();
        }
      } catch (err) {
        setError(err.message);
      } finally {
        setReady(true);
      }
    },
    [id, onChanged]
  );

  useEffect(() => {
    load(true);

    const timer = setInterval(() => {
      if (document.visibilityState === "visible") load(true);
    }, POLL_MS);

    return () => clearInterval(timer);
  }, [load]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "end" });
  }, [messages]);

  async function send(e) {
    e.preventDefault();

    const text = body.trim();
    if (!text) return;

    setSending(true);
    setError("");

    try {
      const message = await conversationsApi.send(id, text);
      setMessages((current) => [...current, message]);
      setBody("");
      onChanged?.();
    } catch (err) {
      setError(err.message);
    } finally {
      setSending(false);
    }
  }

  async function leave() {
    if (!confirm(`Leave “${conversation.title}”?`)) return;

    try {
      await conversationsApi.leave(id);
      onClosed?.();
    } catch (err) {
      setError(err.message);
    }
  }

  if (!conversation) return null;

  return (
    <div className="flex h-[34rem] flex-col rounded-xl border border-slate-200 bg-white">
      <div className="flex items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
        <div className="min-w-0">
          <p className="truncate font-medium text-slate-900">{conversation.title}</p>
          <p className="truncate text-xs text-slate-500">
            {conversation.isGroup
              ? `${conversation.members.length + 1} people`
              : conversation.members[0]?.school || "Direct message"}
          </p>
        </div>

        {conversation.isGroup && (
          <button
            onClick={() => setShowMembers((v) => !v)}
            className="shrink-0 text-sm font-medium text-slate-600 hover:text-slate-900"
          >
            {showMembers ? "Hide" : "People"}
          </button>
        )}
      </div>

      {showMembers && (
        <div className="border-b border-slate-200 bg-slate-50 px-4 py-3">
          <ul className="space-y-1">
            {conversation.members.map((member) => (
              <li key={member.id} className="flex items-center gap-2 text-sm">
                <Avatar student={member} size={24} />
                <span className="text-slate-700">
                  {member.firstName} {member.lastName}
                </span>
              </li>
            ))}
          </ul>

          <button
            onClick={leave}
            className="mt-3 text-sm font-medium text-red-600 hover:underline"
          >
            Leave group
          </button>
        </div>
      )}

      <div className="flex-1 space-y-2 overflow-y-auto px-4 py-4">
        {!ready ? (
          <p className="text-sm text-slate-500">Loading...</p>
        ) : messages.length === 0 ? (
          <p className="text-sm text-slate-500">No messages yet. Say something.</p>
        ) : (
          messages.map((message, i) => {
            // Only label a sender when it changes, so a run of messages from one
            // person reads as one block rather than a stack of name tags.
            const newSender = messages[i - 1]?.senderId !== message.senderId;

            return (
              <div
                key={message.id}
                className={`flex ${message.isMine ? "justify-end" : "justify-start"}`}
              >
                <div className="max-w-[80%]">
                  {conversation.isGroup && !message.isMine && newSender && (
                    <p className="mb-0.5 text-xs font-medium text-slate-500">
                      {message.senderName}
                    </p>
                  )}

                  <div
                    className={`rounded-2xl px-3 py-2 text-sm ${
                      message.isDeleted
                        ? "bg-slate-100 italic text-slate-400"
                        : message.isMine
                        ? "bg-indigo-600 text-white"
                        : "bg-slate-100 text-slate-900"
                    }`}
                  >
                    {message.isDeleted ? "Message deleted" : message.body}
                  </div>

                  <p
                    className={`mt-0.5 text-[11px] text-slate-400 ${
                      message.isMine ? "text-right" : ""
                    }`}
                  >
                    {new Date(message.sentAt).toLocaleTimeString([], {
                      hour: "numeric",
                      minute: "2-digit",
                    })}
                  </p>
                </div>
              </div>
            );
          })
        )}

        <div ref={bottom} />
      </div>

      {error && (
        <p className="border-t border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <form onSubmit={send} className="flex gap-2 border-t border-slate-200 p-3">
        <input
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder="Message"
          maxLength={4000}
          className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />
        <button
          type="submit"
          disabled={sending || !body.trim()}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
        >
          Send
        </button>
      </form>
    </div>
  );
}

/* --------------------------------------------------------------- new group */

function NewGroup({ onClose, onCreated }) {
  const [name, setName] = useState("");
  const [people, setPeople] = useState([]);
  const [chosen, setChosen] = useState([]);
  const [filter, setFilter] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    connectionsApi.list().then(setPeople).catch((err) => setError(err.message));
  }, []);

  function toggle(id) {
    setChosen((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id]
    );
  }

  async function create(e) {
    e.preventDefault();
    setBusy(true);
    setError("");

    try {
      onCreated(await conversationsApi.createGroup(name.trim(), chosen));
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-6">
      <button
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full bg-slate-900/20"
      />

      <form
        onSubmit={create}
        className="relative w-full max-w-md rounded-xl border border-slate-200 bg-white p-6 shadow-xl"
      >
        <h2 className="font-medium text-slate-900">New group</h2>

        {error && (
          <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}

        <label className="mt-4 block text-sm font-medium text-slate-700">Name</label>
        <input
          required
          autoFocus
          maxLength={100}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="CS 201 study group"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />

        <p className="mt-4 text-sm font-medium text-slate-700">
          Who to add{chosen.length > 0 && ` — ${chosen.length} selected`}
        </p>
        <p className="mt-0.5 text-xs text-slate-500">
          Only people you are connected with can be added.
        </p>

        {people.length > 5 && (
          <input
            type="search"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            placeholder="Search connections"
            className="mt-2 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />
        )}

        {people.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">
            You have no connections yet, so there is nobody to add.
          </p>
        ) : (
          <ul className="mt-2 max-h-56 space-y-1 overflow-y-auto">
            {/*
              Anyone already ticked stays listed even when the filter would
              hide them. Otherwise typing a name looks like it silently
              un-selected everyone chosen before it.
            */}
            {people
              .filter(
                (person) =>
                  chosen.includes(person.id) ||
                  filterStudents([person], filter).length > 0
              )
              .map((person) => (
                <li key={person.id}>
                  <label className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-slate-50">
                    <input
                      type="checkbox"
                      checked={chosen.includes(person.id)}
                      onChange={() => toggle(person.id)}
                    />
                    <Avatar student={person} size={24} />
                    <span className="text-slate-700">
                      {person.firstName} {person.lastName}
                    </span>
                  </label>
                </li>
              ))}
          </ul>
        )}

        <div className="mt-5 flex gap-2">
          <button
            type="submit"
            disabled={busy || chosen.length === 0 || !name.trim()}
            className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
          >
            {busy ? "Creating..." : "Create"}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}

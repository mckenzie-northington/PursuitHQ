'use client';

import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import Link from "next/link";
import {
  conversations as conversationsApi,
  connections as connectionsApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Avatar, { forgetPhoto } from "@/components/Avatar";
import { filterStudents } from "@/lib/studentSearch";
import EmojiPicker, { QUICK_REACTIONS } from "@/components/EmojiPicker";
import ContextMenu from "@/components/ContextMenu";
import GroupSettings, { ROLE } from "@/components/GroupSettings";

/**
 * While a conversation is open it is re-fetched on this interval.
 *
 * Polling, not push. SignalR replaces exactly this timer; everything else on
 * the page stays as it is.
 */
const POLL_MS = 5_000;

/**
 * How often an open thread asks who is typing and who has caught up.
 *
 * Faster than the message poll, because these are the parts that have to feel
 * immediate - a "typing" bubble that arrives five seconds late is worse than
 * none. The response is two short lists whatever the history looks like.
 */
const PRESENCE_POLL_MS = 2_500;

/**
 * The most often the composer will tell the server you are typing.
 *
 * Shorter than the server's six-second window, so a steady typist never
 * flickers off, and long enough that a fast one is not sending a request per
 * keystroke.
 */
const TYPING_PING_MS = 2_500;

/**
 * How often the conversation list refreshes on its own.
 *
 * Slower than the open thread, because this only has to move a badge. Without
 * it the list only updated when something else happened to reload it, so an
 * unread marker on a chat you were not looking at could sit there unchanged.
 */
const LIST_POLL_MS = 15_000;

/**
 * How many files can ride along with one message.
 *
 * Matches MaxAttachmentsPerMessage on the API. The limit is enforced there; this
 * copy only exists so the picker stops you before the upload rather than after.
 */
const MAX_ATTACHMENTS = 10;

/**
 * The heading above the first message of each day.
 *
 * Today and yesterday get words rather than a date, because that is how people
 * refer to them, and a bare "16 Sep" on today's messages makes a live
 * conversation read like an archive.
 */
function dayLabel(date) {
  const day = new Date(date);
  const today = new Date();
  const yesterday = new Date();
  yesterday.setDate(today.getDate() - 1);

  const sameDay = (a, b) => a.toDateString() === b.toDateString();

  if (sameDay(day, today)) return "Today";
  if (sameDay(day, yesterday)) return "Yesterday";

  // The year only once it stops being obvious.
  return day.toLocaleDateString([], {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: day.getFullYear() === today.getFullYear() ? undefined : "numeric",
  });
}

const timeLabel = (date) =>
  new Date(date).toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });

/**
 * A cheap fingerprint of a message list, used to decide whether a poll actually
 * brought anything new.
 *
 * The old check compared only the *last* message, so reacting to, editing or
 * deleting anything above it was read as "nothing changed" and thrown away -
 * which is why reactions appeared to do nothing at all unless you happened to
 * pick the newest message.
 */
function signature(messages) {
  return messages
    .map(
      (m) =>
        `${m.id}|${m.body?.length ?? 0}|${m.isEdited ? 1 : 0}|${m.isDeleted ? 1 : 0}|` +
        `${m.attachments?.length ?? 0}|` +
        (m.reactions ?? [])
          .map((r) => `${r.emoji}${r.count}${r.mine ? "*" : ""}`)
          .join(",")
    )
    .join(";");
}

function TypingBubble({ names, isGroup }) {
  const label = !isGroup
    ? null
    : names.length === 1
    ? `${names[0]} is typing`
    : names.length === 2
    ? `${names[0]} and ${names[1]} are typing`
    : `${names.length} people are typing`;

  return (
    <div className="flex items-center gap-2 py-1">
      <span
        aria-label={label ?? "Typing"}
        className="flex items-center gap-1 rounded-2xl bg-slate-100 px-3 py-2.5"
      >
        {[0, 150, 300].map((delay) => (
          <span
            key={delay}
            style={{ animationDelay: `${delay}ms` }}
            className="h-1.5 w-1.5 animate-bounce rounded-full bg-slate-400"
          />
        ))}
      </span>

      {label && <span className="text-xs text-slate-500">{label}</span>}
    </div>
  );
}

function BellIcon({ muted, size = 18 }) {
  return (
    <svg
      width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"
      strokeLinejoin="round" aria-hidden="true"
    >
      <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
      <path d="M13.73 21a2 2 0 0 1-3.46 0" />
      {/* The slash is the whole signal: one icon, struck through or not. */}
      {muted && <line x1="3" y1="3" x2="21" y2="21" />}
    </svg>
  );
}

function DownloadIcon({ size = 16 }) {
  return (
    <svg
      width={size} height={size} viewBox="0 0 24 24" fill="none"
      stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"
      strokeLinejoin="round" aria-hidden="true"
    >
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <path d="M7 10l5 5 5-5" />
      <path d="M12 15V3" />
    </svg>
  );
}

function PinIcon({ size = 12 }) {
  return (
    <svg
      width={size} height={size} viewBox="0 0 24 24" fill="currentColor"
      aria-hidden="true"
    >
      <path d="M16 3l5 5-2 2-1-1-4 4 1 4-2 2-4-4-5 5-1-1 5-5-4-4 2-2 4 1 4-4-1-1z" />
    </svg>
  );
}

export default function MessagesPage() {
  const { user, loading } = useAuth();

  const [list, setList] = useState([]);
  const [invitations, setInvitations] = useState([]);
  const [ready, setReady] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [error, setError] = useState("");
  const [creating, setCreating] = useState(false);

  const [query, setQuery] = useState("");
  const [hits, setHits] = useState([]);
  const [searching, setSearching] = useState(false);

  const [requests, setRequests] = useState([]);
  const [rowMenu, setRowMenu] = useState(null);

  const loadList = useCallback(async () => {
    try {
      // Connection requests are caught separately: they are the least
      // important of the three, and a failure there must not cost you the
      // conversation list.
      const [found, invites, pending] = await Promise.all([
        conversationsApi.list(),
        conversationsApi.invitations(),
        connectionsApi.requests().catch(() => []),
      ]);

      setList(found);
      setInvitations(invites);
      setRequests(pending);
      return found;
    } catch (err) {
      setError(err.message);
      return [];
    } finally {
      setReady(true);
    }
  }, []);

  // Message search runs on the server; conversation names are filtered here,
  // because the list is already loaded and a round trip to match a title the
  // browser is holding would be slower and no more accurate.
  useEffect(() => {
    const trimmed = query.trim();

    if (trimmed.length < 2) {
      setHits([]);
      return;
    }

    setSearching(true);

    const id = setTimeout(() => {
      conversationsApi
        .searchMessages(trimmed)
        .then(setHits)
        .catch(() => setHits([]))
        .finally(() => setSearching(false));
    }, 300);

    return () => clearTimeout(id);
  }, [query]);

  async function act(fn) {
    try {
      await fn();
      await loadList();
    } catch (err) {
      setError(err.message);
    }
  }

  async function remove(conversation) {
    const warning = conversation.isGroup
      ? `Leave “${conversation.title}”? You will stop receiving its messages.`
      : `Remove your conversation with ${conversation.title}? It comes back if they message you again.`;

    if (!confirm(warning)) return;

    try {
      await conversationsApi.leave(conversation.id);

      const remaining = await loadList();
      setOpenId((current) =>
        current === conversation.id ? remaining[0]?.id ?? null : current
      );
    } catch (err) {
      setError(err.message);
    }
  }

  async function answerRequest(connectionId, accept) {
    try {
      if (accept) await connectionsApi.accept(connectionId);
      else await connectionsApi.decline(connectionId);

      await loadList();
    } catch (err) {
      setError(err.message);
    }
  }

  async function answerInvite(conversationId, accept) {
    try {
      if (accept) await conversationsApi.acceptInvite(conversationId);
      else await conversationsApi.declineInvite(conversationId);

      await loadList();
      if (accept) setOpenId(conversationId);
    } catch (err) {
      setError(err.message);
    }
  }

  useEffect(() => {
    if (loading || !user) return;

    loadList().then((found) => {
      // Arriving from a profile's "Message" button, which passes ?c=<id>. Read
      // from the URL directly rather than useSearchParams, which would drag a
      // Suspense boundary into the page for one query parameter.
      const wanted = Number(new URLSearchParams(window.location.search).get("c"));

      if (wanted && found.some((c) => c.id === wanted)) setOpenId(wanted);
      else if (found.length > 0) setOpenId((current) => current ?? found[0].id);
    });
  }, [loading, user, loadList]);

  // Paused while the tab is hidden: a backgrounded page polling every fifteen
  // seconds is a battery cost with nobody there to see the result.
  useEffect(() => {
    if (loading || !user) return;

    const timer = setInterval(() => {
      if (document.visibilityState === "visible") loadList();
    }, LIST_POLL_MS);

    return () => clearInterval(timer);
  }, [loading, user, loadList]);

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const open = list.find((c) => c.id === openId);
  const searchingNow = query.trim().length >= 2;

  const pendingCount = requests.length + invitations.length;

  // Hidden while searching: search takes over the whole area below the header,
  // and a requests column beside results nobody asked it to filter is clutter.
  const showRequests = pendingCount > 0 && !searchingNow;
  const matchingTitles = searchingNow ? filterByTitle(list, query) : [];

  return (
    // Fills the window rather than sitting in a fixed-height box. The
    // subtracted 4.5rem is the nav bar, rounded up: spare pixels are invisible,
    // too few bring back the page scrollbar this exists to remove.
    <div className="mx-auto flex h-[calc(100dvh-4.5rem)] max-w-6xl flex-col px-6 py-6">
      <div className="flex shrink-0 flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Messages</h1>

        <div className="flex flex-1 items-center justify-end gap-2">
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search chats and messages"
            className="w-full max-w-xs rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
          />

          <button
            onClick={() => setCreating(true)}
            className="shrink-0 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
          >
            New group
          </button>
        </div>
      </div>

      {error && (
        <div className="mt-3 flex shrink-0 items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      {/* Narrow windows have no room for a third column, so the same panel
          folds into one line that opens. Closed by default: a stack of requests
          should not push the conversation you came here for off the screen,
          which is exactly what the old full-width banners did. */}
      {showRequests && (
        <details className="mt-3 shrink-0 rounded-xl border border-indigo-200 bg-indigo-50/60 px-3 py-2 xl:hidden">
          <summary className="cursor-pointer select-none text-sm font-medium text-slate-800">
            {pendingCount} {pendingCount === 1 ? "request" : "requests"} waiting
          </summary>

          <div className="mt-2 flex h-56 flex-col">
            <RequestsPanel
              requests={requests}
              invitations={invitations}
              onAnswerRequest={answerRequest}
              onAnswerInvite={answerInvite}
            />
          </div>
        </details>
      )}

      {searchingNow ? (
        <SearchResults
          query={query}
          titles={matchingTitles}
          hits={hits}
          busy={searching}
          onOpen={(id) => {
            setOpenId(id);
            setQuery("");
          }}
        />
      ) : list.length === 0 ? (
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
        // min-h-0 on the grid and both panes: without it a flex or grid child
        // refuses to shrink below its content, so the inner scroll areas push
        // the page into scrolling instead of scrolling themselves.
        <div
          className={`mt-4 grid min-h-0 flex-1 gap-4 md:grid-cols-[15.5rem_1fr] ${
            showRequests ? "xl:grid-cols-[15.5rem_1fr_12.5rem]" : ""
          }`}
        >
          <ul className="min-h-0 space-y-1 overflow-y-auto">
            {list.map((conversation) => (
              <li key={conversation.id}>
                <button
                  onClick={() => setOpenId(conversation.id)}
                  onContextMenu={(e) => {
                    e.preventDefault();
                    setRowMenu({ x: e.clientX, y: e.clientY, conversation });
                  }}
                  className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition ${
                    openId === conversation.id ? "bg-indigo-50" : "hover:bg-slate-100"
                  }`}
                >
                  {/* The dot sits on the picture, which is the one part of a
                      row the eye lands on first. Grey when the chat is muted:
                      still something new, but not something shouting. */}
                  <span className="relative shrink-0">
                    {conversation.isGroup ? (
                      <Avatar
                        group={{
                          id: conversation.id,
                          title: conversation.title,
                          hasPhoto: conversation.hasPhoto,
                        }}
                        size={40}
                      />
                    ) : (
                      <Avatar student={conversation.members[0]} size={40} />
                    )}

                    {conversation.unreadCount > 0 && (
                      <span
                        aria-label={`${conversation.unreadCount} unread`}
                        className={`absolute -right-0.5 -top-0.5 h-3 w-3 rounded-full ring-2 ring-white ${
                          conversation.isMuted ? "bg-slate-400" : "bg-red-500"
                        }`}
                      />
                    )}
                  </span>

                  <span className="min-w-0 flex-1">
                    <span className="flex items-center justify-between gap-2">
                      <span className="flex min-w-0 items-center gap-1">
                        {conversation.isPinned && (
                          <span className="shrink-0 text-slate-400">
                            <PinIcon />
                          </span>
                        )}
                        <span className="truncate text-sm font-medium text-slate-900">
                          {conversation.title}
                        </span>
                      </span>

                      <span className="flex shrink-0 items-center gap-1.5">
                        {conversation.isMuted && (
                          <span className="text-slate-300">
                            <BellIcon muted size={13} />
                          </span>
                        )}

                        {conversation.unreadCount > 0 && !conversation.isMuted && (
                          <span className="rounded-full bg-red-500 px-1.5 py-0.5 text-[11px] font-semibold text-white">
                            {conversation.unreadCount}
                          </span>
                        )}
                      </span>
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

          {open ? (
            <Thread
              key={open.id}
              conversation={open}
              me={user}
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

          {/* A column of its own, mirroring the conversation list on the far
              side, and scrolling on its own rather than growing the page. */}
          {showRequests && (
            <aside className="hidden min-h-0 xl:flex xl:flex-col">
              <RequestsPanel
                requests={requests}
                invitations={invitations}
                onAnswerRequest={answerRequest}
                onAnswerInvite={answerInvite}
              />
            </aside>
          )}
        </div>
      )}

      {rowMenu && (
        <ContextMenu
          x={rowMenu.x}
          y={rowMenu.y}
          onClose={() => setRowMenu(null)}
          items={[
            {
              label: rowMenu.conversation.isPinned ? "Unpin" : "Pin to top",
              onClick: () =>
                act(() =>
                  conversationsApi.setPinned(
                    rowMenu.conversation.id,
                    !rowMenu.conversation.isPinned
                  )
                ),
            },
            {
              label:
                rowMenu.conversation.unreadCount > 0 ? "Mark as read" : "Mark as unread",
              onClick: () =>
                act(() =>
                  rowMenu.conversation.unreadCount > 0
                    ? conversationsApi.markRead(rowMenu.conversation.id)
                    : conversationsApi.markUnread(rowMenu.conversation.id)
                ),
            },
            {
              label: rowMenu.conversation.isMuted ? "Unmute" : "Mute",
              onClick: () =>
                act(() =>
                  conversationsApi.setMuted(
                    rowMenu.conversation.id,
                    !rowMenu.conversation.isMuted
                  )
                ),
            },
            "divider",
            {
              label: rowMenu.conversation.isGroup ? "Leave group" : "Remove from list",
              danger: true,
              onClick: () => remove(rowMenu.conversation),
            },
          ]}
        />
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

function filterByTitle(list, query) {
  const needle = query.trim().toLowerCase();

  return list.filter((c) => c.title.toLowerCase().includes(needle));
}

/* ------------------------------------------------------------------ search */

function SearchResults({ query, titles, hits, busy, onOpen }) {
  return (
    <div className="mt-4 min-h-0 flex-1 space-y-6 overflow-y-auto">
      <section>
        <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
          Chats
        </p>

        {titles.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">No chat names match “{query}”.</p>
        ) : (
          <ul className="mt-2 space-y-1">
            {titles.map((conversation) => (
              <li key={conversation.id}>
                <button
                  onClick={() => onOpen(conversation.id)}
                  className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition hover:bg-slate-100"
                >
                  {conversation.isGroup ? (
                    <Avatar
                      group={{
                        id: conversation.id,
                        title: conversation.title,
                        hasPhoto: conversation.hasPhoto,
                      }}
                      size={32}
                    />
                  ) : (
                    <Avatar student={conversation.members[0]} size={32} />
                  )}
                  <span className="truncate text-sm font-medium text-slate-900">
                    {conversation.title}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section>
        <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
          Messages
        </p>

        {busy ? (
          <p className="mt-2 text-sm text-slate-500">Searching...</p>
        ) : hits.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">No messages match “{query}”.</p>
        ) : (
          <ul className="mt-2 space-y-1">
            {hits.map((hit) => (
              <li key={hit.messageId}>
                <button
                  onClick={() => onOpen(hit.conversationId)}
                  className="w-full rounded-lg px-3 py-2 text-left transition hover:bg-slate-100"
                >
                  <p className="flex items-center justify-between gap-2 text-xs text-slate-500">
                    <span className="truncate font-medium text-slate-700">
                      {hit.conversationTitle}
                    </span>
                    <span className="shrink-0">
                      {dayLabel(hit.sentAt)} {timeLabel(hit.sentAt)}
                    </span>
                  </p>
                  <p className="mt-0.5 truncate text-sm text-slate-800">
                    <span className="text-slate-500">{hit.senderName}: </span>
                    {hit.body}
                  </p>
                </button>
              </li>
            ))}
          </ul>
        )}

        {/* Said plainly rather than left as a surprise: opening a result shows
            the conversation at its newest messages, not at the match. Jumping
            to an old message means paging back to it, which is a bigger change
            than this search was. */}
        {hits.length > 0 && (
          <p className="mt-3 text-xs text-slate-400">
            Opening a result shows the latest messages in that chat, not the matched one.
          </p>
        )}
      </section>
    </div>
  );
}

/* ---------------------------------------------------------------- requests */

/**
 * Connection requests and group invitations, in the same shape as a
 * conversation row.
 *
 * One list rather than two sections: to the person reading it these are the
 * same thing - somebody is waiting on an answer - and splitting them into
 * headed groups spends vertical space on a distinction nobody is looking for.
 */
function RequestsPanel({ requests, invitations, onAnswerRequest, onAnswerInvite }) {
  const total = requests.length + invitations.length;

  const accept =
    "flex-1 rounded-md bg-indigo-600 px-1 py-1 text-[11px] font-medium text-white transition hover:bg-indigo-700";
  const decline =
    "flex-1 rounded-md border border-slate-300 bg-white px-1 py-1 text-[11px] font-medium text-slate-600 transition hover:bg-slate-50";

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <p className="flex shrink-0 items-center gap-2 px-1 pb-2 text-xs font-medium uppercase tracking-wide text-slate-400">
        Requests
        <span className="rounded-full bg-indigo-100 px-1.5 py-0.5 text-[11px] font-semibold text-indigo-700">
          {total}
        </span>
      </p>

      <ul className="min-h-0 flex-1 space-y-2 overflow-y-auto pr-1">
        {requests.map((request) => (
          <li
            key={`connection-${request.id}`}
            className="rounded-lg border border-slate-200 bg-white p-1.5"
          >
            <div className="flex items-center gap-1.5">
              <Avatar student={request.student} size={26} />

              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-medium text-slate-900">
                  {request.student.firstName} {request.student.lastName}
                </p>
                <p className="truncate text-[11px] text-slate-500">
                  {request.student.school || "Wants to connect"}
                </p>
              </div>
            </div>

            {request.note && (
              <p className="mt-1 line-clamp-2 rounded-md bg-slate-50 px-1.5 py-1 text-[11px] text-slate-600">
                {request.note}
              </p>
            )}

            <div className="mt-1.5 flex gap-1">
              <button onClick={() => onAnswerRequest(request.id, true)} className={accept}>
                Accept
              </button>
              <button onClick={() => onAnswerRequest(request.id, false)} className={decline}>
                Decline
              </button>
            </div>
          </li>
        ))}

        {invitations.map((invite) => (
          <li
            key={`group-${invite.conversationId}`}
            className="rounded-lg border border-slate-200 bg-white p-1.5"
          >
            <div className="flex items-center gap-1.5">
              <Avatar
                group={{
                  id: invite.conversationId,
                  title: invite.name,
                  hasPhoto: invite.hasPhoto,
                }}
                size={26}
              />

              <div className="min-w-0 flex-1">
                <p className="truncate text-xs font-medium text-slate-900">{invite.name}</p>
                <p className="truncate text-[11px] text-slate-500">
                  {invite.invitedByName} invited you
                </p>
              </div>
            </div>

            {invite.description && (
              <p className="mt-1 line-clamp-2 px-0.5 text-[11px] text-slate-500">
                {invite.description}
              </p>
            )}

            <div className="mt-1.5 flex gap-1">
              <button
                onClick={() => onAnswerInvite(invite.conversationId, true)}
                className={accept}
              >
                Join
              </button>
              <button
                onClick={() => onAnswerInvite(invite.conversationId, false)}
                className={decline}
              >
                Decline
              </button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

/* ------------------------------------------------------------------ thread */

function Thread({ conversation, me, onChanged, onClosed }) {
  const [messages, setMessages] = useState([]);
  const [ready, setReady] = useState(false);
  const [body, setBody] = useState("");
  const [pendingFiles, setPendingFiles] = useState([]);
  const [replyTo, setReplyTo] = useState(null);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [emojiOpen, setEmojiOpen] = useState(false);
  const [menu, setMenu] = useState(null);
  const [picker, setPicker] = useState(null);
  const [editingId, setEditingId] = useState(null);
  const [presence, setPresence] = useState({ typing: [], readers: [] });

  const scroller = useRef(null);
  const content = useRef(null);
  const fileInput = useRef(null);

  // Whether the view is following the end of the conversation. True until you
  // scroll away from the bottom yourself, so an arriving message only pulls the
  // view down when you were already down there reading.
  const composer = useRef(null);
  const lastPing = useRef(0);
  const pinned = useRef(true);

  // Cleared the first time real messages are laid out - and only then. The
  // first render happens before anything has loaded, and a flag spent on an
  // empty list was the whole reason threads were opening at the top.
  const opening = useRef(true);
  const id = conversation.id;

  const load = useCallback(
    async (markRead) => {
      try {
        const found = await conversationsApi.messages(id);

        // Only re-render when something actually changed, so the poll does not
        // fight with the text box or reset the scroll position every 5 seconds.
        setMessages((current) =>
          signature(current) === signature(found) ? current : found
        );

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

  // Its own loop, on its own clock. Failures are swallowed: a typing bubble
  // that does not arrive is a missing nicety, not something to put a red error
  // bar across somebody's conversation for.
  useEffect(() => {
    let live = true;

    async function tick() {
      if (document.visibilityState !== "visible") return;

      try {
        const found = await conversationsApi.presence(id);
        if (live) setPresence(found);
      } catch {
        /* decoration only */
      }
    }

    tick();
    const timer = setInterval(tick, PRESENCE_POLL_MS);

    return () => {
      live = false;
      clearInterval(timer);
    };
  }, [id]);

  function pingTyping() {
    const now = Date.now();
    if (now - lastPing.current < TYPING_PING_MS) return;

    lastPing.current = now;
    conversationsApi.typing(id).catch(() => {});
  }

  // The newest thing I said, which is the only message a "Seen" line belongs
  // under - repeating it on every message of mine would be noise.
  const myLast = useMemo(
    () => [...messages].reverse().find((m) => m.isMine && !m.isDeleted),
    [messages]
  );

  const seenBy = useMemo(() => {
    if (!myLast) return [];

    const sent = new Date(myLast.sentAt).getTime();

    return (presence.readers ?? []).filter(
      (r) => r.lastReadAt && new Date(r.lastReadAt).getTime() >= sent
    );
  }, [myLast, presence.readers]);

  const toBottom = useCallback(() => {
    const el = scroller.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, []);

  // Before the browser paints, not after. Done in a plain effect the newest
  // message is drawn at the top for one frame and then yanked down, which is
  // the flicker a real messaging app never shows you.
  useLayoutEffect(() => {
    // An empty list is "not loaded yet", not "opened at the bottom".
    if (messages.length === 0) return;

    if (opening.current || pinned.current) {
      toBottom();
      opening.current = false;
    }
  }, [messages, toBottom]);

  // Images and files only settle their own height once they have loaded, which
  // is after the scroll above already happened - so a thread ending in a
  // picture would open part of the way up. Watching the content box re-pins the
  // view every time it grows, which covers late images, an expanding text box
  // and a reply bar appearing, without knowing about any of them.
  useEffect(() => {
    const el = scroller.current;
    const inner = content.current;
    if (!el || !inner || typeof ResizeObserver === "undefined") return;

    const observer = new ResizeObserver(() => {
      if (opening.current || pinned.current) toBottom();
    });

    observer.observe(inner);
    observer.observe(el);

    return () => observer.disconnect();
  }, [toBottom]);

  // Grown to fit, up to a point. Before the browser paints, or the box is one
  // line tall for a frame and the whole conversation above it jumps.
  useLayoutEffect(() => {
    const el = composer.current;
    if (!el) return;

    el.style.height = "0px";
    el.style.height = `${Math.min(el.scrollHeight, 128)}px`;
  }, [body]);

  function onScroll() {
    const el = scroller.current;
    if (!el) return;

    // A little slack, so "at the bottom" survives one notch of a wheel and the
    // half-pixel heights that zoomed-in browsers produce.
    pinned.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  }

  async function send(e) {
    e.preventDefault();

    const text = body.trim();
    if (!text && pendingFiles.length === 0) return;

    setSending(true);
    setError("");

    try {
      // Files go with whatever was typed, as one message - which is why they
      // wait in the tray rather than sending themselves the moment they are
      // chosen, and why all of them go in a single request.
      const message =
        pendingFiles.length > 0
          ? await conversationsApi.sendAttachment(id, pendingFiles, text)
          : await conversationsApi.send(id, text, replyTo?.id);

      setMessages((current) => [...current, message]);
      setBody("");
      setPendingFiles([]);
      setReplyTo(null);

      // Forgotten, so the next keystroke after sending pings straight away
      // rather than waiting out a throttle started before the message went.
      lastPing.current = 0;
      onChanged?.();
    } catch (err) {
      setError(err.message);
    } finally {
      setSending(false);
      if (fileInput.current) fileInput.current.value = "";
    }
  }

  async function act(fn) {
    try {
      await fn();
      await load(false);
      onChanged?.();
    } catch (err) {
      setError(err.message);
    }
  }

  const canManage = conversation.isGroup && conversation.myRole >= ROLE.ADMIN;

  return (
    <div className="flex h-full min-h-0 flex-col rounded-xl border border-slate-200 bg-white">
      <div className="flex shrink-0 items-center justify-between gap-3 border-b border-slate-200 px-4 py-3">
        <div className="flex min-w-0 items-center gap-3">
          {conversation.isGroup ? (
            <Avatar
              group={{
                id: conversation.id,
                title: conversation.title,
                hasPhoto: conversation.hasPhoto,
              }}
              size={36}
            />
          ) : (
            <Avatar student={conversation.members[0]} size={36} />
          )}

          <div className="min-w-0">
            <p className="truncate font-medium text-slate-900">{conversation.title}</p>
            <p className="truncate text-xs text-slate-500">
              {conversation.isGroup
                ? conversation.description || `${conversation.members.length + 1} people`
                : conversation.members[0]?.school || "Direct message"}
            </p>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-1">
          <button
            onClick={() => act(() => conversationsApi.setMuted(id, !conversation.isMuted))}
            title={conversation.isMuted ? "Unmute this chat" : "Mute this chat"}
            aria-label={conversation.isMuted ? "Unmute this chat" : "Mute this chat"}
            aria-pressed={conversation.isMuted}
            className={`rounded-md p-2 transition hover:bg-slate-100 ${
              conversation.isMuted ? "text-slate-400" : "text-slate-600"
            }`}
          >
            <BellIcon muted={conversation.isMuted} />
          </button>

          {conversation.isGroup && (
            <button
              onClick={() => setSettingsOpen(true)}
              className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              {canManage ? "Group settings" : "Group info"}
            </button>
          )}
        </div>
      </div>

      <div
        ref={scroller}
        onScroll={onScroll}
        className="min-h-0 flex-1 overflow-y-auto px-4 py-4"
      >
        {/* One box around the whole list, so its height can be watched. */}
        <div ref={content} className="space-y-1">
        {!ready ? (
          <p className="text-sm text-slate-500">Loading...</p>
        ) : messages.length === 0 ? (
          <p className="text-sm text-slate-500">No messages yet. Say something.</p>
        ) : (
          messages.map((message, i) => {
            const previous = messages[i - 1];

            // A heading whenever the day changes, and above the very first
            // message so a short conversation is still dated.
            const newDay =
              !previous ||
              new Date(previous.sentAt).toDateString() !==
                new Date(message.sentAt).toDateString();

            return (
              <div key={message.id}>
                {newDay && (
                  <p className="py-3 text-center text-xs text-slate-400">
                    <span className="font-semibold text-slate-500">
                      {dayLabel(message.sentAt)}
                    </span>{" "}
                    {timeLabel(message.sentAt)}
                  </p>
                )}

                <MessageRow
                  message={message}
                  previous={previous}
                  conversationId={id}
                  isGroup={conversation.isGroup}
                  editing={editingId === message.id}
                  onStartEdit={() => setEditingId(message.id)}
                  onStopEdit={() => setEditingId(null)}
                  onEdit={(text) =>
                    act(() => conversationsApi.editMessage(id, message.id, text))
                  }
                  onReact={(emoji) => act(() => conversationsApi.react(id, message.id, emoji))}
                  onMenu={(e) => {
                    e.preventDefault();
                    setMenu({ x: e.clientX, y: e.clientY, message });
                  }}
                />
              </div>
            );
          })
        )}

        {/* Under the newest thing I said, and nowhere else. */}
        {myLast && (
          <p className="pt-0.5 text-right text-[11px] text-slate-400">
            {seenBy.length === 0
              ? "Sent"
              : conversation.isGroup
              ? `Seen by ${seenBy.length}`
              : "Seen"}
          </p>
        )}

        {presence.typing?.length > 0 && (
          <TypingBubble
            names={presence.typing.map((t) => t.firstName)}
            isGroup={conversation.isGroup}
          />
        )}
        </div>
      </div>

      {error && (
        <p className="shrink-0 border-t border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      {replyTo && (
        <div className="flex shrink-0 items-center justify-between gap-3 border-t border-slate-200 bg-slate-50 px-4 py-2">
          <p className="min-w-0 truncate text-xs text-slate-600">
            Replying to <span className="font-medium">{replyTo.senderName}</span>:{" "}
            {replyTo.body}
          </p>
          <button
            onClick={() => setReplyTo(null)}
            className="shrink-0 text-sm text-slate-500 hover:text-slate-900"
          >
            &times;
          </button>
        </div>
      )}

      {pendingFiles.length > 0 && (
        <div className="shrink-0 border-t border-slate-200 bg-slate-50 px-4 py-2">
          <p className="text-xs text-slate-500">
            {pendingFiles.length === 1 ? "1 file" : `${pendingFiles.length} files`} ready
            — add a message if you want, then Send.
          </p>

          <ul className="mt-1 max-h-28 space-y-1 overflow-y-auto">
            {pendingFiles.map((file, i) => (
              // Keyed by position as well as name: picking the same file twice
              // is allowed, and the names alone would collide.
              <li
                key={`${file.name}-${i}`}
                className="flex items-center justify-between gap-3"
              >
                <span className="min-w-0 truncate text-xs text-slate-600">
                  <span className="font-medium">{file.name}</span>{" "}
                  <span className="text-slate-400">({formatSize(file.size)})</span>
                </span>

                <button
                  type="button"
                  aria-label={`Remove ${file.name}`}
                  onClick={() =>
                    setPendingFiles((current) => current.filter((_, at) => at !== i))
                  }
                  className="shrink-0 text-sm text-slate-500 hover:text-red-600"
                >
                  &times;
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <form
        onSubmit={send}
        className="relative flex shrink-0 items-end gap-1 border-t border-slate-200 p-3"
      >
        <div className="relative">
          <button
            type="button"
            onClick={() => setEmojiOpen((v) => !v)}
            aria-label="Add an emoji"
            className="rounded-md px-2 py-1.5 text-xl leading-none transition hover:bg-slate-100"
          >
            🙂
          </button>

          {emojiOpen && (
            <EmojiPicker
              onClose={() => setEmojiOpen(false)}
              onPick={(emoji) => setBody((current) => current + emoji)}
            />
          )}
        </div>

        <button
          type="button"
          onClick={() => fileInput.current?.click()}
          aria-label="Attach a file"
          title="Attach images or files"
          className="rounded-md px-2 py-1.5 text-slate-500 transition hover:bg-slate-100"
        >
          <svg
            width="20" height="20" viewBox="0 0 24 24" fill="none"
            stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"
            strokeLinejoin="round" aria-hidden="true"
          >
            <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
          </svg>
        </button>

        <input
          ref={fileInput}
          type="file"
          multiple
          accept="image/jpeg,image/png,image/webp,image/gif,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv,.zip"
          onChange={(e) => {
            const chosen = Array.from(e.target.files ?? []);

            // Added to what is already waiting rather than replacing it, so you
            // can pick from two folders in a row. Then cleared, because
            // choosing the same file again fires no change event otherwise.
            setPendingFiles((current) =>
              [...current, ...chosen].slice(0, MAX_ATTACHMENTS)
            );
            e.target.value = "";
          }}
          className="hidden"
        />

        <textarea
          ref={composer}
          rows={1}
          value={body}
          onChange={(e) => {
            setBody(e.target.value);
            pingTyping();
          }}
          onKeyDown={(e) => {
            // Enter sends, Shift+Enter starts a new line - the arrangement
            // every messaging app uses. isComposing is checked because an IME
            // uses Enter to choose a word, and without it typing in Japanese or
            // Chinese would fire off a message mid-word.
            if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
              e.preventDefault();
              send(e);
            }
          }}
          placeholder={pendingFiles.length > 0 ? "Add a message (optional)" : "Message"}
          maxLength={4000}
          className="ml-1 max-h-32 flex-1 resize-none overflow-y-auto rounded-md border border-slate-300 px-3 py-2 text-sm leading-5 outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />

        <button
          type="submit"
          disabled={sending || (!body.trim() && pendingFiles.length === 0)}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
        >
          {sending ? "Sending..." : "Send"}
        </button>
      </form>

      {menu && (
        <ContextMenu
          x={menu.x}
          y={menu.y}
          onClose={() => setMenu(null)}
          header={
            <div className="flex items-center gap-0.5 px-1.5 pb-1">
              {QUICK_REACTIONS.map((emoji) => (
                <button
                  key={emoji}
                  type="button"
                  title={`React ${emoji}`}
                  aria-label={`React ${emoji}`}
                  onClick={() => {
                    const target = menu.message.id;
                    setMenu(null);
                    act(() => conversationsApi.react(id, target, emoji));
                  }}
                  className="rounded-full px-1.5 py-1 text-lg leading-none transition hover:bg-slate-100"
                >
                  {emoji}
                </button>
              ))}

              {/* Anything not in the row above. Placed here rather than as a
                  menu line, because it belongs with the emoji it extends. */}
              <button
                type="button"
                title="More emoji"
                aria-label="More emoji"
                onClick={() => {
                  // Clamped so the picker, which opens upward and is 18rem
                  // wide, cannot open off the top or the right of the window.
                  setPicker({
                    x: Math.min(menu.x, window.innerWidth - 300),
                    y: Math.max(menu.y, 380),
                    message: menu.message,
                  });
                  setMenu(null);
                }}
                className="ml-1 rounded-full border border-slate-200 px-2 py-1 text-sm font-medium leading-none text-slate-500 transition hover:bg-slate-100"
              >
                +
              </button>
            </div>
          }
          items={[
            { label: "Reply", onClick: () => setReplyTo(menu.message) },
            menu.message.isMine &&
              !menu.message.isDeleted && {
                label: "Edit",
                onClick: () => setEditingId(menu.message.id),
              },
            (menu.message.isMine || canManage) &&
              !menu.message.isDeleted && {
                label: "Delete",
                danger: true,
                onClick: () =>
                  confirm("Delete this message?") &&
                  act(() => conversationsApi.deleteMessage(id, menu.message.id)),
              },
          ].filter(Boolean)}
        />
      )}

      {/* Lives here, beside the menu that opens it - `picker` is Thread's own
          state, and the page around Thread cannot see it. */}
      {picker && (
        <div className="fixed z-50" style={{ left: picker.x, top: picker.y }}>
          <EmojiPicker
            onClose={() => setPicker(null)}
            onPick={(emoji) => {
              const target = picker.message.id;
              setPicker(null);
              act(() => conversationsApi.react(id, target, emoji));
            }}
          />
        </div>
      )}

      {settingsOpen && (
        <GroupSettings
          conversation={conversation}
          me={me}
          onChanged={async () => {
            await load(false);
            onChanged?.();
          }}
          onLeft={() => {
            setSettingsOpen(false);
            onClosed?.();
          }}
          onClose={() => setSettingsOpen(false)}
        />
      )}
    </div>
  );
}

/* -------------------------------------------------------------- attachment */

/**
 * Images are fetched as blobs for the same reason avatars are: they sit behind
 * the bearer token, so an <img src> at the API comes back 401. Everything else
 * is a download link, and the server refuses to serve it inline regardless of
 * what it claims to be.
 */
const attachmentUrls = new Map();

function AttachmentView({ conversationId, attachment }) {
  const [url, setUrl] = useState(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!attachment.isImage) return;

    const key = `${conversationId}:${attachment.id}`;

    if (!attachmentUrls.has(key)) {
      attachmentUrls.set(
        key,
        conversationsApi
          .attachment(conversationId, attachment.id)
          .then(({ url: blobUrl }) => blobUrl)
          .catch(() => null)
      );
    }

    let live = true;
    attachmentUrls.get(key).then((found) => {
      if (!live) return;
      if (found) setUrl(found);
      else setFailed(true);
    });

    return () => {
      live = false;
    };
  }, [conversationId, attachment.id, attachment.isImage]);

  async function download() {
    try {
      const { url: blobUrl } = await conversationsApi.attachment(
        conversationId,
        attachment.id
      );

      const link = document.createElement("a");
      link.href = blobUrl;
      link.download = attachment.fileName;

      // Appended before the click: a detached link does nothing in Firefox.
      document.body.appendChild(link);
      link.click();
      link.remove();

      // Released on the next tick rather than immediately. Revoking in the same
      // turn can cut the download off before the browser has finished reading
      // the blob, which looks exactly like a file that failed to save.
      setTimeout(() => URL.revokeObjectURL(blobUrl), 30_000);
    } catch {
      setFailed(true);
    }
  }

  if (attachment.isImage) {
    if (failed) {
      return <p className="text-xs text-slate-400">Image could not be loaded.</p>;
    }

    return url ? (
      <span className="group relative inline-block">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img
          src={url}
          alt={attachment.fileName}
          onClick={() => window.open(url, "_blank", "noopener")}
          className="block max-h-64 cursor-zoom-in rounded-lg border border-slate-200 object-contain"
        />

        {/* Its own control, because clicking the picture opens it full size -
            one target cannot mean both "look closer" and "keep this". */}
        <button
          type="button"
          onClick={download}
          title={`Download ${attachment.fileName}`}
          aria-label={`Download ${attachment.fileName}`}
          className="absolute right-2 top-2 rounded-md bg-white/90 p-1.5 text-slate-700 opacity-0 shadow transition hover:bg-white focus:opacity-100 group-hover:opacity-100"
        >
          <DownloadIcon />
        </button>
      </span>
    ) : (
      <div className="h-32 w-48 animate-pulse rounded-lg bg-slate-100" />
    );
  }

  return (
    <button
      onClick={download}
      className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-left text-sm transition hover:bg-slate-50"
    >
      <svg
        width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
        strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"
        className="shrink-0 text-slate-400" aria-hidden="true"
      >
        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" />
        <path d="M14 2v6h6" />
      </svg>

      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium text-slate-800">
          {attachment.fileName}
        </span>
        <span className="block text-xs text-slate-500">
          {formatSize(attachment.sizeBytes)}
        </span>
      </span>

      {/* The card was already a download; nothing said so. */}
      <span className="shrink-0 text-slate-400" aria-hidden="true">
        <DownloadIcon />
      </span>
    </button>
  );
}

function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;

  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/* ----------------------------------------------------------------- message */

function MessageRow({
  message,
  previous,
  conversationId,
  isGroup,
  editing,
  onStartEdit,
  onStopEdit,
  onEdit,
  onReact,
  onMenu,
}) {
  const [draft, setDraft] = useState(message.body);

  useEffect(() => {
    if (editing) setDraft(message.body);
  }, [editing, message.body]);

  // The app narrating a change to the group, not somebody talking. Centred and
  // quiet so it reads as a note rather than a message from a person.
  if (message.kind === 1) {
    return <p className="py-1 text-center text-xs text-slate-400">{message.body}</p>;
  }

  // Only label a sender when it changes, so a run of messages from one person
  // reads as a block rather than a stack of name tags.
  const newSender = previous?.senderId !== message.senderId || previous?.kind === 1;

  if (editing) {
    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (draft.trim()) onEdit(draft.trim());
          onStopEdit();
        }}
        className="flex justify-end gap-2 py-1"
      >
        <textarea
          autoFocus
          rows={2}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
              e.preventDefault();
              if (draft.trim()) onEdit(draft.trim());
              onStopEdit();
            }
          }}
          className="w-2/3 resize-none rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500"
        />
        <button type="submit" className="text-sm font-medium text-indigo-600">
          Save
        </button>
        <button type="button" onClick={onStopEdit} className="text-sm text-slate-500">
          Cancel
        </button>
      </form>
    );
  }

  return (
    <div className={`flex ${message.isMine ? "justify-end" : "justify-start"}`}>
      {/* Right-click rather than hover controls: a message that changes shape
          as the pointer crosses it is distracting to read past, and there is no
          hover at all on a touchscreen. */}
      <div className="max-w-[80%]" onContextMenu={onMenu}>
        {isGroup && !message.isMine && newSender && (
          <p className="mb-0.5 text-xs font-medium text-slate-500">{message.senderName}</p>
        )}

        {message.replyToId && (
          <div className="mb-0.5 border-l-2 border-slate-300 pl-2 text-xs text-slate-500">
            <span className="font-medium">{message.replyToSender ?? "Someone"}</span>:{" "}
            {message.replyToBody}
          </div>
        )}

        {message.attachments?.length > 0 && !message.isDeleted && (
          <div className={`mb-1 space-y-1 ${message.isMine ? "flex flex-col items-end" : ""}`}>
            {message.attachments.map((attachment) => (
              <AttachmentView
                key={attachment.id}
                conversationId={conversationId}
                attachment={attachment}
              />
            ))}
          </div>
        )}

        {/* A file sent on its own has no caption, and an empty bubble under it
            would just be a coloured smudge. */}
        {(message.body || message.isDeleted) && (
          <div
            className={`rounded-2xl px-3 py-2 text-sm ${
              message.isDeleted
                ? "bg-slate-100 italic text-slate-400"
                : message.isMine
                ? "bg-indigo-600 text-white"
                : "bg-slate-100 text-slate-900"
            }`}
          >
            {/* pre-wrap so the line breaks somebody typed survive, break-words
                so one long unbroken string cannot stretch the bubble off the
                side of the conversation. */}
            <span className="whitespace-pre-wrap break-words">
              {message.isDeleted ? "Message deleted" : message.body}
            </span>
          </div>
        )}

        {message.reactions?.length > 0 && (
          <div className={`mt-1 flex flex-wrap gap-1 ${message.isMine ? "justify-end" : ""}`}>
            {message.reactions.map((reaction) => (
              <button
                key={reaction.emoji}
                onClick={() => onReact(reaction.emoji)}
                title={reaction.mine ? "Remove your reaction" : "React"}
                className={`rounded-full border px-2 py-0.5 text-xs transition ${
                  reaction.mine
                    ? "border-indigo-300 bg-indigo-50 text-indigo-700"
                    : "border-slate-200 bg-white text-slate-600 hover:bg-slate-50"
                }`}
              >
                {reaction.emoji} {reaction.count}
              </button>
            ))}
          </div>
        )}

        <p
          className={`mt-0.5 flex items-center gap-2 text-[11px] text-slate-400 ${
            message.isMine ? "justify-end" : ""
          }`}
        >
          <span>{timeLabel(message.sentAt)}</span>

          {/* Always shown. An edit that leaves no trace is a way to rewrite what
              somebody remembers being said. */}
          {message.isEdited && !message.isDeleted && <span>edited</span>}
        </p>
      </div>
    </div>
  );
}

/* --------------------------------------------------------------- new group */

function NewGroup({ onClose, onCreated }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [photo, setPhoto] = useState(null);
  const [people, setPeople] = useState([]);
  const [chosen, setChosen] = useState([]);
  const [filter, setFilter] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const photoInput = useRef(null);

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
      const conversation = await conversationsApi.createGroup(
        name.trim(),
        chosen,
        description.trim()
      );

      // Uploaded after the group exists, because there is nothing to attach it
      // to until then. A failure here is not worth losing the group over - the
      // picture can be set again from Group settings.
      if (photo) {
        try {
          await conversationsApi.uploadGroupPhoto(conversation.id, photo);
          forgetPhoto(conversation.id, "group");
        } catch {
          // Deliberately swallowed; the group was created successfully.
        }
      }

      onCreated(conversation);
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  }

  const field =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-6 py-6">
      <button
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full bg-slate-900/25"
      />

      <form
        onSubmit={create}
        className="relative flex max-h-full w-full max-w-md flex-col rounded-xl border border-slate-200 bg-white shadow-xl"
      >
        <div className="shrink-0 border-b border-slate-200 px-6 py-4">
          <h2 className="font-medium text-slate-900">New group</h2>
          <p className="mt-1 text-xs text-slate-500">
            You will own it. Everyone you pick gets an invitation to accept.
          </p>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-6 py-5">
          {error && (
            <p className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {error}
            </p>
          )}

          <div className="flex items-center gap-4">
            {photo ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={URL.createObjectURL(photo)}
                alt=""
                className="h-16 w-16 rounded-full object-cover"
              />
            ) : (
              <span className="flex h-16 w-16 items-center justify-center rounded-full bg-slate-100 text-xl text-slate-400">
                #
              </span>
            )}

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => photoInput.current?.click()}
                className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
              >
                {photo ? "Change photo" : "Add photo"}
              </button>

              {photo && (
                <button
                  type="button"
                  onClick={() => {
                    setPhoto(null);
                    if (photoInput.current) photoInput.current.value = "";
                  }}
                  className="rounded-md px-3 py-1.5 text-sm font-medium text-slate-500 transition hover:text-red-600"
                >
                  Remove
                </button>
              )}

              <input
                ref={photoInput}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(e) => setPhoto(e.target.files?.[0] ?? null)}
                className="hidden"
              />
            </div>
          </div>

          <label className="mt-4 block text-sm font-medium text-slate-700">Name</label>
          <input
            required
            autoFocus
            maxLength={100}
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="CS 201 study group"
            className={field}
          />

          <label className="mt-3 block text-sm font-medium text-slate-700">
            Description <span className="font-normal text-slate-400">(optional)</span>
          </label>
          <input
            maxLength={300}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Weekly problem sets and exam prep"
            className={field}
          />

          <p className="mt-4 text-sm font-medium text-slate-700">
            Who to invite{chosen.length > 0 && ` — ${chosen.length} selected`}
          </p>
          <p className="mt-0.5 text-xs text-slate-500">
            Only people you are connected with can be invited.
          </p>

          {people.length > 5 && (
            <input
              type="search"
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
              placeholder="Search connections"
              className={field}
            />
          )}

          {people.length === 0 ? (
            <p className="mt-3 text-sm text-slate-500">
              You have no connections yet, so there is nobody to invite.
            </p>
          ) : (
            <ul className="mt-2 max-h-48 space-y-1 overflow-y-auto">
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
        </div>

        <div className="flex shrink-0 gap-2 border-t border-slate-200 px-6 py-4">
          <button
            type="submit"
            disabled={busy || chosen.length === 0 || !name.trim()}
            className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
          >
            {busy ? "Creating..." : "Create group"}
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

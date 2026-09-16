'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  conversations as conversationsApi,
  connections as connectionsApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Avatar, { forgetPhoto } from "@/components/Avatar";
import { filterStudents } from "@/lib/studentSearch";
import EmojiPicker, { QUICK_REACTIONS } from "@/components/EmojiPicker";

/**
 * While a conversation is open it is re-fetched on this interval.
 *
 * Polling, not push. SignalR replaces exactly this timer; everything else on
 * the page stays as it is.
 */
const POLL_MS = 5_000;

/**
 * The heading above the first message of each day.
 *
 * Today and yesterday get words rather than a date, because that is how
 * people actually refer to them, and a bare "16 Sep" on today's messages
 * makes a live conversation read like an archive.
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

/** Matches ConversationRole in the API. Ordered, so comparisons work. */
const ROLE = { MEMBER: 0, ADMIN: 1, OWNER: 2 };

const ROLE_LABEL = { 0: "Member", 1: "Admin", 2: "Owner" };

export default function MessagesPage() {
  const { user, loading } = useAuth();

  const [list, setList] = useState([]);
  const [invitations, setInvitations] = useState([]);
  const [ready, setReady] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [error, setError] = useState("");
  const [creating, setCreating] = useState(false);

  const loadList = useCallback(async () => {
    try {
      const [found, invites] = await Promise.all([
        conversationsApi.list(),
        conversationsApi.invitations(),
      ]);

      setList(found);
      setInvitations(invites);
      return found;
    } catch (err) {
      setError(err.message);
      return [];
    } finally {
      setReady(true);
    }
  }, []);

  /**
   * Takes a conversation off your list.
   *
   * Deliberately not a delete. Leaving a group is permanent; removing a direct
   * conversation only hides it from you, and it reappears if the other person
   * writes again - their messages are theirs, and quietly throwing them away
   * for both sides is not what an X on a row should do.
   */
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

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const open = list.find((c) => c.id === openId);

  return (
    // Fills the window rather than sitting in a fixed-height box. The
    // subtracted 4.5rem is the nav bar, rounded up: spare pixels are invisible,
    // too few bring back the page scrollbar this exists to remove. dvh rather
    // than vh so a phone's collapsing address bar cannot hide the composer.
    <div className="mx-auto flex h-[calc(100dvh-4.5rem)] max-w-6xl flex-col px-6 py-6">
      <div className="flex shrink-0 items-center justify-between">
        <h1 className="text-2xl font-semibold">Messages</h1>
        <button
          onClick={() => setCreating(true)}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
        >
          New group
        </button>
      </div>

      {error && (
        <div className="mt-3 flex shrink-0 items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      {invitations.length > 0 && (
        <div className="mt-3 shrink-0 space-y-2">
          {invitations.map((invite) => (
            <div
              key={invite.conversationId}
              className="flex flex-wrap items-center gap-3 rounded-lg border border-indigo-200 bg-indigo-50 px-4 py-3"
            >
              <Avatar
                group={{
                  id: invite.conversationId,
                  title: invite.name,
                  hasPhoto: invite.hasPhoto,
                }}
                size={36}
              />

              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-slate-900">{invite.name}</p>
                <p className="truncate text-xs text-slate-600">
                  {invite.invitedByName} invited you · {invite.memberCount}{" "}
                  {invite.memberCount === 1 ? "person" : "people"}
                  {invite.description ? ` · ${invite.description}` : ""}
                </p>
              </div>

              <div className="flex shrink-0 gap-2">
                <button
                  onClick={() => answerInvite(invite.conversationId, true)}
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
                >
                  Join
                </button>
                <button
                  onClick={() => answerInvite(invite.conversationId, false)}
                  className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                >
                  Decline
                </button>
              </div>
            </div>
          ))}
        </div>
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
        // min-h-0 on the grid and both panes: without it a flex or grid child
        // refuses to shrink below its content, so the inner scroll areas push
        // the page into scrolling instead of scrolling themselves.
        <div className="mt-4 grid min-h-0 flex-1 gap-4 md:grid-cols-[18rem_1fr]">
          <ul className="min-h-0 space-y-1 overflow-y-auto">
            {list.map((conversation) => (
              <li
                key={conversation.id}
                className={`flex items-center rounded-lg pr-1 transition ${
                  openId === conversation.id ? "bg-indigo-50" : "hover:bg-slate-100"
                }`}
              >
                <button
                  onClick={() => setOpenId(conversation.id)}
                  className="flex min-w-0 flex-1 items-center gap-3 rounded-lg px-3 py-2 text-left"
                >
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

                  <span className="min-w-0 flex-1">
                    <span className="flex items-center justify-between gap-2">
                      <span className="truncate text-sm font-medium text-slate-900">
                        {conversation.title}
                      </span>

                      {conversation.isMuted ? (
                        <span className="shrink-0 text-xs text-slate-400" title="Muted">
                          muted
                        </span>
                      ) : (
                        conversation.unreadCount > 0 && (
                          <span className="shrink-0 rounded-full bg-red-500 px-1.5 py-0.5 text-[11px] font-semibold text-white">
                            {conversation.unreadCount}
                          </span>
                        )
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

                <button
                  onClick={() => remove(conversation)}
                  title={conversation.isGroup ? "Leave group" : "Remove from list"}
                  aria-label={
                    conversation.isGroup
                      ? `Leave ${conversation.title}`
                      : `Remove conversation with ${conversation.title}`
                  }
                  className="shrink-0 rounded-md px-2 py-1 text-lg leading-none text-slate-300 transition hover:bg-white hover:text-red-600"
                >
                  &times;
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

function Thread({ conversation, me, onChanged, onClosed }) {
  const [messages, setMessages] = useState([]);
  const [ready, setReady] = useState(false);
  const [body, setBody] = useState("");
  const [replyTo, setReplyTo] = useState(null);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");
  const [panelOpen, setPanelOpen] = useState(false);
  const [emojiOpen, setEmojiOpen] = useState(false);
  const [attaching, setAttaching] = useState(false);
  const fileInput = useRef(null);

  const scroller = useRef(null);
  const atFirstPaint = useRef(true);
  const id = conversation.id;

  const load = useCallback(
    async (markRead) => {
      try {
        const found = await conversationsApi.messages(id);

        // Only re-render when something actually changed, so the poll does not
        // fight with the text box or reset the scroll position every 5 seconds.
        setMessages((current) => {
          const same =
            current.length === found.length &&
            current[current.length - 1]?.id === found[found.length - 1]?.id &&
            current[current.length - 1]?.body === found[found.length - 1]?.body;

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
    const el = scroller.current;
    if (!el) return;

    // Opening a conversation lands at the newest message, the way every
    // messaging app does. Set directly rather than scrolled into view, so
    // it is already there rather than travelling there while you watch.
    if (atFirstPaint.current) {
      el.scrollTop = el.scrollHeight;
      atFirstPaint.current = false;
      return;
    }

    // After that, only follow new messages if you were already near the
    // bottom. Somebody reading back through history should not be yanked
    // away every time another person types.
    const nearBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 200;
    if (nearBottom) el.scrollTop = el.scrollHeight;
  }, [messages]);

  async function send(e) {
    e.preventDefault();

    const text = body.trim();
    if (!text) return;

    setSending(true);
    setError("");

    try {
      const message = await conversationsApi.send(id, text, replyTo?.id);
      setMessages((current) => [...current, message]);
      setBody("");
      setReplyTo(null);
      onChanged?.();
    } catch (err) {
      setError(err.message);
    } finally {
      setSending(false);
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

  /** Whatever is already typed rides along as the caption. */
  async function attach(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    setAttaching(true);
    setError("");

    try {
      const message = await conversationsApi.sendAttachment(id, file, body.trim());
      setMessages((current) => [...current, message]);
      setBody("");
      onChanged?.();
    } catch (err) {
      setError(err.message);
    } finally {
      setAttaching(false);
      if (fileInput.current) fileInput.current.value = "";
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
                ? conversation.description ||
                  `${conversation.members.length + 1} people`
                : conversation.members[0]?.school || "Direct message"}
            </p>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <button
            onClick={() =>
              act(() => conversationsApi.setMuted(id, !conversation.isMuted))
            }
            title={conversation.isMuted ? "Unmute" : "Mute"}
            className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
          >
            {conversation.isMuted ? "Unmute" : "Mute"}
          </button>

          {conversation.isGroup && (
            <button
              onClick={() => setPanelOpen((v) => !v)}
              aria-expanded={panelOpen}
              className="flex items-center gap-1.5 rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              <svg
                width="15" height="15" viewBox="0 0 24 24" fill="none"
                stroke="currentColor" strokeWidth="2" strokeLinecap="round"
                strokeLinejoin="round" aria-hidden="true"
              >
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                <circle cx="9" cy="7" r="4" />
                <path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
              </svg>

              {conversation.members.length + 1} people

              <svg
                width="12" height="12" viewBox="0 0 24 24" fill="none"
                stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"
                strokeLinejoin="round" aria-hidden="true"
                className={`transition ${panelOpen ? "rotate-180" : ""}`}
              >
                <path d="m6 9 6 6 6-6" />
              </svg>
            </button>
          )}
        </div>
      </div>

      {panelOpen && conversation.isGroup && (
        <GroupPanel
          conversation={conversation}
          me={me}
          onChanged={async () => {
            await load(false);
            onChanged?.();
          }}
          onLeft={onClosed}
          onError={setError}
        />
      )}

      <div ref={scroller} className="min-h-0 flex-1 space-y-1 overflow-y-auto px-4 py-4">
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
              canModerate={canManage}
              onReply={() => setReplyTo(message)}
              onReact={(emoji) =>
                act(() => conversationsApi.react(id, message.id, emoji))
              }
              onEdit={(text) =>
                act(() => conversationsApi.editMessage(id, message.id, text))
              }
              onDelete={() =>
                act(() => conversationsApi.deleteMessage(id, message.id))
              }
                />
              </div>
            );
          })
        )}
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

      <form onSubmit={send} className="relative flex shrink-0 items-center gap-2 border-t border-slate-200 p-3">
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
          disabled={attaching}
          aria-label="Attach a file"
          title="Attach an image or file"
          className="rounded-md px-2 py-1.5 text-slate-500 transition hover:bg-slate-100 disabled:opacity-50"
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
          accept="image/jpeg,image/png,image/webp,image/gif,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv,.zip"
          onChange={attach}
          className="hidden"
        />

        <input
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder={attaching ? "Sending file..." : "Message"}
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

/* -------------------------------------------------------------- attachment */

/**
 * One file on a message.
 *
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
      link.click();

      URL.revokeObjectURL(blobUrl);
    } catch {
      setFailed(true);
    }
  }

  if (attachment.isImage) {
    if (failed) {
      return <p className="text-xs text-slate-400">Image could not be loaded.</p>;
    }

    return url ? (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={url}
        alt={attachment.fileName}
        onClick={() => window.open(url, "_blank", "noopener")}
        className="max-h-64 cursor-zoom-in rounded-lg border border-slate-200 object-contain"
      />
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

      <span className="min-w-0">
        <span className="block truncate font-medium text-slate-800">
          {attachment.fileName}
        </span>
        <span className="block text-xs text-slate-500">
          {formatSize(attachment.sizeBytes)}
        </span>
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
  canModerate,
  onReply,
  onReact,
  onEdit,
  onDelete,
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(message.body);
  const [reactOpen, setReactOpen] = useState(false);

  // The app narrating a change to the group, not somebody talking. Centred and
  // quiet so it reads as a note rather than a message from a person.
  if (message.kind === 1) {
    return (
      <p className="py-1 text-center text-xs text-slate-400">{message.body}</p>
    );
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
          setEditing(false);
        }}
        className="flex justify-end gap-2 py-1"
      >
        <input
          autoFocus
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          className="w-2/3 rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500"
        />
        <button type="submit" className="text-sm font-medium text-indigo-600">
          Save
        </button>
        <button
          type="button"
          onClick={() => {
            setDraft(message.body);
            setEditing(false);
          }}
          className="text-sm text-slate-500"
        >
          Cancel
        </button>
      </form>
    );
  }

  return (
    <div className={`group flex ${message.isMine ? "justify-end" : "justify-start"}`}>
      <div className="max-w-[80%]">
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

        {/* A file sent on its own has no caption, and an empty bubble under
            it would just be a coloured smudge. */}
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
            {message.isDeleted ? "Message deleted" : message.body}
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

        <div
          className={`mt-0.5 flex items-center gap-2 text-[11px] text-slate-400 ${
            message.isMine ? "justify-end" : ""
          }`}
        >
          <span>{timeLabel(message.sentAt)}</span>

          {/* Always shown, never hidden. An edit that leaves no trace is a way
              to rewrite what somebody remembers being said. */}
          {message.isEdited && !message.isDeleted && <span>edited</span>}

          {!message.isDeleted && (
            <span className="relative hidden gap-2 group-hover:flex">
              <button
                onClick={() => setReactOpen((v) => !v)}
                className="hover:text-slate-700"
              >
                React
              </button>

              {reactOpen && (
                <div className="absolute bottom-full right-0 z-30 mb-1 flex gap-0.5 rounded-full border border-slate-200 bg-white px-1.5 py-1 shadow-lg">
                  {QUICK_REACTIONS.map((emoji) => (
                    <button
                      key={emoji}
                      onClick={() => {
                        onReact(emoji);
                        setReactOpen(false);
                      }}
                      className="rounded-full px-1 text-base transition hover:bg-slate-100"
                    >
                      {emoji}
                    </button>
                  ))}
                </div>
              )}

              <button onClick={onReply} className="hover:text-slate-700">
                Reply
              </button>

              {message.isMine && (
                <button onClick={() => setEditing(true)} className="hover:text-slate-700">
                  Edit
                </button>
              )}

              {(message.isMine || canModerate) && (
                <button
                  onClick={() => confirm("Delete this message?") && onDelete()}
                  className="hover:text-red-600"
                >
                  Delete
                </button>
              )}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------- group panel */

function GroupPanel({ conversation, me, onChanged, onLeft, onError }) {
  const [members, setMembers] = useState([]);
  const [connections, setConnections] = useState([]);
  const [filter, setFilter] = useState("");
  const [adding, setAdding] = useState([]);
  const [editingName, setEditingName] = useState(false);
  const [name, setName] = useState(conversation.title);
  const [description, setDescription] = useState(conversation.description ?? "");
  const [busy, setBusy] = useState(false);

  const photoInput = useRef(null);
  const id = conversation.id;

  const myRole = conversation.myRole;
  const isOwner = myRole === ROLE.OWNER;
  const canManage = myRole >= ROLE.ADMIN;

  const load = useCallback(async () => {
    try {
      setMembers(await conversationsApi.members(id));
    } catch (err) {
      onError(err.message);
    }
  }, [id, onError]);

  useEffect(() => {
    load();
    if (canManage) connectionsApi.list().then(setConnections).catch(() => {});
  }, [load, canManage]);

  async function act(fn) {
    setBusy(true);

    try {
      await fn();
      await load();
      await onChanged();
    } catch (err) {
      onError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function uploadPhoto(e) {
    const file = e.target.files?.[0];
    if (!file) return;

    await act(async () => {
      await conversationsApi.uploadGroupPhoto(id, file);

      // The cached blob is keyed by conversation id, so without this the old
      // picture stays on screen until a full reload.
      forgetPhoto(id, "group");
    });

    if (photoInput.current) photoInput.current.value = "";
  }

  // Anyone already in the group, or already invited, is not offered again.
  const invitable = filterStudents(
    connections.filter((person) => !members.some((m) => m.student.id === person.id)),
    filter
  );

  return (
    <div className="max-h-72 shrink-0 overflow-y-auto border-b border-slate-200 bg-slate-50 px-4 py-3">
      {canManage && (
        <div className="mb-4">
          {editingName ? (
            <div className="space-y-2">
              <input
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={100}
                placeholder="Group name"
                className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500"
              />
              <input
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                maxLength={300}
                placeholder="What is this group for? (optional)"
                className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500"
              />
              <div className="flex gap-2">
                <button
                  disabled={busy || !name.trim()}
                  onClick={() =>
                    act(async () => {
                      await conversationsApi.updateGroup(id, name.trim(), description.trim());
                      setEditingName(false);
                    })
                  }
                  className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
                >
                  Save
                </button>
                <button
                  onClick={() => {
                    setName(conversation.title);
                    setDescription(conversation.description ?? "");
                    setEditingName(false);
                  }}
                  className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700"
                >
                  Cancel
                </button>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap gap-2">
              <button
                onClick={() => setEditingName(true)}
                className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
              >
                Edit name and description
              </button>
              <button
                onClick={() => photoInput.current?.click()}
                disabled={busy}
                className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                {conversation.hasPhoto ? "Change photo" : "Add photo"}
              </button>
              <input
                ref={photoInput}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={uploadPhoto}
                className="hidden"
              />
            </div>
          )}
        </div>
      )}

      <p className="text-xs font-medium uppercase tracking-wide text-slate-400">People</p>

      <ul className="mt-2 space-y-1">
        {members.map((member) => {
          const them = member.student;
          const pending = Boolean(member.invitedByName);
          const isMe = them.id === me?.id;

          return (
            <li
              key={them.id}
              className="flex flex-wrap items-center gap-2 rounded-md bg-white px-2 py-1.5"
            >
              <Avatar student={them} size={28} />

              <div className="min-w-0 flex-1">
                <p className="truncate text-sm text-slate-800">
                  {them.firstName} {them.lastName}
                  {isMe && <span className="text-slate-400"> (you)</span>}
                </p>
                {pending && (
                  <p className="truncate text-xs text-slate-500">
                    Invited by {member.invitedByName} — not joined yet
                  </p>
                )}
              </div>

              {member.role > ROLE.MEMBER && !pending && (
                <span className="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600">
                  {ROLE_LABEL[member.role]}
                </span>
              )}

              {/* Owner-only, because if admins could promote each other then
                  anyone reaching admin could make themselves owner. */}
              {isOwner && !isMe && !pending && (
                <div className="flex shrink-0 gap-2 text-xs">
                  {member.role === ROLE.MEMBER ? (
                    <button
                      disabled={busy}
                      onClick={() => act(() => conversationsApi.setRole(id, them.id, ROLE.ADMIN))}
                      className="text-indigo-600 hover:underline"
                    >
                      Make admin
                    </button>
                  ) : (
                    <button
                      disabled={busy}
                      onClick={() => act(() => conversationsApi.setRole(id, them.id, ROLE.MEMBER))}
                      className="text-slate-500 hover:underline"
                    >
                      Remove admin
                    </button>
                  )}

                  <button
                    disabled={busy}
                    onClick={() =>
                      confirm(
                        `Make ${them.firstName} the owner? You become an admin and cannot undo this yourself.`
                      ) && act(() => conversationsApi.setRole(id, them.id, ROLE.OWNER))
                    }
                    className="text-slate-500 hover:underline"
                  >
                    Make owner
                  </button>
                </div>
              )}

              {canManage && !isMe && member.role !== ROLE.OWNER && (
                <button
                  disabled={busy}
                  onClick={() =>
                    confirm(`Remove ${them.firstName} from the group?`) &&
                    act(() => conversationsApi.removeMember(id, them.id))
                  }
                  className="shrink-0 text-xs text-slate-400 hover:text-red-600"
                >
                  Remove
                </button>
              )}
            </li>
          );
        })}
      </ul>

      {canManage && (
        <div className="mt-4">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
            Invite {adding.length > 0 && `— ${adding.length} selected`}
          </p>

          {connections.length === 0 ? (
            <p className="mt-2 text-sm text-slate-500">
              You have no connections left to invite.
            </p>
          ) : (
            <>
              {connections.length > 5 && (
                <input
                  type="search"
                  value={filter}
                  onChange={(e) => setFilter(e.target.value)}
                  placeholder="Search connections"
                  className="mt-2 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500"
                />
              )}

              <ul className="mt-2 max-h-40 space-y-1 overflow-y-auto">
                {/* Anyone already ticked stays listed even when the filter would
                    hide them, or typing a name looks like it un-selected them. */}
                {connections
                  .filter(
                    (person) =>
                      adding.includes(person.id) ||
                      invitable.some((p) => p.id === person.id)
                  )
                  .map((person) => (
                    <li key={person.id}>
                      <label className="flex items-center gap-2 rounded-md bg-white px-2 py-1.5 text-sm">
                        <input
                          type="checkbox"
                          checked={adding.includes(person.id)}
                          onChange={() =>
                            setAdding((current) =>
                              current.includes(person.id)
                                ? current.filter((x) => x !== person.id)
                                : [...current, person.id]
                            )
                          }
                        />
                        <Avatar student={person} size={24} />
                        <span className="text-slate-700">
                          {person.firstName} {person.lastName}
                        </span>
                      </label>
                    </li>
                  ))}
              </ul>

              <button
                disabled={busy || adding.length === 0}
                onClick={() =>
                  act(async () => {
                    await conversationsApi.invite(id, adding);
                    setAdding([]);
                  })
                }
                className="mt-2 rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
              >
                Send {adding.length || ""} invitation{adding.length === 1 ? "" : "s"}
              </button>
            </>
          )}
        </div>
      )}

      <button
        onClick={() =>
          confirm(`Leave “${conversation.title}”?`) &&
          conversationsApi
            .leave(id)
            .then(onLeft)
            .catch((err) => onError(err.message))
        }
        className="mt-4 text-sm font-medium text-red-600 hover:underline"
      >
        Leave group
      </button>

      {isOwner && (
        <p className="mt-1 text-xs text-slate-500">
          You own this group. If you leave, the longest-serving admin takes it over.
        </p>
      )}
    </div>
  );
}

/* --------------------------------------------------------------- new group */

function NewGroup({ onClose, onCreated }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
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
      onCreated(
        await conversationsApi.createGroup(name.trim(), chosen, description.trim())
      );
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
        <p className="mt-1 text-xs text-slate-500">
          You will own it. Everyone you pick gets an invitation to accept.
        </p>

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

        <label className="mt-3 block text-sm font-medium text-slate-700">
          Description <span className="font-normal text-slate-400">(optional)</span>
        </label>
        <input
          maxLength={300}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="Weekly problem sets and exam prep"
          className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
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
            className="mt-2 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:border-indigo-500"
          />
        )}

        {people.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">
            You have no connections yet, so there is nobody to invite.
          </p>
        ) : (
          <ul className="mt-2 max-h-56 space-y-1 overflow-y-auto">
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

'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import {
  conversations as conversationsApi,
  connections as connectionsApi,
} from "@/lib/api";
import Avatar, { forgetPhoto } from "@/components/Avatar";
import { filterStudents } from "@/lib/studentSearch";

/** Matches ConversationRole in the API. Ordered, so comparisons work. */
export const ROLE = { MEMBER: 0, ADMIN: 1, OWNER: 2 };

const ROLE_LABEL = { 0: "Member", 1: "Admin", 2: "Owner" };

/**
 * Everything about a group in one place.
 *
 * A dialog of its own rather than a drawer under the people count, because
 * these are settings - naming it, its picture, who is in it, who runs it - and
 * settings hidden behind a dropdown labelled with a head count are settings
 * nobody finds.
 */
export default function GroupSettings({ conversation, me, onChanged, onLeft, onClose }) {
  const [members, setMembers] = useState([]);
  const [connections, setConnections] = useState([]);
  const [filter, setFilter] = useState("");
  const [adding, setAdding] = useState([]);
  const [name, setName] = useState(conversation.title);
  const [description, setDescription] = useState(conversation.description ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);

  const photoInput = useRef(null);
  const id = conversation.id;

  const myRole = conversation.myRole;
  const isOwner = myRole === ROLE.OWNER;
  const canManage = myRole >= ROLE.ADMIN;

  const load = useCallback(async () => {
    try {
      setMembers(await conversationsApi.members(id));
    } catch (err) {
      setError(err.message);
    }
  }, [id]);

  useEffect(() => {
    load();
    if (canManage) connectionsApi.list().then(setConnections).catch(() => {});
  }, [load, canManage]);

  useEffect(() => {
    function onKey(e) {
      if (e.key === "Escape") onClose();
    }

    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  async function act(fn) {
    setBusy(true);
    setError("");

    try {
      await fn();
      await load();
      await onChanged();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function pickPhoto(e) {
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

  const field =
    "w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-6 py-6">
      <button
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full bg-slate-900/25"
      />

      <div className="relative flex max-h-full w-full max-w-lg flex-col rounded-xl border border-slate-200 bg-white shadow-xl">
        <div className="flex shrink-0 items-center justify-between border-b border-slate-200 px-5 py-4">
          <h2 className="font-medium text-slate-900">Group settings</h2>
          <button
            onClick={onClose}
            className="rounded-md px-2 py-1 text-sm text-slate-500 hover:bg-slate-100"
          >
            Close
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-6 overflow-y-auto px-5 py-5">
          {error && (
            <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {error}
            </p>
          )}

          <div className="flex items-center gap-4">
            <Avatar
              group={{ id, title: conversation.title, hasPhoto: conversation.hasPhoto }}
              size={64}
            />

            {canManage ? (
              <div className="flex flex-wrap gap-2">
                <button
                  onClick={() => photoInput.current?.click()}
                  disabled={busy}
                  className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
                >
                  {conversation.hasPhoto ? "Change photo" : "Add photo"}
                </button>

                {conversation.hasPhoto && (
                  <button
                    onClick={() =>
                      act(async () => {
                        await conversationsApi.removeGroupPhoto(id);
                        forgetPhoto(id, "group");
                      })
                    }
                    disabled={busy}
                    className="rounded-md px-3 py-1.5 text-sm font-medium text-slate-500 transition hover:text-red-600 disabled:opacity-50"
                  >
                    Remove photo
                  </button>
                )}

                <input
                  ref={photoInput}
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={pickPhoto}
                  className="hidden"
                />
              </div>
            ) : (
              <p className="text-sm text-slate-500">Only admins can change the picture.</p>
            )}
          </div>

          {canManage && (
            <div className="space-y-2">
              <div>
                <label className="block text-sm font-medium text-slate-700">Name</label>
                <input
                  value={name}
                  onChange={(e) => {
                    setName(e.target.value);
                    setSaved(false);
                  }}
                  maxLength={100}
                  className={`mt-1 ${field}`}
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700">
                  Description <span className="font-normal text-slate-400">(optional)</span>
                </label>
                <input
                  value={description}
                  onChange={(e) => {
                    setDescription(e.target.value);
                    setSaved(false);
                  }}
                  maxLength={300}
                  placeholder="Weekly problem sets and exam prep"
                  className={`mt-1 ${field}`}
                />
              </div>

              <div className="flex items-center gap-3">
                <button
                  disabled={busy || !name.trim()}
                  onClick={() =>
                    act(async () => {
                      await conversationsApi.updateGroup(id, name.trim(), description.trim());
                      setSaved(true);
                    })
                  }
                  className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
                >
                  Save
                </button>

                {saved && <span className="text-sm text-green-700">Saved.</span>}
              </div>
            </div>
          )}

          <div>
            <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
              People
            </p>

            <ul className="mt-2 space-y-1">
              {members.map((member) => {
                const them = member.student;
                const pending = Boolean(member.invitedByName);
                const isMe = them.id === me?.id;

                return (
                  <li
                    key={them.id}
                    className="flex flex-wrap items-center gap-2 rounded-md border border-slate-100 px-2 py-1.5"
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

                    {/* Owner-only, because if admins could promote each other
                        then anyone reaching admin could make themselves owner. */}
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
          </div>

          {canManage && (
            <div>
              <p className="text-xs font-medium uppercase tracking-wide text-slate-400">
                Invite {adding.length > 0 && `— ${adding.length} selected`}
              </p>

              {connections.length === 0 ? (
                <p className="mt-2 text-sm text-slate-500">
                  You have no connections left to invite.
                </p>
              ) : (
                <>
                  {/* Same reasoning as the connections list: a box that only
                      appears past some threshold is a box nobody learns. */}
                  <input
                    type="search"
                    value={filter}
                    onChange={(e) => setFilter(e.target.value)}
                    placeholder="Search connections"
                    className={`mt-2 ${field}`}
                  />

                  <ul className="mt-2 max-h-40 space-y-1 overflow-y-auto">
                    {/* Anyone already ticked stays listed even when the filter
                        would hide them, or typing a name looks like it
                        un-selected everyone chosen before it. */}
                    {connections
                      .filter(
                        (person) =>
                          adding.includes(person.id) ||
                          invitable.some((p) => p.id === person.id)
                      )
                      .map((person) => (
                        <li key={person.id}>
                          <label className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-slate-50">
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
                    Send invitation{adding.length === 1 ? "" : "s"}
                  </button>
                </>
              )}
            </div>
          )}
        </div>

        <div className="shrink-0 border-t border-slate-200 px-5 py-4">
          <button
            onClick={() =>
              confirm(`Leave “${conversation.title}”?`) &&
              conversationsApi
                .leave(id)
                .then(onLeft)
                .catch((err) => setError(err.message))
            }
            className="text-sm font-medium text-red-600 hover:underline"
          >
            Leave group
          </button>

          {isOwner && (
            <p className="mt-1 text-xs text-slate-500">
              You own this group. If you leave, the longest-serving admin takes it over.
            </p>
          )}
        </div>
      </div>
    </div>
  );
}

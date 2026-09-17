'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  students as studentsApi,
  connections as connectionsApi,
  conversations as conversationsApi,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Avatar from "@/components/Avatar";
import { educationLabel } from "@/lib/education";
import ReportDialog from "@/components/ReportDialog";

/**
 * Another student's profile.
 *
 * What is on the page depends entirely on the relationship, and the server
 * decides that - this component renders whatever it is given. Email, major and
 * graduation year simply arrive as null until the two are connected, so there
 * is no client-side rule here that could drift from the real one.
 */
export default function StudentProfilePage() {
  const { id } = useParams();
  const router = useRouter();
  const { user, loading } = useAuth();

  const [student, setStudent] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [note, setNote] = useState("");
  const [composing, setComposing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [reporting, setReporting] = useState(false);

  const load = useCallback(async () => {
    try {
      setStudent(await studentsApi.get(id));
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [id]);

  useEffect(() => {
    if (!loading && user) load();
  }, [loading, user, load]);

  async function act(fn) {
    setBusy(true);

    try {
      await fn();
      await load();
      setComposing(false);
      setNote("");
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function message() {
    setBusy(true);

    try {
      const conversation = await conversationsApi.startDirect(student.id);
      router.push(`/messages?c=${conversation.id}`);
    } catch (err) {
      setError(err.message);
      setBusy(false);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-2xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (!student) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-10">
        <Link href="/students" className="text-sm text-indigo-600 hover:underline">
          &larr; Students
        </Link>
        <div className="mt-6 rounded-xl border border-dashed border-slate-300 px-6 py-12 text-center">
          <p className="text-slate-600">No student found.</p>
          <p className="mt-1 text-sm text-slate-500">
            They may not be listed, or the link may be out of date.
          </p>
        </div>
      </div>
    );
  }

  const connected = student.relationship === "connected";

  const details = [
    ["School", student.school],
    ["Education level", educationLabel(student.educationLevel)],
    ["Major", student.major],
    ["Graduating", student.graduationYear],
    ["Email", student.email],
  ].filter(([, value]) => Boolean(value));

  return (
    <div className="mx-auto max-w-2xl px-6 py-10">
      <Link href="/students" className="text-sm text-indigo-600 hover:underline">
        &larr; Students
      </Link>

      {error && (
        <p className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <div className="mt-4 rounded-xl border border-slate-200 bg-white p-6">
        <div className="flex items-center gap-4">
          <Avatar student={student} size={72} />
          <div className="min-w-0">
            <h1 className="truncate text-xl font-semibold text-slate-900">
              {student.firstName} {student.lastName}
            </h1>
            <p className="truncate text-sm text-slate-600">
              {[student.school, educationLabel(student.educationLevel)]
                .filter(Boolean)
                .join(" · ") || "No school listed"}
            </p>
          </div>
        </div>

        <dl className="mt-6 space-y-2 border-t border-slate-200 pt-4">
          {details.map(([term, value]) => (
            <div key={term} className="flex justify-between gap-4 text-sm">
              <dt className="text-slate-500">{term}</dt>
              <dd className="text-right font-medium text-slate-900">{value}</dd>
            </div>
          ))}
        </dl>

        {!connected && (
          <p className="mt-4 rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-600">
            Connect to see their full profile and send messages.
          </p>
        )}

        <div className="mt-6 flex flex-wrap gap-2 border-t border-slate-200 pt-4">
          {connected && (
            <>
              <button
                onClick={message}
                disabled={busy}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                Message
              </button>
              <button
                onClick={() => act(() => connectionsApi.remove(student.connectionId))}
                disabled={busy}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
              >
                Remove connection
              </button>
            </>
          )}

          {student.relationship === "pending_out" && (
            <>
              <span className="rounded-md bg-slate-100 px-4 py-2 text-sm text-slate-600">
                Request sent
              </span>
              <button
                onClick={() => act(() => connectionsApi.remove(student.connectionId))}
                disabled={busy}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
              >
                Cancel
              </button>
            </>
          )}

          {student.relationship === "pending_in" && (
            <>
              <button
                onClick={() => act(() => connectionsApi.accept(student.connectionId))}
                disabled={busy}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                Accept request
              </button>
              <button
                onClick={() => act(() => connectionsApi.decline(student.connectionId))}
                disabled={busy}
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
              >
                Decline
              </button>
            </>
          )}

          {(student.relationship === "none" || student.relationship === "declined") &&
            (composing ? (
              <div className="w-full">
                <label className="block text-sm font-medium text-slate-700">
                  Say hello (optional)
                </label>
                <textarea
                  rows={2}
                  maxLength={300}
                  autoFocus
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="We're in CS 201 together"
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
                />
                <p className="mt-1 text-xs text-slate-500">
                  This is the only thing they see from you until they accept.
                </p>

                <div className="mt-2 flex gap-2">
                  <button
                    onClick={() => act(() => connectionsApi.request(student.id, note))}
                    disabled={busy}
                    className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
                  >
                    {busy ? "Sending..." : "Send request"}
                  </button>
                  <button
                    onClick={() => setComposing(false)}
                    className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            ) : (
              <button
                onClick={() => setComposing(true)}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
              >
                Connect
              </button>
            ))}

          {student.relationship === "blocked" ? (
            <button
              onClick={() => act(() => connectionsApi.unblock(student.id))}
              disabled={busy}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
            >
              Unblock
            </button>
          ) : (
            <>
              {/* Next to Block rather than hidden in a menu: the two are the
                  answers to different questions, and somebody who needs one
                  often needs the other. */}
              <button
                onClick={() => setReporting(true)}
                disabled={busy}
                className="ml-auto rounded-md px-3 py-2 text-sm font-medium text-slate-400 transition hover:text-slate-700 disabled:opacity-50"
              >
                Report
              </button>

              <button
                onClick={() => {
                  if (confirm(`Block ${student.firstName}? They will not be able to find or message you.`)) {
                    act(() => connectionsApi.block(student.id));
                  }
                }}
                disabled={busy}
                className="rounded-md px-3 py-2 text-sm font-medium text-slate-400 transition hover:text-red-600 disabled:opacity-50"
              >
                Block
              </button>
            </>
          )}
        </div>
      </div>

      {reporting && (
        <ReportDialog student={student} onClose={() => setReporting(false)} />
      )}
    </div>
  );
}

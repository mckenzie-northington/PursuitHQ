'use client';

import { useState } from "react";
import Link from "next/link";
import { support as supportApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

/**
 * Where people say the app is broken.
 *
 * Deliberately one short form rather than a help centre. The failure mode of a
 * support page is that somebody hits a bug, cannot find anywhere to say so, and
 * simply stops using the thing - so the only job here is to be findable and to
 * take two sentences without argument.
 */
export default function SupportPage() {
  const { user, loading } = useAuth();

  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState("");
  const [error, setError] = useState("");

  async function submit(e) {
    e.preventDefault();

    setError("");
    setSent("");

    if (subject.trim().length < 3) {
      setError("Give it a short title so we can tell reports apart.");
      return;
    }

    if (description.trim().length < 10) {
      setError("Tell us a little more - at least a sentence about what happened.");
      return;
    }

    setSending(true);

    try {
      // The page they came from, read here rather than asked for. Falls back to
      // an empty string during the server render, where there is no location.
      const from =
        typeof window === "undefined" ? "" : document.referrer || window.location.href;

      const result = await supportApi.reportProblem(
        subject.trim(),
        description.trim(),
        from.slice(0, 300)
      );

      setSent(result.message);
      setSubject("");
      setDescription("");
    } catch (err) {
      setError(err.message);
    } finally {
      setSending(false);
    }
  }

  if (loading) {
    return <div className="mx-auto max-w-2xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (!user) {
    return (
      <div className="mx-auto max-w-2xl px-6 py-10">
        <h1 className="text-2xl font-semibold">Report a problem</h1>
        <p className="mt-2 text-sm text-slate-600">
          You need to be signed in to send a report, so we know who to reply to.{" "}
          <Link href="/login" className="font-medium text-indigo-600 hover:underline">
            Sign in
          </Link>
          , or email us directly at{" "}
          <a
            href="mailto:support@pursuit-hq.com"
            className="font-medium text-indigo-600 hover:underline"
          >
            support@pursuit-hq.com
          </a>
          .
        </p>
      </div>
    );
  }

  const field =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="mx-auto max-w-2xl px-6 py-10">
      <h1 className="text-2xl font-semibold">Report a problem</h1>
      <p className="mt-1 text-sm text-slate-600">
        Something broken, confusing, or just wrong? Tell us. It goes straight to a
        person.
      </p>

      <form onSubmit={submit} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
        <label className="block text-sm font-medium text-slate-700">
          What is it about?
          <input
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            maxLength={120}
            placeholder="Assignments page will not load"
            className={field}
          />
        </label>

        <label className="mt-4 block text-sm font-medium text-slate-700">
          What happened?
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={4000}
            rows={7}
            placeholder="What you were doing, what you expected, and what happened instead. If you saw an error message, paste it here."
            className={`${field} resize-y`}
          />
        </label>

        <p className="mt-2 text-xs text-slate-500">
          We will include your name, your email address and the page you came from,
          so we can reply and find the problem. Nothing else.
        </p>

        {error && (
          <p className="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
        )}

        {sent && (
          <p className="mt-4 rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
            {sent}
          </p>
        )}

        <div className="mt-5 flex items-center gap-3">
          <button
            type="submit"
            disabled={sending}
            className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-500 disabled:opacity-60"
          >
            {sending ? "Sending..." : "Send report"}
          </button>

          <span className="text-xs text-slate-500">
            Or email{" "}
            <a
              href="mailto:support@pursuit-hq.com"
              className="font-medium text-indigo-600 hover:underline"
            >
              support@pursuit-hq.com
            </a>
          </span>
        </div>
      </form>

      <p className="mt-6 text-xs text-slate-500">
        Reporting another student for how they are behaving is a different thing -
        use the report option on their profile or on the message, so we can see what
        they said.
      </p>
    </div>
  );
}

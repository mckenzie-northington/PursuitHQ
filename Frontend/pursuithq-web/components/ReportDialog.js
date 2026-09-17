'use client';

import { useEffect, useState } from "react";
import { reports as reportsApi } from "@/lib/api";

/**
 * Matches ReportReason in the API. The numbers are stored, so they must not be
 * reordered here.
 */
const REASONS = [
  { value: 0, label: "Harassment or bullying" },
  { value: 1, label: "Spam" },
  { value: 2, label: "Pretending to be someone else" },
  { value: 3, label: "Inappropriate content" },
  { value: 4, label: "Someone may be in danger" },
  { value: 5, label: "Something else" },
];

/**
 * Reporting a student, optionally about one message.
 *
 * Blocking and reporting are deliberately different things and the dialog says
 * so: blocking is something you do for yourself and takes effect immediately,
 * reporting asks somebody to look. Offering only one of them makes people use
 * the wrong one.
 */
export default function ReportDialog({ student, messageId, messagePreview, onClose }) {
  const [reason, setReason] = useState(0);
  const [details, setDetails] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState("");

  useEffect(() => {
    function onKey(e) {
      if (e.key === "Escape") onClose();
    }

    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError("");

    try {
      const result = await reportsApi.create(
        student.id,
        reason,
        details.trim() || null,
        messageId ?? null
      );

      setDone(result.message);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  const name = `${student.firstName} ${student.lastName}`.trim() || "this student";

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-6 py-6">
      <button
        aria-label="Close"
        onClick={onClose}
        className="absolute inset-0 h-full w-full bg-slate-900/25"
      />

      <div className="relative w-full max-w-md rounded-xl border border-slate-200 bg-white shadow-xl">
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <h2 className="font-medium text-slate-900">
            {done ? "Report sent" : `Report ${name}`}
          </h2>
          <button
            onClick={onClose}
            className="rounded-md px-2 py-1 text-sm text-slate-500 hover:bg-slate-100"
          >
            Close
          </button>
        </div>

        {done ? (
          <div className="space-y-4 px-5 py-5">
            <p className="text-sm text-slate-700">{done}</p>
            <button
              onClick={onClose}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
            >
              Done
            </button>
          </div>
        ) : (
          <form onSubmit={submit} className="space-y-4 px-5 py-5">
            {error && (
              <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {error}
              </p>
            )}

            {messagePreview && (
              <div className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2">
                <p className="text-xs font-medium text-slate-500">Reporting this message</p>
                <p className="mt-0.5 line-clamp-3 text-sm text-slate-700">{messagePreview}</p>
              </div>
            )}

            <fieldset>
              <legend className="text-sm font-medium text-slate-700">
                What is happening?
              </legend>

              <div className="mt-2 space-y-1">
                {REASONS.map((option) => (
                  <label
                    key={option.value}
                    className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
                  >
                    <input
                      type="radio"
                      name="reason"
                      checked={reason === option.value}
                      onChange={() => setReason(option.value)}
                    />
                    {option.label}
                  </label>
                ))}
              </div>
            </fieldset>

            <div>
              <label className="block text-sm font-medium text-slate-700">
                Anything else?{" "}
                <span className="font-normal text-slate-400">(optional)</span>
              </label>
              <textarea
                value={details}
                onChange={(e) => setDetails(e.target.value)}
                rows={3}
                maxLength={2000}
                placeholder="What happened, and when."
                className="mt-1 w-full resize-none rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
              />
            </div>

            <p className="text-xs text-slate-500">
              This goes to whoever runs PursuitHQ. {name} is not told that you
              reported them. If you also want them to stop contacting you, block
              them — that takes effect straight away.
            </p>

            <div className="flex gap-2">
              <button
                type="submit"
                disabled={busy}
                className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                {busy ? "Sending..." : "Send report"}
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
        )}
      </div>
    </div>
  );
}

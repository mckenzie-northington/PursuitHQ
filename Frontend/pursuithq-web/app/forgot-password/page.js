'use client';

import { useState } from "react";
import Link from "next/link";
import { auth } from "@/lib/api";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [resentAt, setResentAt] = useState(null);

  async function send() {
    setError("");
    setBusy(true);

    try {
      const result = await auth.forgotPassword(email);
      setSent(result);
      return true;
    } catch (err) {
      setError(err.message);
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setResentAt(null);
    await send();
  }

  async function resend() {
    if (await send()) setResentAt(new Date());
  }

  /** Back to the form, with the address still in the box to correct. */
  function startOver() {
    setSent(null);
    setResentAt(null);
    setError("");
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-6">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-semibold text-slate-900">
            Pursuit<span className="text-indigo-600">HQ</span>
          </h1>
          <p className="mt-2 text-sm text-slate-600">Reset your password</p>
        </div>

        {sent ? (
          <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <p className="text-sm font-medium text-slate-900">Check your email</p>

            {/*
              The address is shown back because getting it wrong is the most
              likely reason nothing arrives, and you cannot spot a typo you
              cannot see.

              Still phrased as a condition. Saying "sent" outright would confirm
              that an account exists for this address, and anyone can type any
              address into this box - which is the whole reason the API answers
              the same way either way.
            */}
            <p className="text-sm text-slate-700">
              If an account exists for{" "}
              <span className="font-medium text-slate-900">{email}</span>, a reset link is on
              its way. It is good for one hour.
            </p>

            <p className="text-sm text-slate-600">
              Nothing yet? Give it a minute and check your spam folder.
            </p>

            {error && (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {error}
              </div>
            )}

            {resentAt && !error && (
              <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
                Sent again at {resentAt.toLocaleTimeString()}.
              </div>
            )}

            <div className="flex flex-col gap-2">
              <button
                type="button"
                onClick={resend}
                disabled={busy}
                className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
              >
                {busy ? "Sending..." : "Send it again"}
              </button>

              <button
                type="button"
                onClick={startOver}
                disabled={busy}
                className="w-full rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50"
              >
                Use a different email
              </button>
            </div>

            <Link
              href="/login"
              className="block text-center text-sm font-medium text-indigo-600 hover:underline"
            >
              Back to sign in
            </Link>
          </div>
        ) : (
          <form
            onSubmit={handleSubmit}
            className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
          >
            {error && (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {error}
              </div>
            )}

            <p className="text-sm text-slate-600">
              Enter the email you signed up with and we will send a link to set a new
              password.
            </p>

            <div>
              <label className="block text-sm font-medium text-slate-700">Email</label>
              <input
                type="email"
                required
                autoFocus
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
                placeholder="you@school.edu"
              />
            </div>

            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Sending..." : "Send reset link"}
            </button>

            <p className="text-center text-sm text-slate-600">
              Remembered it?{" "}
              <Link href="/login" className="font-medium text-indigo-600 hover:underline">
                Sign in
              </Link>
            </p>
          </form>
        )}
      </div>
    </div>
  );
}

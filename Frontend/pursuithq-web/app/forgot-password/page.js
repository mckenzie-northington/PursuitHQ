'use client';

import { useState } from "react";
import Link from "next/link";
import { auth } from "@/lib/api";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setBusy(true);

    try {
      setSent(await auth.forgotPassword(email));
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
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
            <p className="text-sm text-slate-700">{sent.message}</p>

            {/*
              Only ever present in development. The API refuses to include the
              link in any other environment, because handing the token back to
              whoever asked would let anyone reset anyone's password.
            */}
            {sent.developmentResetUrl && (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                <p className="font-medium">Development mode</p>
                <p className="mt-1">
                  Email is not set up yet, so the link is shown here instead of being sent.
                </p>
                <Link
                  href={sent.developmentResetUrl.replace(/^https?:\/\/[^/]+/, "")}
                  className="mt-2 block font-medium text-indigo-600 hover:underline"
                >
                  Open the reset link &rarr;
                </Link>
              </div>
            )}

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

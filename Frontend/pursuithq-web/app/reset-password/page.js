'use client';

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { auth } from "@/lib/api";

export default function ResetPasswordPage() {
  // useSearchParams opts the page into client-side rendering, and Next wants
  // that boundary stated explicitly rather than inferred.
  return (
    <Suspense
      fallback={<div className="px-6 py-10 text-center text-slate-500">Loading...</div>}
    >
      <ResetPasswordForm />
    </Suspense>
  );
}

function ResetPasswordForm() {
  const params = useSearchParams();
  const router = useRouter();

  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");

    if (password !== confirm) {
      setError("The passwords do not match.");
      return;
    }

    setBusy(true);

    try {
      await auth.resetPassword({ email, token, newPassword: password });
      setDone(true);
    } catch (err) {
      // Identity says exactly what a rejected password was missing, which is
      // more use than "that password was not accepted".
      const details = err.details ? Object.values(err.details).flat().join(" ") : "";
      setError(details || err.message);
    } finally {
      setBusy(false);
    }
  }

  const card = "space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm";

  return (
    <div className="flex min-h-screen items-center justify-center px-6">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-semibold text-slate-900">
            Pursuit<span className="text-indigo-600">HQ</span>
          </h1>
          <p className="mt-2 text-sm text-slate-600">Set a new password</p>
        </div>

        {!token || !email ? (
          <div className={card}>
            <p className="text-sm text-slate-700">
              This link is missing part of itself. Copy the whole link, or ask for a new
              one.
            </p>
            <Link
              href="/forgot-password"
              className="block text-center text-sm font-medium text-indigo-600 hover:underline"
            >
              Request a new link
            </Link>
          </div>
        ) : done ? (
          <div className={card}>
            <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
              Your password has been changed, and any lockout has been cleared.
            </div>
            <button
              onClick={() => router.push("/login")}
              className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
            >
              Sign in
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className={card}>
            {error && (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {error}
              </div>
            )}

            <p className="text-sm text-slate-600">
              Setting a new password for <span className="font-medium">{email}</span>.
            </p>

            <div>
              <label className="block text-sm font-medium text-slate-700">New password</label>
              <input
                type="password"
                required
                autoFocus
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700">
                Confirm password
              </label>
              <input
                type="password"
                required
                autoComplete="new-password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
              />
            </div>

            <p className="text-xs text-slate-500">
              At least 8 characters, with an uppercase letter, a lowercase letter, and a
              number.
            </p>

            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Saving..." : "Set new password"}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

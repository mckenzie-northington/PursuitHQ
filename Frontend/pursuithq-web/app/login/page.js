'use client';

import { useState } from "react";
import Link from "next/link";
import { auth } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function LoginPage() {
  const { signIn } = useAuth();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  // Set alongside the message so the error box can offer a way out, rather than
  // leaving somebody to work out for themselves that they never signed up.
  const [noAccount, setNoAccount] = useState(false);
  const [busy, setBusy] = useState(false);

  /**
   * Set when the password was right but the account wants a code.
   *
   * Holding the token here rather than in storage is deliberate: it is good for
   * five minutes and for one thing, and a refresh sending you back to the
   * password box is the correct outcome, not a bug to work around.
   */
  const [pending, setPending] = useState(null);
  const [code, setCode] = useState("");

  async function submitPassword(e) {
    e.preventDefault();
    setError("");
    setNoAccount(false);
    setBusy(true);

    try {
      const result = await auth.login({ email, password });

      // Checked before anything touches result.token, which is empty at this
      // point - there is no session yet, only a correct password.
      if (result.requiresTwoFactor) {
        setPending(result.twoFactorToken);
        setPassword("");
      } else {
        signIn(result.token, result.user);
        return;
      }
    } catch (err) {
      setError(err.message);
      setNoAccount(err.code === "NoAccount");
    }

    setBusy(false);
  }

  async function submitCode(e) {
    e.preventDefault();
    setError("");
    setNoAccount(false);
    setBusy(true);

    try {
      const result = await auth.twoFactor.verify(pending, code);
      signIn(result.token, result.user);
      return;
    } catch (err) {
      setError(err.message);
      setCode("");
    }

    setBusy(false);
  }

  function startOver() {
    setPending(null);
    setCode("");
    setError("");
    setNoAccount(false);
  }

  const field =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="flex min-h-screen items-center justify-center px-6">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-semibold text-slate-900">
            Pursuit<span className="text-indigo-600">HQ</span>
          </h1>
          <p className="mt-2 text-sm text-slate-600">
            {pending ? "One more step" : "Sign in to your account"}
          </p>
        </div>

        {error && (
          <div className="mb-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}

            {noAccount && (
              <>
                {" "}
                <Link href="/register" className="font-medium underline">
                  Create one
                </Link>
                , or check the spelling.
              </>
            )}
          </div>
        )}

        {pending ? (
          <form
            onSubmit={submitCode}
            className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
          >
            <p className="text-sm text-slate-600">
              Open your authenticator app and enter the six-digit code for PursuitHQ.
            </p>

            <div>
              <label className="block text-sm font-medium text-slate-700">Code</label>
              <input
                type="text"
                required
                autoFocus
                // One-time-code lets phones offer the code from the notification,
                // and the numeric keypad saves hunting for digits.
                autoComplete="one-time-code"
                inputMode="numeric"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                className={`${field} text-center text-lg tracking-[0.3em]`}
                placeholder="000000"
              />
            </div>

            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Checking..." : "Verify"}
            </button>

            <p className="text-xs text-slate-500">
              Lost your phone? Enter one of your recovery codes in the same box. Each one
              works once.
            </p>

            <p className="text-center text-sm">
              <button
                type="button"
                onClick={startOver}
                className="font-medium text-indigo-600 hover:underline"
              >
                Start over
              </button>
            </p>
          </form>
        ) : (
          <form
            onSubmit={submitPassword}
            className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
          >
            <div>
              <label className="block text-sm font-medium text-slate-700">Email</label>
              <input
                type="email"
                required
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className={field}
                placeholder="you@school.edu"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700">Password</label>
              <input
                type="password"
                required
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className={field}
              />
            </div>

            <button
              type="submit"
              disabled={busy}
              className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {busy ? "Signing in..." : "Sign in"}
            </button>

            <p className="text-center text-sm">
              <Link
                href="/forgot-password"
                className="font-medium text-indigo-600 hover:underline"
              >
                Forgot your password?
              </Link>
            </p>

            <p className="text-center text-sm text-slate-600">
              No account?{" "}
              <Link href="/register" className="font-medium text-indigo-600 hover:underline">
                Create one
              </Link>
            </p>

            {/* Reachable without an account, which is the point - somebody
                deciding whether to sign up is exactly who needs to read them. */}
            <p className="text-center text-xs text-slate-400">
              <Link href="/privacy" className="hover:text-slate-600 hover:underline">
                Privacy
              </Link>
              {" · "}
              <Link href="/terms" className="hover:text-slate-600 hover:underline">
                Terms
              </Link>
            </p>
          </form>
        )}
      </div>
    </div>
  );
}

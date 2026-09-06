'use client';

import { useState } from "react";
import Link from "next/link";
import { auth } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

export default function RegisterPage() {
  const { signIn } = useAuth();
  const [form, setForm] = useState({
    firstName: "",
    lastName: "",
    email: "",
    password: "",
    major: "",
    graduationYear: "",
  });
  const [error, setError] = useState("");
  const [details, setDetails] = useState(null);
  const [busy, setBusy] = useState(false);

  function update(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError("");
    setDetails(null);
    setBusy(true);

    try {
      const result = await auth.register({
        ...form,
        graduationYear: form.graduationYear ? Number(form.graduationYear) : null,
        major: form.major || null,
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "America/New_York",
      });
      signIn(result.token, result.user);
    } catch (err) {
      setError(err.message);
      setDetails(err.details || null);
      setBusy(false);
    }
  }

  const input =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="flex min-h-screen items-center justify-center px-6 py-10">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-semibold text-slate-900">
            Pursuit<span className="text-indigo-600">HQ</span>
          </h1>
          <p className="mt-2 text-sm text-slate-600">Create your student account</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          {error && (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              <p>{error}</p>
              {details && (
                <ul className="mt-1 list-inside list-disc">
                  {Object.values(details).flat().map((d, i) => (
                    <li key={i}>{d}</li>
                  ))}
                </ul>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700">First name</label>
              <input required value={form.firstName} onChange={(e) => update("firstName", e.target.value)} className={input} />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Last name</label>
              <input required value={form.lastName} onChange={(e) => update("lastName", e.target.value)} className={input} />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Email</label>
            <input type="email" required value={form.email} onChange={(e) => update("email", e.target.value)} className={input} placeholder="you@school.edu" />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700">Password</label>
            <input type="password" required value={form.password} onChange={(e) => update("password", e.target.value)} className={input} />
            <p className="mt-1 text-xs text-slate-500">
              At least 8 characters, with an uppercase letter, a lowercase letter, and a number.
            </p>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-slate-700">Major</label>
              <input value={form.major} onChange={(e) => update("major", e.target.value)} className={input} placeholder="Optional" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Grad year</label>
              <input type="number" value={form.graduationYear} onChange={(e) => update("graduationYear", e.target.value)} className={input} placeholder="2028" />
            </div>
          </div>

          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
          >
            {busy ? "Creating account..." : "Create account"}
          </button>

          <p className="text-center text-sm text-slate-600">
            Already have an account?{" "}
            <Link href="/login" className="font-medium text-indigo-600 hover:underline">
              Sign in
            </Link>
          </p>
        </form>
      </div>
    </div>
  );
}

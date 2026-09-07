'use client';

import { useCallback, useEffect, useState } from "react";
import { applications as appsApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

// These indexes match the ApplicationStatus enum in the API.
const COLUMNS = [
  { status: 0, label: "Saved", tone: "slate" },
  { status: 1, label: "Applied", tone: "blue" },
  { status: 2, label: "Interview", tone: "amber" },
  { status: 3, label: "Offer", tone: "green" },
  { status: 4, label: "Rejected", tone: "rose" },
];

const TYPES = { 0: "Internship", 1: "Part-time", 2: "Full-time" };

const TONES = {
  slate: "border-slate-300 bg-slate-50 text-slate-700",
  blue: "border-blue-300 bg-blue-50 text-blue-700",
  amber: "border-amber-300 bg-amber-50 text-amber-800",
  green: "border-green-300 bg-green-50 text-green-700",
  rose: "border-rose-300 bg-rose-50 text-rose-700",
};

const EMPTY = {
  company: "", role: "", type: 0, status: 0,
  appliedDate: "", notes: "", sourceUrl: "",
};

export default function ApplicationsPage() {
  const { user, loading } = useAuth();

  const [items, setItems] = useState([]);
  const [stats, setStats] = useState(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [view, setView] = useState("board");

  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(EMPTY);
  const [editingId, setEditingId] = useState(null);
  const [busy, setBusy] = useState(false);

  const [dragged, setDragged] = useState(null);
  const [dropColumn, setDropColumn] = useState(null);

  const refresh = useCallback(async () => {
    try {
      const [list, s] = await Promise.all([
        appsApi.list({ search: search || undefined }),
        appsApi.stats(),
      ]);
      setItems(list);
      setStats(s);
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [search]);

  useEffect(() => {
    if (loading || !user) return;
    const timer = setTimeout(refresh, search ? 300 : 0);
    return () => clearTimeout(timer);
  }, [loading, user, refresh, search]);

  function startAdd() {
    setForm(EMPTY);
    setEditingId(null);
    setShowForm(true);
  }

  function startEdit(a) {
    setForm({
      company: a.company,
      role: a.role,
      type: a.type,
      status: a.status,
      appliedDate: a.appliedDate ? a.appliedDate.slice(0, 10) : "",
      notes: a.notes || "",
      sourceUrl: a.sourceUrl || "",
    });
    setEditingId(a.id);
    setShowForm(true);
  }

  async function save(e) {
    e.preventDefault();
    setBusy(true);
    setError("");

    const payload = {
      company: form.company,
      role: form.role,
      type: Number(form.type),
      status: Number(form.status),
      appliedDate: form.appliedDate ? new Date(form.appliedDate).toISOString() : null,
      notes: form.notes || null,
      sourceUrl: form.sourceUrl || null,
    };

    try {
      if (editingId) await appsApi.update(editingId, payload);
      else await appsApi.create(payload);

      setShowForm(false);
      setForm(EMPTY);
      setEditingId(null);
      await refresh();
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  async function moveTo(application, status) {
    if (application.status === status) return;

    // Update the card immediately so the drag feels instant, then reconcile
    // with the server. On failure the refresh puts it back.
    setItems((prev) => prev.map((a) => (a.id === application.id ? { ...a, status } : a)));

    try {
      await appsApi.setStatus(application.id, status);
      await refresh();
    } catch (err) {
      setError(err.message);
      await refresh();
    }
  }

  async function remove(a) {
    if (!confirm(`Delete the ${a.role} application at ${a.company}?`)) return;
    try {
      await appsApi.remove(a.id);
      await refresh();
    } catch (err) {
      setError(err.message);
    }
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-6xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  const input =
    "mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500";

  return (
    <div className="mx-auto max-w-6xl px-6 py-10">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Internships &amp; Jobs</h1>
          <p className="mt-1 text-sm text-slate-600">
            Everything you have applied to, and where each one stands.
          </p>
        </div>
        <button
          onClick={startAdd}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Add application
        </button>
      </div>

      {stats && stats.total > 0 && (
        <div className="mt-6 grid gap-3 sm:grid-cols-3">
          <Stat label="Total tracked" value={stats.total} />
          <Stat label="Active interviews" value={stats.interview} tone="amber" />
          <Stat
            label="Interview rate"
            value={`${stats.interviewRate}%`}
            hint="of applications actually submitted"
          />
        </div>
      )}

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search company or role..."
          className="w-64 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />

        <div className="flex rounded-md border border-slate-300 p-0.5">
          {["board", "list"].map((v) => (
            <button
              key={v}
              onClick={() => setView(v)}
              className={`rounded px-3 py-1 text-sm font-medium capitalize transition ${
                view === v ? "bg-indigo-600 text-white" : "text-slate-600 hover:bg-slate-100"
              }`}
            >
              {v}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">Dismiss</button>
        </div>
      )}

      {showForm && (
        <form onSubmit={save} className="mt-6 rounded-xl border border-slate-200 bg-white p-5">
          <h2 className="font-medium">{editingId ? "Edit application" : "New application"}</h2>

          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <label className="block text-sm font-medium text-slate-700">Company</label>
              <input required value={form.company} onChange={(e) => setForm({ ...form, company: e.target.value })} className={input} placeholder="Acme Corp" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Role</label>
              <input required value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} className={input} placeholder="Software Engineering Intern" />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Type</label>
              <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })} className={input}>
                {Object.entries(TYPES).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Status</label>
              <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })} className={input}>
                {COLUMNS.map((c) => <option key={c.status} value={c.status}>{c.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Date applied</label>
              <input type="date" value={form.appliedDate} onChange={(e) => setForm({ ...form, appliedDate: e.target.value })} className={input} />
              <p className="mt-1 text-xs text-slate-500">Left blank, today is used when the status is past Saved.</p>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700">Posting link</label>
              <input type="url" value={form.sourceUrl} onChange={(e) => setForm({ ...form, sourceUrl: e.target.value })} className={input} placeholder="https://..." />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-sm font-medium text-slate-700">Notes</label>
              <textarea rows={3} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} className={input} placeholder="Recruiter name, interview dates, follow-ups..." />
            </div>
          </div>

          <div className="mt-5 flex gap-2">
            <button type="submit" disabled={busy} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50">
              {busy ? "Saving..." : editingId ? "Save changes" : "Add application"}
            </button>
            <button type="button" onClick={() => setShowForm(false)} className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
              Cancel
            </button>
          </div>
        </form>
      )}

      {items.length === 0 ? (
        <div className="mt-8 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-16 text-center">
          <p className="text-slate-600">
            {search ? `Nothing matches "${search}".` : "No applications tracked yet."}
          </p>
          {!search && (
            <button onClick={startAdd} className="mt-3 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
              Add your first one
            </button>
          )}
        </div>
      ) : view === "board" ? (
        <div className="mt-8 grid gap-3 md:grid-cols-5">
          {COLUMNS.map((col) => {
            const cards = items.filter((a) => a.status === col.status);
            return (
              <div
                key={col.status}
                onDragOver={(e) => {
                  if (!dragged) return;
                  e.preventDefault();
                  setDropColumn(col.status);
                }}
                onDragLeave={() => setDropColumn((c) => (c === col.status ? null : c))}
                onDrop={(e) => {
                  if (!dragged) return;
                  e.preventDefault();
                  moveTo(dragged, col.status);
                  setDragged(null);
                  setDropColumn(null);
                }}
                className={`rounded-xl border p-2 transition ${
                  dropColumn === col.status
                    ? "border-indigo-400 bg-indigo-50 ring-2 ring-indigo-300"
                    : "border-slate-200 bg-slate-50/60"
                }`}
              >
                <div className="flex items-center justify-between px-2 py-1.5">
                  <span className="text-sm font-medium text-slate-700">{col.label}</span>
                  <span className="rounded-full bg-white px-2 py-0.5 text-xs font-medium text-slate-600">
                    {cards.length}
                  </span>
                </div>

                <div className="space-y-2">
                  {cards.map((a) => (
                    <div
                      key={a.id}
                      draggable
                      onDragStart={() => setDragged(a)}
                      onDragEnd={() => { setDragged(null); setDropColumn(null); }}
                      className={`cursor-grab rounded-lg border border-slate-200 bg-white p-3 shadow-sm transition active:cursor-grabbing ${
                        dragged?.id === a.id ? "opacity-40" : "hover:shadow"
                      }`}
                    >
                      <p className="text-sm font-medium text-slate-900">{a.company}</p>
                      <p className="mt-0.5 text-xs text-slate-600">{a.role}</p>

                      <div className="mt-2 flex flex-wrap items-center gap-1.5">
                        <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] text-slate-600">
                          {TYPES[a.type]}
                        </span>
                        {a.daysSinceApplied !== null && a.daysSinceApplied !== undefined && (
                          <span className={`rounded px-1.5 py-0.5 text-[11px] ${
                            a.status === 1 && a.daysSinceApplied > 21
                              ? "bg-amber-100 text-amber-800"
                              : "bg-slate-100 text-slate-600"
                          }`}>
                            {a.daysSinceApplied}d ago
                          </span>
                        )}
                      </div>

                      <div className="mt-2 flex gap-2 text-xs">
                        <button onClick={() => startEdit(a)} className="text-indigo-600 hover:underline">Edit</button>
                        {a.sourceUrl && (
                          <a href={a.sourceUrl} target="_blank" rel="noopener noreferrer" className="text-slate-500 hover:underline">
                            Posting
                          </a>
                        )}
                        <button onClick={() => remove(a)} className="ml-auto text-slate-400 hover:text-red-600">Delete</button>
                      </div>
                    </div>
                  ))}

                  {cards.length === 0 && (
                    <p className="px-2 py-6 text-center text-xs text-slate-400">
                      {dragged ? "Drop here" : "Empty"}
                    </p>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <ul className="mt-8 divide-y divide-slate-200 rounded-xl border border-slate-200 bg-white">
          {items.map((a) => {
            const col = COLUMNS.find((c) => c.status === a.status);
            return (
              <li key={a.id} className="flex items-center justify-between gap-4 px-4 py-3">
                <div className="min-w-0">
                  <p className="truncate font-medium text-slate-900">
                    {a.role} <span className="font-normal text-slate-500">at</span> {a.company}
                  </p>
                  <p className="text-sm text-slate-500">
                    {TYPES[a.type]}
                    {a.appliedDate && ` · applied ${new Date(a.appliedDate).toLocaleDateString()}`}
                    {a.daysSinceApplied != null && ` (${a.daysSinceApplied}d ago)`}
                  </p>
                </div>

                <div className="flex shrink-0 items-center gap-3 text-sm">
                  <select
                    value={a.status}
                    onChange={(e) => moveTo(a, Number(e.target.value))}
                    className={`rounded-full border px-2 py-1 text-xs font-medium ${TONES[col?.tone ?? "slate"]}`}
                  >
                    {COLUMNS.map((c) => <option key={c.status} value={c.status}>{c.label}</option>)}
                  </select>
                  <button onClick={() => startEdit(a)} className="text-indigo-600 hover:underline">Edit</button>
                  <button onClick={() => remove(a)} className="text-slate-400 hover:text-red-600">Delete</button>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function Stat({ label, value, tone, hint }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4">
      <p className="text-sm text-slate-600">{label}</p>
      <p className={`mt-1 text-2xl font-semibold ${tone === "amber" ? "text-amber-600" : "text-slate-900"}`}>
        {value}
      </p>
      {hint && <p className="mt-0.5 text-xs text-slate-500">{hint}</p>}
    </div>
  );
}

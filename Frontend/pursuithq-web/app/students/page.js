'use client';

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { students as studentsApi, connections as connectionsApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import Avatar from "@/components/Avatar";
import { educationLabel } from "@/lib/education";
import { filterStudents } from "@/lib/studentSearch";

const TABS = [
  { key: "search", label: "Find students" },
  { key: "requests", label: "Requests" },
  { key: "connections", label: "Connections" },
];

/** A name search needs two letters; an email has to be whole. */
const looksLikeEmail = (text) => text.includes("@") && text.includes(".");

export default function StudentsPage() {
  const { user, loading } = useAuth();
  const [tab, setTab] = useState("search");
  const [requestCount, setRequestCount] = useState(0);

  const refreshCount = useCallback(async () => {
    try {
      setRequestCount((await connectionsApi.requests()).length);
    } catch {
      // The badge is a nicety; a failed count should not break the page.
    }
  }, []);

  useEffect(() => {
    if (!loading && user) refreshCount();
  }, [loading, user, refreshCount]);

  if (loading) {
    return <div className="mx-auto max-w-3xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-2xl font-semibold">Students</h1>
      <p className="mt-1 text-sm text-slate-600">
        Find classmates, answer requests, and see who you are connected with.
      </p>

      <div className="mt-6 flex gap-1 border-b border-slate-200">
        {TABS.map((item) => (
          <button
            key={item.key}
            onClick={() => setTab(item.key)}
            className={`-mb-px border-b-2 px-4 py-2 text-sm font-medium transition ${
              tab === item.key
                ? "border-indigo-600 text-indigo-700"
                : "border-transparent text-slate-600 hover:text-slate-900"
            }`}
          >
            {item.label}
            {item.key === "requests" && requestCount > 0 && (
              <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-700">
                {requestCount}
              </span>
            )}
          </button>
        ))}
      </div>

      <div className="mt-6">
        {tab === "search" && <FindStudents me={user} />}
        {tab === "requests" && <Requests onChange={refreshCount} />}
        {tab === "connections" && <Connections />}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ search */

function FindStudents({ me }) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [searched, setSearched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  // Stops a slow earlier request painting over a faster later one.
  const latest = useRef(0);

  const run = useCallback(async (text) => {
    const trimmed = text.trim();

    if (trimmed.length < 2) {
      setResults([]);
      setSearched(false);
      return;
    }

    const ticket = ++latest.current;
    setBusy(true);
    setError("");

    try {
      // An address goes to the exact lookup, which finds people who are not
      // listed in the directory. Anything else is a name search.
      const found = looksLikeEmail(trimmed)
        ? [await studentsApi.lookup(trimmed)]
        : await studentsApi.search(trimmed);

      if (ticket === latest.current) {
        setResults(found);
        setSearched(true);
      }
    } catch (err) {
      if (ticket !== latest.current) return;

      setResults([]);
      setSearched(true);

      // "Nobody uses that address" is an empty result, not a failure.
      if (!/not found|nobody/i.test(err.message)) setError(err.message);
    } finally {
      if (ticket === latest.current) setBusy(false);
    }
  }, []);

  useEffect(() => {
    const id = setTimeout(() => run(query), 350);
    return () => clearTimeout(id);
  }, [query, run]);

  return (
    <div>
      {me && !me.isDiscoverable && (
        <div className="mb-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm">
          <p className="text-amber-800">
            You are not listed, so classmates cannot find you by name.
          </p>
          <p className="mt-1 text-xs text-amber-700">
            They can still reach you if they know your email.{" "}
            <Link href="/settings" className="font-medium underline">
              Change this in Settings
            </Link>
            .
          </p>
        </div>
      )}

      <input
        type="search"
        autoFocus
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Search by name, or type a full email address"
        className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
      />

      <p className="mt-2 text-xs text-slate-500">
        Name search only finds students who have chosen to be listed. An exact email
        address finds anyone.
      </p>

      {error && (
        <p className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <div className="mt-6">
        {query.trim().length > 0 && query.trim().length < 2 ? (
          <p className="text-sm text-slate-500">Keep typing — at least two letters.</p>
        ) : busy ? (
          <p className="text-sm text-slate-500">Searching...</p>
        ) : searched && results.length === 0 ? (
          <Empty
            title="Nobody found."
            hint="They may not be listed. If you know their email address, try that instead."
          />
        ) : (
          <ul className="space-y-2">
            {results.filter(Boolean).map((student) => (
              <StudentRow key={student.id} student={student} />
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

/* ---------------------------------------------------------------- requests */

function Requests({ onChange }) {
  const [incoming, setIncoming] = useState([]);
  const [outgoing, setOutgoing] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      const [inbox, sent] = await Promise.all([
        connectionsApi.requests(),
        connectionsApi.sent(),
      ]);

      setIncoming(inbox);
      setOutgoing(sent);
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function act(fn) {
    try {
      await fn();
      await load();
      onChange?.();
    } catch (err) {
      setError(err.message);
    }
  }

  if (!ready) return <p className="text-sm text-slate-500">Loading...</p>;

  return (
    <div className="space-y-8">
      {error && (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      <section>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
          Waiting for you
        </h2>

        {incoming.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">No requests right now.</p>
        ) : (
          <ul className="mt-3 space-y-2">
            {incoming.map((request) => (
              <li
                key={request.id}
                className="rounded-xl border border-slate-200 bg-white p-4"
              >
                <StudentLine student={request.student} />

                {request.note && (
                  <p className="mt-3 rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-700">
                    “{request.note}”
                  </p>
                )}

                <div className="mt-3 flex gap-2">
                  <button
                    onClick={() => act(() => connectionsApi.accept(request.id))}
                    className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-indigo-700"
                  >
                    Accept
                  </button>
                  <button
                    onClick={() => act(() => connectionsApi.decline(request.id))}
                    className="rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                  >
                    Decline
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section>
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
          Sent
        </h2>

        {outgoing.length === 0 ? (
          <p className="mt-3 text-sm text-slate-500">Nothing waiting on anyone else.</p>
        ) : (
          <ul className="mt-3 space-y-2">
            {outgoing.map((request) => (
              <li
                key={request.id}
                className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white p-4"
              >
                <StudentLine student={request.student} />
                <button
                  onClick={() => act(() => connectionsApi.remove(request.id))}
                  className="shrink-0 text-sm font-medium text-slate-500 hover:text-red-600"
                >
                  Cancel
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

/* ------------------------------------------------------------- connections */

function Connections() {
  const [people, setPeople] = useState([]);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("");

  useEffect(() => {
    connectionsApi
      .list()
      .then(setPeople)
      .catch((err) => setError(err.message))
      .finally(() => setReady(true));
  }, []);

  if (!ready) return <p className="text-sm text-slate-500">Loading...</p>;

  if (error) {
    return (
      <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {error}
      </p>
    );
  }

  if (people.length === 0) {
    return (
      <Empty
        title="No connections yet."
        hint="Find someone under “Find students” and send them a request."
      />
    );
  }

  const shown = filterStudents(people, filter);

  return (
    <div>
      {/* Always shown, even for a short list. It used to appear only past five
          connections, which meant the one feature people go looking for was
          invisible exactly when they were learning the page - and a search box
          that comes and goes is harder to trust than one that is simply there. */}
      <input
        type="search"
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        placeholder={`Search your ${people.length} ${
          people.length === 1 ? "connection" : "connections"
        }`}
        className="mb-3 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
      />

      {shown.length === 0 ? (
        <p className="py-6 text-center text-sm text-slate-500">
          Nobody matches “{filter}”.
        </p>
      ) : (
        <ul className="space-y-2">
          {shown.map((student) => (
            <StudentRow key={student.id} student={student} />
          ))}
        </ul>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ pieces */

function StudentLine({ student }) {
  return (
    <div className="flex min-w-0 items-center gap-3">
      <Avatar student={student} size={40} />
      <div className="min-w-0">
        <p className="truncate font-medium text-slate-900">
          {student.firstName} {student.lastName}
        </p>
        <p className="truncate text-sm text-slate-600">
          {[student.school, educationLabel(student.educationLevel)]
            .filter(Boolean)
            .join(" · ") || "No school listed"}
        </p>
      </div>
    </div>
  );
}

function StudentRow({ student }) {
  return (
    <li className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3">
      <StudentLine student={student} />

      <Link
        href={`/students/${student.id}`}
        className="shrink-0 rounded-md border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
      >
        {student.relationship === "connected" ? "Open" : "View"}
      </Link>
    </li>
  );
}

function Empty({ title, hint }) {
  return (
    <div className="rounded-xl border border-dashed border-slate-300 px-6 py-10 text-center">
      <p className="text-slate-600">{title}</p>
      <p className="mt-1 text-sm text-slate-500">{hint}</p>
    </div>
  );
}
